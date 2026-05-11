/**
 * 拉卡拉调试工具 - 后端代理服务
 *
 * 功能:
 *   1. 托管前端静态文件 (public/)
 *   2. /api/proxy 转发请求到拉卡拉接口 (绕过浏览器 CORS)
 *
 * 启动: node server.js   或   PORT=80 node server.js
 */
const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');
const { URL } = require('url');

const PORT = process.env.PORT || 3000;
const PUBLIC_DIR = path.join(__dirname, 'public');

// 允许转发到的目标域名白名单 (防止把代理当成开放代理被滥用)
const ALLOWED_TARGETS = [
  'https://test.wsmsd.cn',
  'https://s2.lakala.com',
  'https://api.lakala.com'
];

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js':   'application/javascript; charset=utf-8',
  '.css':  'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg':  'image/svg+xml',
  '.ico':  'image/x-icon'
};

function serveStatic(req, res) {
  let urlPath = req.url.split('?')[0];
  if (urlPath === '/') urlPath = '/index.html';

  // 防穿越
  const safePath = path.normalize(urlPath).replace(/^(\.\.[\/\\])+/, '');
  const fullPath = path.join(PUBLIC_DIR, safePath);
  if (!fullPath.startsWith(PUBLIC_DIR)) {
    res.writeHead(403); return res.end('Forbidden');
  }

  fs.readFile(fullPath, (err, data) => {
    if (err) { res.writeHead(404); return res.end('Not found: ' + urlPath); }
    const ext = path.extname(fullPath).toLowerCase();
    res.writeHead(200, {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Cache-Control': 'no-cache'
    });
    res.end(data);
  });
}

function handleProxy(req, res) {
  // CORS 预检
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'POST,OPTIONS',
      'Access-Control-Allow-Headers': '*',
      'Access-Control-Max-Age': '86400'
    });
    return res.end();
  }
  if (req.method !== 'POST') { res.writeHead(405); return res.end('Method not allowed'); }

  // 目标 URL 通过 X-Target-Url 头传递
  const targetUrl = req.headers['x-target-url'];
  if (!targetUrl) { res.writeHead(400); return res.end('Missing X-Target-Url header'); }

  // 安全检查: 只允许转发到白名单
  let parsed;
  try { parsed = new URL(targetUrl); } catch (e) {
    res.writeHead(400); return res.end('Invalid X-Target-Url');
  }
  const origin = parsed.origin;
  if (!ALLOWED_TARGETS.includes(origin)) {
    res.writeHead(403);
    return res.end(`Target not allowed: ${origin}\nAllowed: ${ALLOWED_TARGETS.join(', ')}`);
  }

  // 收集请求体
  const chunks = [];
  req.on('data', c => chunks.push(c));
  req.on('end', () => {
    const body = Buffer.concat(chunks);

    const upstreamReq = https.request({
      hostname: parsed.hostname,
      port: parsed.port || 443,
      path: parsed.pathname + parsed.search,
      method: 'POST',
      headers: {
        'Content-Type': req.headers['content-type'] || 'application/json',
        'Accept': 'application/json',
        'Authorization': req.headers['authorization'] || '',
        'Content-Length': body.length
      },
      timeout: 30000
    }, (upRes) => {
      const respChunks = [];
      upRes.on('data', c => respChunks.push(c));
      upRes.on('end', () => {
        const respBody = Buffer.concat(respChunks);
        // 透传所有响应头 (包括 Lklapi-* 验签头), 加上 CORS
        const outHeaders = { ...upRes.headers };
        outHeaders['access-control-allow-origin'] = '*';
        outHeaders['access-control-expose-headers'] = '*';
        // 删掉 transfer-encoding 防止冲突
        delete outHeaders['transfer-encoding'];
        delete outHeaders['content-encoding'];
        outHeaders['content-length'] = respBody.length;
        res.writeHead(upRes.statusCode || 502, outHeaders);
        res.end(respBody);
      });
    });

    upstreamReq.on('error', e => {
      res.writeHead(502, {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*'
      });
      res.end(JSON.stringify({ proxy_error: true, message: e.message, code: e.code }));
    });
    upstreamReq.on('timeout', () => {
      upstreamReq.destroy(new Error('Upstream timeout (30s)'));
    });

    upstreamReq.write(body);
    upstreamReq.end();
  });
}

const server = http.createServer((req, res) => {
  // 简单访问日志 (不记录 body 和敏感头)
  console.log(`[${new Date().toISOString()}] ${req.method} ${req.url}`);

  if (req.url.startsWith('/api/proxy')) {
    return handleProxy(req, res);
  }
  if (req.url === '/api/health') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    return res.end(JSON.stringify({ ok: true, time: new Date().toISOString() }));
  }
  serveStatic(req, res);
});

// 端口被占用时等用户读完信息再退出 (避免 exe 闪退看不到错误)
function pauseAndExit(code) {
  if (!process.pkg) { process.exit(code); return; }   // 非 exe 模式直接退
  console.log('\n按回车键退出...');
  process.stdin.resume();
  process.stdin.once('data', () => process.exit(code));
}

// 监听 3000 占用就自动试下一个 (最多 3000~3010)
function tryListen(port, maxTries) {
  if (maxTries <= 0) {
    console.error(`\n[错误] 端口 3000-${port-1} 全部被占用, 无法启动服务。`);
    console.error('       请关闭其他占用端口的程序后重试 (常见: 之前启动的 node server.js 或 PM2 守护进程)。');
    pauseAndExit(1);
    return;
  }
  server.once('error', (err) => {
    if (err.code === 'EADDRINUSE') {
      console.log(`  端口 ${port} 已被占用, 尝试 ${port+1}...`);
      tryListen(port + 1, maxTries - 1);
    } else {
      console.error(`\n[错误] 启动失败: ${err.message}`);
      pauseAndExit(1);
    }
  });
  server.listen(port, () => {
    const url = `http://localhost:${port}`;
    console.log('\n========================================');
    console.log('  拉卡拉接口调试工具');
    console.log('========================================');
    console.log(`  访问地址: ${url}`);
    console.log(`  关闭此窗口即可停止服务`);
    console.log(`  代理白名单: ${ALLOWED_TARGETS.join(', ')}`);
    console.log('========================================\n');

    // 打包成 exe 后 (process.pkg 为真), 启动时自动用默认浏览器打开页面
    if (process.pkg) {
      const cmd = process.platform === 'win32' ? `start "" "${url}"`
               : process.platform === 'darwin' ? `open "${url}"`
               : `xdg-open "${url}"`;
      setTimeout(() => {
        exec(cmd, (err) => {
          if (err) console.log(`  (未能自动打开浏览器, 请手动访问 ${url})`);
        });
      }, 500);
    }
  });
}

// 兜底: 未捕获异常时给用户看错误再退
process.on('uncaughtException', (e) => {
  console.error('\n[未处理的异常]', e.message);
  console.error(e.stack);
  pauseAndExit(1);
});

tryListen(Number(PORT), 11);   // 默认 3000, 失败试到 3010
