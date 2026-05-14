using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LakalaDemo;

/// <summary>
/// 拉卡拉 V3 接口加签 / 验签工具. SHA256withRSA.
/// </summary>
public static class LakalaHelper
{
    // 解析 Authorization 参数, 形如 a="abc",b="def" → ("a","abc"), ("b","def")
    private static readonly Regex _signRegex = new(@"(\w+)\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    // ============================================================================
    //  构建签名 HTTP 请求 (5 行报文)
    // ============================================================================
    /// <summary>
    /// 拼装 HTTP 请求 + Authorization (5 行报文加签: appid + serial + ts + nonce + body + \n).
    /// </summary>
    public static HttpRequestMessage CreateSignedHttpRequest(string body, string url, LakalaOpenOptions options)
    {
        var tms   = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N")[..12];

        // 5 行报文: 注意最后一行 body 后也要 \n. 90%+ 的签名错误都是漏掉这个换行
        var signSource = $"{options.AppId}\n{options.SerialNo}\n{tms}\n{nonce}\n{body}\n";
        var sigBytes = options.GetPriRsa().SignData(
            Encoding.UTF8.GetBytes(signSource),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(sigBytes);

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        // ⚠ 用 TryAddWithoutValidation, 不要 new AuthenticationHeaderValue(...)
        //    因为 .NET 的 AuthenticationHeaderValue 会按 RFC 7235 严格解析,
        //    可能把拉卡拉这种 'scheme appid="...",serial_no="..."' 的格式
        //    重新格式化或拒绝, 导致服务端验签失败.
        var authValue =
            "LKLAPI-SHA256withRSA " +
            $"appid=\"{options.AppId}\"," +
            $"serial_no=\"{options.SerialNo}\"," +
            $"timestamp=\"{tms}\"," +
            $"nonce_str=\"{nonce}\"," +
            $"signature=\"{signature}\"";
        req.Headers.TryAddWithoutValidation("Authorization", authValue);

        return req;
    }

    // ============================================================================
    //  验证拉卡拉同步响应签名 (5 行报文: appid + serial + ts + nonce + body + \n)
    // ============================================================================
    /// <summary>
    /// 验证拉卡拉同步响应的签名. 5 行报文.
    /// 注意: 异步通知用 VerifyAsyncNotifySignature (3 行报文).
    /// </summary>
    public static bool VerifyResponseSignature(HttpResponseHeaders headers, string body, LakalaOpenOptions options)
    {
        // 异步通知没有 Lklapi-Appid, 同步响应一般都有. 用 TryGetValues 容错.
        var appid  = TryGetSingle(headers, "Lklapi-Appid")     ?? "";
        var serial = TryGetSingle(headers, "Lklapi-Serial")    ?? "";
        var tms    = TryGetSingle(headers, "Lklapi-Timestamp") ?? "";
        var nonce  = TryGetSingle(headers, "Lklapi-Nonce")     ?? "";
        var sig    = TryGetSingle(headers, "Lklapi-Signature") ?? "";

        if (string.IsNullOrEmpty(sig)) return false;

        var signSource = $"{appid}\n{serial}\n{tms}\n{nonce}\n{body}\n";
        return options.GetPubRsa().VerifyData(
            Encoding.UTF8.GetBytes(signSource),
            Convert.FromBase64String(sig),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    // ============================================================================
    //  验证拉卡拉异步通知签名 (3 行报文: ts + nonce + body + \n)
    // ============================================================================
    /// <summary>
    /// 验证拉卡拉异步通知的签名.
    /// 异步通知格式:
    ///   Authorization: LKLAPI-SHA256withRSA timestamp="...",nonce_str="...",signature="..."
    /// 签名报文 3 行: ${timestamp}\n${nonce_str}\n${body}\n (注意末尾 \n)
    ///
    /// 重要 (来自拉卡拉文档):
    ///   - 不要对 body 做 JSON 反序列化, 必须用原始 HTTP 字节
    ///   - Spring 用户不能用 @RequestBody 注解 (会动 body), 用 HttpServletRequest.getInputStream
    ///   - .NET 用 await request.Content.ReadAsStringAsync() 即可
    /// </summary>
    public static bool VerifyAsyncNotifySignature(string authorizationHeader, string body, LakalaOpenOptions options)
    {
        var matches = _signRegex.Matches(authorizationHeader);
        string Get(string name) => matches.Cast<Match>().FirstOrDefault(m => m.Groups[1].Value == name)?.Groups[2].Value ?? "";

        var tms   = Get("timestamp");
        var nonce = Get("nonce_str");
        var sig   = Get("signature");

        if (string.IsNullOrEmpty(sig)) return false;

        var signSource = $"{tms}\n{nonce}\n{body}\n";
        return options.GetPubRsa().VerifyData(
            Encoding.UTF8.GetBytes(signSource),
            Convert.FromBase64String(sig),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    /// <summary>HttpRequestHeaders 版本 (用 HttpListener / ASP.NET 接收异步通知时用)</summary>
    public static bool VerifyAsyncNotifySignature(HttpRequestHeaders headers, string body, LakalaOpenOptions options)
    {
        var auth = headers.Authorization;
        if (auth == null) return false;
        // .NET 把 scheme 和 parameter 自动拆分, 我们要的是 parameter 部分
        return VerifyAsyncNotifySignature(auth.Parameter ?? "", body, options);
    }

    private static string? TryGetSingle(HttpResponseHeaders headers, string name)
    {
        return headers.TryGetValues(name, out var v) ? v.FirstOrDefault() : null;
    }
}
