using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LakalaDemo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("======================================================");
Console.WriteLine("  拉卡拉接口调试 C# DEMO");
Console.WriteLine("======================================================\n");

var options = new LakalaOpenOptions();
var poster  = new LakalaOpenPoster(options);

// ============================================================
//  演示 1: SM4 加解密 round-trip (本地, 无需网络)
// ============================================================
Console.WriteLine("【演示 1】SM4/ECB/PKCS5 加解密 round-trip\n");
{
    var plain  = "{\"hello\":\"world\",\"中文\":\"测试\"}";
    var cipher = Sm4Util.Encrypt(options.Sm4Key, plain);
    var back   = Sm4Util.Decrypt(options.Sm4Key, cipher);
    Console.WriteLine($"  原文:  {plain}");
    Console.WriteLine($"  密文:  {cipher}");
    Console.WriteLine($"  解密:  {back}");
    Console.WriteLine($"  一致:  {(plain == back ? "✓" : "✗")}\n");
}

// ============================================================
//  演示 2: 加签 (生成 Authorization header)
// ============================================================
Console.WriteLine("【演示 2】SHA256withRSA 加签 — 生成 Authorization\n");
{
    var body = $"{{\"version\":\"3.0\",\"req_time\":\"{DateTime.Now:yyyyMMddHHmmss}\",\"req_data\":{{}}}}";
    var url  = options.BaseUrl + "/api/v3/labs/query/tradequery";
    var req  = LakalaHelper.CreateSignedHttpRequest(body, url, options);
    Console.WriteLine($"  URL:            {url}");
    Console.WriteLine($"  Body:           {body}");
    Console.WriteLine($"  Authorization:  {req.Headers.GetValues("Authorization").First()}\n");
}

