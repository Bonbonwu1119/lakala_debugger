// 从 public/schemas/*.json 自动生成 INLINE_SCHEMAS 内嵌字典, 替换 index.html 里的区块
// 这样以后只要改 schemas/*.json, 跑一次脚本就同步.
const fs = require('fs');
const path = require('path');

const SCHEMA_DIR = path.join(__dirname, 'public', 'schemas');
const INDEX_HTML = path.join(__dirname, 'public', 'index.html');

// 读 index.json 拿到分类配置
const idx = JSON.parse(fs.readFileSync(path.join(SCHEMA_DIR, 'index.json'), 'utf8'));

// 收集所有 schema id (展开 groups)
const allIds = [];
for (const cat of idx.categories) {
  if (cat.groups && cat.groups.length) {
    for (const g of cat.groups) for (const id of (g.interfaces || [])) allIds.push(id);
  } else {
    for (const id of (cat.interfaces || [])) allIds.push(id);
  }
}

// 读每个 schema 文件
const schemas = {};
for (const id of allIds) {
  const fp = path.join(SCHEMA_DIR, id + '.json');
  if (!fs.existsSync(fp)) {
    console.warn(`⚠ 缺少 ${fp}`);
    continue;
  }
  schemas[id] = JSON.parse(fs.readFileSync(fp, 'utf8'));
}

// 生成 JS 字面量
const inlineIndexJs = 'const INLINE_SCHEMAS_INDEX = ' + JSON.stringify(idx, null, 2) + ';';
const inlineSchemasJs = 'const INLINE_SCHEMAS = ' + JSON.stringify(schemas, null, 2) + ';';

// 读 index.html, 找到两个区块替换
let html = fs.readFileSync(INDEX_HTML, 'utf8');

// 替换 INLINE_SCHEMAS_INDEX 块: 从 'const INLINE_SCHEMAS_INDEX' 到下一个 ';' 之后的空白行
const idxStart = html.indexOf('const INLINE_SCHEMAS_INDEX');
if (idxStart < 0) { console.error('找不到 INLINE_SCHEMAS_INDEX'); process.exit(1); }
// 找闭合 } + ;
let depth = 0, idxEnd = -1, inObj = false;
for (let i = idxStart; i < html.length; i++) {
  const c = html[i];
  if (c === '{') { depth++; inObj = true; }
  else if (c === '}') { depth--; if (depth === 0 && inObj) { idxEnd = i+2; break; } }   // }; 闭合
}
if (idxEnd < 0) { console.error('INLINE_SCHEMAS_INDEX 找不到闭合 ;'); process.exit(1); }

// 同理 INLINE_SCHEMAS 块
const schStart = html.indexOf('const INLINE_SCHEMAS =', idxEnd);
if (schStart < 0) { console.error('找不到 INLINE_SCHEMAS ='); process.exit(1); }
depth = 0; let schEnd = -1; inObj = false;
for (let i = schStart; i < html.length; i++) {
  const c = html[i];
  if (c === '{') { depth++; inObj = true; }
  else if (c === '}') { depth--; if (depth === 0 && inObj) { schEnd = i+2; break; } }
}
if (schEnd < 0) { console.error('INLINE_SCHEMAS 找不到闭合 ;'); process.exit(1); }

const before = html.slice(0, idxStart);
const between = html.slice(idxEnd, schStart);
const after = html.slice(schEnd);

const out = before + inlineIndexJs + between + inlineSchemasJs + after;
fs.writeFileSync(INDEX_HTML, out, 'utf8');

console.log('✅ 替换完成');
console.log(`  - INLINE_SCHEMAS_INDEX: ${idxStart}..${idxEnd}`);
console.log(`  - INLINE_SCHEMAS:       ${schStart}..${schEnd}`);
console.log(`  - 总 schema 数: ${Object.keys(schemas).length}`);
console.log(`  - schema ids: ${Object.keys(schemas).join(', ')}`);
