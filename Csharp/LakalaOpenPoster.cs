using System.Net.Http;
using System.Net.Http.Headers;

namespace LakalaDemo;

/// <summary>
/// 拉卡拉接口调用客户端 (独立版, 不依赖 ABP 框架).
///
/// 用法:
///   var opt = new LakalaOpenOptions();
///   var client = new LakalaOpenPoster(opt);
///   var result = await client.PostAsync("/api/v3/labs/query/tradequery", jsonBody);
///   if (result.IsSuccess) Console.WriteLine(result.Body);
/// </summary>
public class LakalaOpenPoster
{
    private readonly HttpClient _http;
    public LakalaOpenOptions Options { get; }

    public LakalaOpenPoster(LakalaOpenOptions options, HttpClient? http = null)
    {
        Options = options;
        _http   = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// 调用拉卡拉接口. 自动加签 + 自动验签.
    /// </summary>
    /// <param name="urlOrPath">完整 URL 或以 / 开头的路径 (自动拼 BaseUrl)</param>
    /// <param name="body">JSON 字符串 body. 整体加密接口请传 SM4 base64 密文.</param>
    /// <param name="contentType">默认 application/json. 整体 SM4 加密接口建议也用 application/json.</param>
    public async Task<LakalaCallResult> PostAsync(string urlOrPath, string body, string contentType = "application/json")
    {
        var url = urlOrPath.StartsWith("http") ? urlOrPath : Options.BaseUrl + urlOrPath;

        var req = LakalaHelper.CreateSignedHttpRequest(body, url, Options);
        // 默认 application/json. 整体 SM4 加密接口要求也是这个, 不需要换.
        // 如果未来某接口要求别的 (例如 text/plain), 这里强制覆盖.
        if (contentType != "application/json" && req.Content != null)
        {
            req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType) { CharSet = "utf-8" };
        }

        using var resp = await _http.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();

        var result = new LakalaCallResult
        {
            HttpStatus     = (int)resp.StatusCode,
            ResponseBody   = respBody,
            RequestBody    = body,
            RequestUrl     = url,
            Authorization  = req.Headers.TryGetValues("Authorization", out var a) ? a.FirstOrDefault() : null,
            Traceid        = resp.Headers.TryGetValues("Lklapi-Traceid", out var t) ? t.FirstOrDefault() : null,
        };

        // 拉卡拉响应有 Lklapi-Signature 头时, 自动验签
        if (resp.Headers.Contains("Lklapi-Signature"))
        {
            try
            {
                result.SignatureValid = LakalaHelper.VerifyResponseSignature(resp.Headers, respBody, Options);
                result.SignatureChecked = true;
            }
            catch (Exception e)
            {
                result.SignatureValid = false;
                result.SignatureChecked = true;
                result.SignatureError = e.Message;
            }
        }

        return result;
    }
}

public class LakalaCallResult
{
    public int      HttpStatus      { get; set; }
    public string   RequestUrl      { get; set; } = "";
    public string   RequestBody     { get; set; } = "";
    public string?  Authorization   { get; set; }
    public string   ResponseBody    { get; set; } = "";
    public string?  Traceid         { get; set; }
    public bool     SignatureChecked { get; set; }
    public bool     SignatureValid   { get; set; }
    public string?  SignatureError   { get; set; }
    public bool     IsSuccess        => HttpStatus >= 200 && HttpStatus < 300;
}
