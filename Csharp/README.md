# 拉卡拉接口调试 C# DEMO

最小可运行的 C# 版本, 演示拉卡拉 V3 接口的:

- **SHA256withRSA 加签** — 拼装 Authorization header
- **同步响应验签** (5 行报文) + **异步通知验签** (3 行报文)
- **SM4/ECB/PKCS5 加解密** (用于整报文 / 字段加密接口, 如商户进件)
- 完整的 HttpClient 调用流程

## 运行

需要 .NET 6 / 7 / 8 / 9 (Windows / Linux / macOS 都行):

```bash
cd Csharp
dotnet run
```

首次运行会自动下载 BouncyCastle NuGet 包.

预期输出:

```
======================================================
  拉卡拉接口调试 C# DEMO
======================================================

【演示 1】SM4/ECB/PKCS5 加解密 round-trip
  原文:  {"hello":"world","中文":"测试"}
  密文:  E0vGz...
  解密:  {"hello":"world","中文":"测试"}
  一致:  ✓

【演示 2】SHA256withRSA 加签
  URL:           https://test.wsmsd.cn/sit/api/v3/labs/query/tradequery
  Body:          {"version":"3.0",...}
  Authorization: LKLAPI-SHA256withRSA appid="OP00000003",serial_no="00dfba8194c41b84cf",timestamp="...",...

【演示 3】完整调用 — 聚合交易查询 (/api/v3/labs/query/tradequery)
  HTTP 状态:  200
  Traceid:    abc123...
  验签:       ✓ 通过
  响应 Body:  {"code":"...","msg":"...","resp_data":...}
```

## 文件结构

| 文件 | 作用 |
|---|---|
| `LakalaDemo.csproj` | .NET 8 项目文件 (依赖只有 BouncyCastle.Cryptography) |
| `LakalaOpenOptions.cs` | 配置类 — 商户号 / 私钥 / 公钥证书 / SM4 Key (测试环境已预填) |
| `LakalaHelper.cs` | **加签 / 验签核心** (`CreateSignedHttpRequest`, `VerifyResponseSignature`, `VerifyAsyncNotifySignature`) |
| `Sm4Util.cs` | SM4/ECB/PKCS5 加解密 (基于 BouncyCastle) |
| `LakalaOpenPoster.cs` | HttpClient 封装 — 一次调用自动加签 + 发送 + 验签 |
| `Program.cs` | 演示入口, 跑三个 demo |

## 整合到自己项目时的注意点

跟之前 demo 版本相比, **5 处关键修正**:

1. **PEM 私钥加载用 `RSA.Create() + ImportFromPem()`**, 不用 `RSACryptoServiceProvider`
   - `RSACryptoServiceProvider` 是 Windows-only, Linux / macOS / Docker 容器跑会挂
   - `RSA.Create()` 自动选当前平台的实现, 真正跨平台
   - `ImportFromPem` (Span 重载) 同时支持 PKCS#1 (`-----BEGIN RSA PRIVATE KEY-----`) 和 PKCS#8 (`-----BEGIN PRIVATE KEY-----`) 两种 PEM

2. **Authorization header 用 `TryAddWithoutValidation`, 不用 `new AuthenticationHeaderValue(...)`**
   - `AuthenticationHeaderValue` 严格按 RFC 7235 校验, 拉卡拉的 `LKLAPI-SHA256withRSA appid="...",...` 格式有时被重写或拒绝
   - `TryAddWithoutValidation` 让你完全控制 header 字节, 避免 .NET 偷偷改写

3. **私钥 / 公钥实例字段, 不是 static**
   - 之前是 `private static RSA _priRsa`, 多套 LakalaOpenOptions 实例会共享第一个加载的 RSA, 严重 bug
   - 现在是 instance field, 每个 Options 各自独立

4. **不依赖 ABP 框架** (原 `LakalaOpenPoster` 用了 `ISingletonDependency`, `IOptions<>`, `JsonUtils.CamelCaseIgnorNullOption`, 自定义异常类等)
   - 现在的 `LakalaOpenPoster` 只用标准 `System.Net.Http`, 任何 .NET 项目直接抄走

5. **加 SM4 工具** — `Sm4Util.cs` (BouncyCastle, 拉卡拉商户进件等接口必须)

## 修改为自己的参数

编辑 `LakalaOpenOptions.cs`:

```csharp
public string AppId    { get; set; } = "您的 AppId";
public string SerialNo { get; set; } = "您的证书序列号";
public string Sm4Key   { get; set; } = "您的 SM4 Key";

public string PriPem { get; set; } = @"-----BEGIN PRIVATE KEY-----
您的私钥内容
-----END PRIVATE KEY-----";

public string PubCert { get; set; } = @"-----BEGIN CERTIFICATE-----
拉卡拉发给您的公钥证书 (生产环境必须替换)
-----END CERTIFICATE-----";

public bool UseTestEnv { get; set; } = false;  // 生产环境改为 false
```

## 私钥不是 .pem 怎么办

拉卡拉发给您的可能是 `.pfx` / `.p12` (PKCS#12 格式), 转 PEM:

```bash
# 1. 从 .pfx 提取私钥 (加密)
openssl pkcs12 -in your.pfx -nocerts -nodes -out tmp.pem

# 2. 转成 PKCS#8 格式 (.NET RSA.ImportFromPem 友好)
openssl pkcs8 -topk8 -nocrypt -in tmp.pem -out private.pem

# 把 private.pem 内容复制到 LakalaOpenOptions.PriPem
```

## 调试技巧

- 如果验签失败, 优先检查 body 是不是 **原始字节** (不要反序列化)
- `req_time` / `timestamp` 跟服务器时间偏差超过 5 分钟会被拒
- 整体 SM4 加密接口: 注意签名是对 **加密后的密文** 做的, 不是对明文
- 拉卡拉测试网关 `test.wsmsd.cn` 限制 **境外 IP**, 海外服务器调用会被拒
- 出问题时, **打印 Lklapi-Traceid**, 给拉卡拉客服报这个 ID 能快速定位

## 完整工具

如果需要图形界面调试 (含 18 个接口模板, 在线签名 / 验签 / SM4):

- 在线: https://lakala-debug.shibofa.com (网页版, 受境外 IP 限制)
- 本地版下载: https://github.com/Bonbonwu1119/lakala_debugger/releases/latest
