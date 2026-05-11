# 拉卡拉调试工具

给客户用的拉卡拉接口调试网页。三种模式：

- **发送请求** — 客户填参数 → 自动签名 → 通过本服务转发到拉卡拉 → 显示响应 + 验签结果
- **诊断签名** — 客户把自己代码生成的签名输入进来，工具用同样的输入再签一次，告诉客户哪里错了
- **验证响应** — 客户粘贴拉卡拉返回的响应头和响应体，工具用拉卡拉公钥证书验签

私钥仅在客户浏览器内使用，**不会上传到服务器**。

---

## 一、本地预览

需要 Node.js >= 14。

```bash
node server.js
# 浏览器打开 http://localhost:3000
```

无需 `npm install` —— 这个项目零依赖，只用 Node 内置模块。

---

## 二、部署到服务器（生产环境）

假设是一台 Linux 服务器（Ubuntu / CentOS 都行），有公网 IP，已绑定域名（比如 `debug.example.com`）。

### 2.1 上传代码

把整个 `lakala-debugger/` 目录传到服务器。任意目录都行，下面以 `/opt/lakala-debugger` 为例：

```bash
scp -r lakala-debugger/ user@your-server:/opt/
```

### 2.2 安装 Node.js

```bash
# Ubuntu / Debian
curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
sudo apt install -y nodejs

# CentOS / RHEL
curl -fsSL https://rpm.nodesource.com/setup_lts.x | sudo bash -
sudo yum install -y nodejs

# 验证
node -v   # 应输出 v18.x 或更高
```

### 2.3 启动服务

直接启动（前台运行，关闭终端就停）：

```bash
cd /opt/lakala-debugger
node server.js
```

**生产环境推荐用 PM2**（让进程后台跑、开机自启、崩了自动重启）：

```bash
sudo npm install -g pm2
cd /opt/lakala-debugger
pm2 start server.js --name lakala-debugger
pm2 save
pm2 startup    # 跟着提示走，让 PM2 开机自启
```

之后服务就会一直在后台跑，访问 `http://服务器IP:3000` 就能用。

### 2.4 配置 Nginx + HTTPS（让客户访问域名）

直接暴露 3000 端口给客户也能用，但生产环境通常用 Nginx 反代 + HTTPS 证书。

安装 Nginx：

```bash
sudo apt install -y nginx     # Ubuntu
# sudo yum install -y nginx   # CentOS
```

新建配置文件 `/etc/nginx/conf.d/lakala-debugger.conf`：

```nginx
server {
    listen 80;
    server_name debug.example.com;   # 改成你的域名

    location / {
        proxy_pass http://127.0.0.1:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_read_timeout 60s;
    }
}
```

重启 Nginx：

```bash
sudo nginx -t && sudo systemctl reload nginx
```

加 HTTPS 证书（Let's Encrypt 免费）：

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d debug.example.com
```

完成后客户访问 `https://debug.example.com` 即可。

---

## 三、增加新接口

每个接口对应一个 schema JSON 文件，放在 `public/schemas/` 目录下。

### 3.1 创建 schema 文件

复制 `public/schemas/trans_preorder.json` 改名为新接口的 ID，比如 `trans_query.json`：

```json
{
  "id": "trans_query",
  "category": "收单",
  "name": "交易查询",
  "description": "根据订单号查询交易状态",
  "method": "POST",
  "url_test": "https://test.wsmsd.cn/sit/api/v3/labs/trans/query",
  "url_prod": "https://s2.lakala.com/api/v3/labs/trans/query",
  "fields": [
    { "path": "version",                  "required": true,  "desc": "接口版本号 3.0" },
    { "path": "req_data.merchant_no",     "required": true,  "desc": "商户号" },
    { "path": "req_data.out_trade_no",    "required": false, "desc": "商户订单号 (与 trade_no 二选一)" },
    { "path": "req_data.trade_no",        "required": false, "desc": "拉卡拉订单号 (与 out_trade_no 二选一)" }
  ],
  "example": {
    "req_time": "{{req_time}}",
    "version": "3.0",
    "req_data": {
      "merchant_no": "82229007392000A",
      "out_trade_no": "{{out_trade_no}}"
    }
  }
}
```

