using System.Text.Json;
using LakalaDemo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("======================================================");
Console.WriteLine("  拉卡拉接口调试 C# DEMO");
Console.WriteLine("======================================================\n");

var options = new LakalaOpenOptions();
var poster  = new LakalaOpenPoster(options);

// ----------------------------------------------------------
// 演示 1: SM4 加解密
// ----------------------------------------------------------
Console.WriteLine("【演示 1】SM4/ECB/PKCS5 加解密 round-trip\n");
{
    var plain = "{\"hello\":\"world\",\"中文\":\"测试\"}";
    var cipher = Sm4Util.Encrypt(options.Sm4Key, plain);
    var back   = Sm4Util.Decrypt(options.Sm4Key, cipher);
    Console.WriteLine($"  原文:  {plain}");
    Console.WriteLine($"  密文:  {cipher}");
    Console.WriteLine($"  解密:  {back}");
    Console.WriteLine($"  一致:  {(plain == back ? "✓" : "✗")}\n");
}

// ----------------------------------------------------------
// 演示 2: 加签 (生成 Authorization header)
// ----------------------------------------------------------
Console.WriteLine("【演示 2】SHA256withRSA 加签\n");
{
    var body = "{\"version\":\"3.0\",\"req_time\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"req_data\":{}}";
    var url  = options.BaseUrl + "/api/v3/labs/query/tradequery";
    var req  = LakalaHelper.CreateSignedHttpRequest(body, url, options);
    Console.WriteLine($"  URL:           {url}");
    Console.WriteLine($"  Body:          {body}");
    Console.WriteLine($"  Authorization: {req.Headers.GetValues("Authorization").First()}\n");
}

// ----------------------------------------------------------
// 演示 3: 完整调用 (聚合交易查询接口, 测试环境)
// ----------------------------------------------------------
Console.WriteLine("【演示 3】完整调用 — 聚合交易查询 (/api/v3/labs/query/tradequery)\n");
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
        },
    });

    Console.WriteLine($"  请求 URL:   {options.BaseUrl}/api/v3/labs/query/tradequery");
    Console.WriteLine($"  请求 Body:  {reqBody}");

    try
    {
        var result = await poster.PostAsync("/api/v3/labs/query/tradequery", reqBody);
        Console.WriteLine($"  HTTP 状态:  {result.HttpStatus}");
        Console.WriteLine($"  Traceid:    {result.Traceid ?? "(无)"}");
        Console.WriteLine($"  验签:       {(result.SignatureChecked ? (result.SignatureValid ? "✓ 通过" : "✗ 失败 " + result.SignatureError) : "(响应无 Lklapi-Signature)")}");
        Console.WriteLine($"  响应 Body:  {result.ResponseBody}");
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine($"\n  ⚠ HTTP 请求失败: {e.Message}");
        Console.WriteLine("    可能原因: 您的电脑不能直接访问 https://test.wsmsd.cn (境外 IP 受限),");
        Console.WriteLine("    建议在境内电脑跑此 DEMO. 工具签名 / 加密逻辑本身不受影响.");
    }
}

Console.WriteLine("\n======================================================");
Console.WriteLine("  DEMO 跑完");
Console.WriteLine("======================================================");