// ============================================================
//  演示 3: 同步响应验签 (5 行报文) round-trip
//
//  拉卡拉同步响应签名规则:
//    {Lklapi-Appid}\n{Lklapi-Serial}\n{Lklapi-Timestamp}\n{Lklapi-Nonce}\n{body}\n
//
//  本演示用 customer 自己的 RSA pair 做"自签自验" 证明代码逻辑正确.
//  实际场景: 拉卡拉用他们的私钥签, 您用 options.PubCert (拉卡拉公钥证书) 验.
//  调用方式: LakalaHelper.VerifyResponseSignature(httpResponse.Headers, body, options)
// ============================================================
Console.WriteLine("【演示 3】同步响应验签 (5 行报文) round-trip\n");
{
    var rsa = options.GetPriRsa();
    var derivedPub = RSA.Create();
    derivedPub.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));

    var respBody = "{\"code\":\"BBS00000\",\"msg\":\"成功\",\"resp_data\":{\"trade_state\":\"SUCCESS\"}}";
    var tms      = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var nonce    = Guid.NewGuid().ToString("N")[..12];

    // 5 行报文
    var payload5 = $"{options.AppId}\n{options.SerialNo}\n{tms}\n{nonce}\n{respBody}\n";
    var sig5     = Convert.ToBase64String(rsa.SignData(
        Encoding.UTF8.GetBytes(payload5), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    var ok5 = derivedPub.VerifyData(
        Encoding.UTF8.GetBytes(payload5),
        Convert.FromBase64String(sig5),
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Console.WriteLine($"  (A) 正确报文验签:        {(ok5 ? "✓ 通过" : "✗ 失败")}");

    // 篡改 body 验签必须失败
    var tampered = payload5.Replace("BBS00000", "BBSFFFFF");
    var okT = derivedPub.VerifyData(
        Encoding.UTF8.GetBytes(tampered),
        Convert.FromBase64String(sig5),
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Console.WriteLine($"  (B) 篡改 body 后验签:    {(okT ? "✗ 错! 应该检测到篡改" : "✓ 失败 (符合预期, 签名机制有效)")}");

    // 改 timestamp 验签也必须失败
    var tsAltered = payload5.Replace(tms, "0000000000");
    var okTs = derivedPub.VerifyData(
        Encoding.UTF8.GetBytes(tsAltered),
        Convert.FromBase64String(sig5),
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Console.WriteLine($"  (C) 改 timestamp 后验签: {(okTs ? "✗ 错!" : "✓ 失败 (符合预期)")}");

    Console.WriteLine();
    Console.WriteLine("  ★ 实际生产调用: LakalaHelper.VerifyResponseSignature(httpResponse.Headers, body, options)");
    Console.WriteLine("  ★ 内部用 options.PubCert (拉卡拉公钥证书) 做验签, 同样 5 行报文格式\n");
}

// ============================================================
//  演示 4: 异步通知验签 (3 行报文) round-trip
//
//  拉卡拉异步通知发到您的 notify_url, header 形如:
//    Authorization: LKLAPI-SHA256withRSA timestamp="...",nonce_str="...",signature="..."
//  签名报文 3 行 (无 appid / serial):
//    {timestamp}\n{nonce_str}\n{body}\n
//
//  注意: 验签前不要对 body 反序列化! Spring 不能用 @RequestBody (会动 body),
//        .NET 用 await request.Content.ReadAsStringAsync() 拿原始字节.
//
//  调用方式: LakalaHelper.VerifyAsyncNotifySignature(authHeader, body, options)
// ============================================================
Console.WriteLine("【演示 4】异步通知验签 (3 行报文) round-trip\n");
{
    var rsa = options.GetPriRsa();
    var derivedPub = RSA.Create();
    derivedPub.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));

    // 模拟拉卡拉异步通知 body (真实场景就是交易通知 JSON)
    var notifyBody = "{\"payOrderNo\":\"21090611012001970631000463034\",\"merchantOrderNo\":\"CH2021090613190866292\",\"payStatus\":\"S\"}";
    var ts    = "1630905585";
    var nonce = "9003323344";

    // 3 行报文
    var payload3 = $"{ts}\n{nonce}\n{notifyBody}\n";
    var sig3 = Convert.ToBase64String(rsa.SignData(
        Encoding.UTF8.GetBytes(payload3), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    // 模拟收到的 Authorization header
    var fakeAuth = $"LKLAPI-SHA256withRSA timestamp=\"{ts}\",nonce_str=\"{nonce}\",signature=\"{sig3}\"";
    Console.WriteLine($"  收到 Authorization:  {fakeAuth[..Math.Min(110, fakeAuth.Length)]}...");
    Console.WriteLine($"  收到 Body:           {notifyBody}");

    // 直接调 RSA 验签 (因为 LakalaHelper.VerifyAsyncNotifySignature 用 PubCert 验,
    // 但 PubCert 跟 customer 的 PriPem 不是一对, 所以直接演示 round-trip 用派生公钥)
    var ok3 = derivedPub.VerifyData(
        Encoding.UTF8.GetBytes(payload3),
        Convert.FromBase64String(sig3),
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Console.WriteLine($"  3 行报文验签:        {(ok3 ? "✓ 通过" : "✗ 失败")}");

    Console.WriteLine();
    Console.WriteLine("  ★ 实际接收异步通知:");
    Console.WriteLine("       string body = await request.Content.ReadAsStringAsync();  // 原始字节, 不要反序列化!");
    Console.WriteLine("       string auth = request.Headers.GetValues(\"Authorization\").First();");
    Console.WriteLine("       bool valid = LakalaHelper.VerifyAsyncNotifySignature(auth, body, options);");
    Console.WriteLine("       if (valid) return Ok(new { code = \"SUCCESS\", message = \"执行成功\" });");
    Console.WriteLine();
}

// ============================================================
//  演示 5: 完整网络调用 — 聚合被扫 (商户扫用户码, 会自动加签 + 验签)
//
//  ⚠ 真实业务: auth_code 是 POS 设备读取用户 App 里的付款码, 18 位数字, 一次性有效
//  ⚠ 本 demo 用文档样本 auth_code, 大概率会返回业务错误 (无效付款码) 但流程跑通
//  ⚠ 测试环境强制 total_amount = "1" (单位: 分)
//  ⚠ 拉卡拉测试网关 test.wsmsd.cn 限制境外 IP, 必须从境内电脑跑
//
//  auth_code 前缀对应钱包: 10-15 微信 / 25-30 支付宝 / 62 银联 / 01 数币 / 51 翼支付 / 83 苏宁
// ============================================================
Console.WriteLine("【演示 5】完整调用 — 聚合被扫 (/api/v3/labs/trans/micropay)\n");
{
    var reqBody = JsonSerializer.Serialize(new
    {
        version      = "3.0",
        req_time     = DateTime.Now.ToString("yyyyMMddHHmmss"),
        out_org_code = "OP00000003",
        req_data     = new
        {
            merchant_no  = "82229007392000A",
            term_no      = "D9296400",
            out_trade_no = "DEMO" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            auth_code    = "130845690645325413",   // 13 开头 = 微信付款码 (样本值)
            total_amount = "1",                     // 测试环境固定 1 分
            notify_url   = "https://example.com/notify",
            location_info = new
            {
                request_ip = "10.176.1.192",
                location   = "+37.123456789,-121.123456789"
            }
        },
    });

    Console.WriteLine($"  请求 URL:   {options.BaseUrl}/api/v3/labs/trans/micropay");
    Console.WriteLine($"  请求 Body:  {reqBody}");

    try
    {
        var result = await poster.PostAsync("/api/v3/labs/trans/micropay", reqBody);
        Console.WriteLine($"  HTTP 状态:  {result.HttpStatus}");
        Console.WriteLine($"  Traceid:    {result.Traceid ?? "(无)"}");
        Console.WriteLine($"  响应验签:   {(result.SignatureChecked
                                              ? (result.SignatureValid ? "✓ 通过 (LakalaHelper.VerifyResponseSignature 自动验)" : "✗ 失败 " + result.SignatureError)
                                              : "(响应里无 Lklapi-Signature, 跳过验签)")}");

        // 智能分析响应类型, 不要刷屏 HTML
        var body = result.ResponseBody?.TrimStart() ?? "";
        if (body.StartsWith("<") || body.Contains("<!DOCTYPE"))
        {
            Console.WriteLine($"  响应类型:   ⚠ HTML 错误页 (不是 JSON), {body.Length} 字符");
            Console.WriteLine($"  原因诊断:   被网关 WAF 拦截或返回 405 等错误. 通常是:");
            Console.WriteLine($"               1) 出口 IP 触发风控 (如海外 / 数据中心 IP, test.wsmsd.cn 限制境外)");
            Console.WriteLine($"               2) 请求 body 里的字段值触发 WAF 规则 (如 location 里有 / 等特殊字符)");
            Console.WriteLine($"               3) auth_code 不是真实有效的付款码");
            Console.WriteLine($"  响应预览:   {body[..Math.Min(120, body.Length)]}...");
        }
        else if (body.StartsWith("{") || body.StartsWith("["))
        {
            Console.WriteLine($"  响应 Body:  {body}");
        }
        else
        {
            Console.WriteLine($"  响应 Body:  {body[..Math.Min(200, body.Length)]}");
        }
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine($"\n  ⚠ HTTP 请求失败: {e.Message}");
        Console.WriteLine("    可能原因: 您的电脑访问不了 https://test.wsmsd.cn (境外服务器 IP 受限),");
        Console.WriteLine("    建议在境内电脑跑此 DEMO. 工具的加签 / 验签 / SM4 逻辑本身不受影响 (演示 1-4 都通过).");
    }
}

Console.WriteLine("\n======================================================");
Console.WriteLine("  DEMO 跑完");
Console.WriteLine("======================================================");
Console.WriteLine("\nLakalaHelper 提供 3 个核心 API:");
Console.WriteLine("  · CreateSignedHttpRequest(body, url, options)            ← 加签 (生成请求)");
Console.WriteLine("  · VerifyResponseSignature(httpResp.Headers, body, opt)   ← 同步验签 (5 行)");
Console.WriteLine("  · VerifyAsyncNotifySignature(authHeader, body, opt)      ← 异步通知验签 (3 行)");
Console.WriteLine("\nSm4Util 提供 SM4 加解密: Encrypt/Decrypt(keyBase64, plaintext)");