可用占位符：
- `{{req_time}}` — 自动替换为当前时间 yyyyMMddHHmmss
- `{{out_trade_no}}` — 自动替换为新生成的订单号
- `{{timestamp}}` — 当前秒级时间戳

### 3.2 注册到接口清单

编辑 `public/schemas/index.json`，把新接口加到对应分类下：

```json
{
  "categories": [
    {
      "name": "收单",
      "interfaces": ["trans_preorder", "trans_query"]
    },
    {
      "name": "退款",
      "interfaces": ["refund_apply", "refund_query"]
    }
  ]
}
```

### 3.3 立即生效

无需重启服务，刷新浏览器即可。schema 文件是前端动态加载的。

---

## 四、配置项

`server.js` 顶部可以改：

| 配置 | 说明 | 默认值 |
|---|---|---|
| `PORT` | 监听端口 | `3000` (可用环境变量 `PORT=80 node server.js` 覆盖) |
| `ALLOWED_TARGETS` | 代理转发的目标白名单 | `test.wsmsd.cn`, `s2.lakala.com`, `api.lakala.com` |

如果以后要支持新的拉卡拉域名（比如对接其他系统），把对应的 origin 加到 `ALLOWED_TARGETS` 里。

---

## 五、运维

### 查看日志

PM2 方式：
```bash
pm2 logs lakala-debugger        # 实时日志
pm2 logs lakala-debugger --lines 500   # 最近 500 行
```

直接运行的话日志在 stdout，建议重定向：
```bash
node server.js > /var/log/lakala-debugger.log 2>&1 &
```

### 重启
```bash
pm2 restart lakala-debugger
```

### 健康检查

`GET /api/health` 返回 `{"ok": true, "time": "..."}`，可以接入监控。

---

## 六、给客户的使用说明

可以发给客户的简单说明（直接复制）：

> **拉卡拉接口调试工具**
>
> 访问地址：`https://debug.example.com`
>
> **如果您要调试自己的请求：**
> - 进入"发送请求"模式，选择接口，填入您的 App ID / 私钥 / 请求参数，点击发送即可看到拉卡拉响应。
>
> **如果您自己代码签名跟拉卡拉对不上：**
> - 进入"诊断签名"模式，把您程序里实际用的 timestamp、nonce、body、私钥、签名值贴进来，工具会告诉您哪里错了。
>
> **如果您想验证收到的响应是不是真的来自拉卡拉：**
> - 进入"验证响应"模式，粘贴响应头和响应体即可。
>
> 工具完全在您的浏览器内运行，私钥不会上传到任何服务器。

---

## 七、常见问题

**Q: 客户的请求转发失败，报 502？**
A: 大概率是服务器到拉卡拉网络不通。SSH 到服务器跑 `curl -v https://test.wsmsd.cn/sit/api/v3/labs/trans/preorder`，看是否能连通。

**Q: 浏览器访问报跨域错误？**
A: `server.js` 已经加了 CORS 头，正常不会出现。如果确实出现，检查 Nginx 配置是否覆盖了 `Access-Control-*` 头。

**Q: 客户怎么验证响应？我们没把生产环境公钥证书内嵌进去。**
A: 当前内嵌的是测试环境证书。生产证书可以在"验证响应"模式里手动粘贴覆盖；以后要默认支持，可以扩展 `LKL_PUB_CERT_TEST` 加一个 `LKL_PUB_CERT_PROD`，根据环境选择。

**Q: 客户上传的私钥真的不会上传到服务器吗？**
A: 真的。所有签名/验签都在浏览器内由 jsrsasign 完成，server.js 只负责转发已签好名的 HTTP 请求。可以让客户打开浏览器开发者工具 → Network 面板，发送请求时观察实际网络请求，确认私钥没出现在任何请求里。
