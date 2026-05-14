using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LakalaDemo;

/// <summary>
/// 拉卡拉开放平台调用参数 (测试环境已预填). 生产环境只需替换:
///   AppId / SerialNo / Sm4Key / PriPem / PubCert / BaseUrlProd
/// </summary>
public class LakalaOpenOptions
{
    // ============================================================================
    //  环境 URL
    // ============================================================================
    public string BaseUrlTest { get; set; } = "https://test.wsmsd.cn/sit";
    public string BaseUrlProd { get; set; } = "https://s2.lakala.com";

    /// <summary>true=测试环境, false=生产环境</summary>
    public bool UseTestEnv { get; set; } = true;
    public string BaseUrl => UseTestEnv ? BaseUrlTest : BaseUrlProd;

    // ============================================================================
    //  接入方参数 (V3 测试环境)
    // ============================================================================
    public string AppId    { get; set; } = "OP00000003";
    public string SerialNo { get; set; } = "00dfba8194c41b84cf";

    /// <summary>SM4 密钥 (Base64, 解码后 16 字节). 仅 SM4 加密接口用到</summary>
    public string Sm4Key   { get; set; } = "LHo55AjrT4aDhAIBZhb5KQ==";

    // ============================================================================
    //  商户私钥 (PKCS#8 PEM 格式, -----BEGIN PRIVATE KEY----- 开头)
    //  注意: 不是 PKCS#1 的 -----BEGIN RSA PRIVATE KEY-----, 不是 .pfx/.p12!
    //  如果您拿到的是 .pfx, 用 openssl 转:
    //    openssl pkcs12 -in your.pfx -nocerts -nodes -out tmp.pem
    //    openssl pkcs8 -topk8 -nocrypt -in tmp.pem -out private.pem
    // ============================================================================
    public string PriPem { get; set; } = @"-----BEGIN PRIVATE KEY-----
MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDvDBZyHUDndAGx
rIcsCV2njhNO3vCEZotTaWYSYwtDvkcAb1EjsBFabXZaKigpqFXk5XXNI3NIHP9M
8XKzIgGvc65NpLAfRjVql8JiTvLyYd1gIUcOXMInabu+oX7dQSI1mS8XzqaoVRhD
ZQWhXcJW9bxMulgnzvk0Ggw07AjGF7si+hP/Va8SJmN7EJwfQq6TpSxR+WdIHpbW
dhZ+NHwitnQwAJTLBFvfk28INM39G7XOsXdVLfsooFdglVTOHpNuRiQAj9gShCCN
rpGsNQxDiJIxE43qRsNsRwigyo6DPJk/klgDJa417E2wgP8VrwiXparO4FMzOGK1
5quuoD7DAgMBAAECggEBANhmWOt1EAx3OBFf3f4/fEjylQgRSiqRqg8Ymw6KGuh4
mE4Md6eW/B6geUOmZjVP7nIIR1wte28M0REWgn8nid8LGf+v1sB5DmIwgAf+8G/7
qCwd8/VMg3aqgQtRp0ckb5OV2Mv0h2pbnltkWHR8LDIMwymyh5uCApbn/aTrCAZK
NXcPOyAn9tM8Bu3FHk3Pf24Er3SN+bnGxgpzDrFjsDSHjDFT9UMIc2WdA3tuMv9X
3DDn0bRCsHnsIw3WrwY6HQ8mumdbURk+2Ey3eRFfMYxyS96kOgBC2hqZOlDwVPAK
TPtS4hoq+cQ0sRaJQ4T0UALJrBVHa+EESgRaTvrXqAECgYEA+WKmy9hcvp6IWZlk
9Q1JZ+dgIVxrO65zylK2FnD1/vcTx2JMn73WKtQb6vdvTuk+Ruv9hY9PEsf7S8gH
STTmzHOUgo5x0F8yCxXFnfji2juoUnDdpkjtQK5KySDcpQb5kcCJWEVi9v+zObM0
Zr1Nu5/NreE8EqUl3+7MtHOu1TMCgYEA9WM9P6m4frHPW7h4gs/GISA9LuOdtjLv
AtgCK4cW2mhtGNAMttD8zOBQrRuafcbFAyU9de6nhGwetOhkW9YSV+xRNa7HWTeI
RgXJuJBrluq5e1QGTIwZU/GujpNaR4Qiu0B8TodM/FME7htsyxjmCwEfT6SDYlke
MzTbMa9Q0DECgYBqsR/2+dvD2YMwAgZFKKgNAdoIq8dcwyfamUQ5mZ5EtGQL2yw4
8zibHh/LiIxgUD1Kjk/qQgNsX45NP4iOc0mCkrgomtRqdy+rumbPTNmQ0BEVJCBP
scd+8pIgNiTvnWpMRvj7gMP0NDTzLI3wnnCRIq8WAtR2jZ0Ejt+ZHBziLQKBgQDi
bEe/zqNmhDuJrpXEXmO7fTv3YB/OVwEj5p1Z/LSho2nHU3Hn3r7lbLYEhUvwctCn
Ll2fzC7Wic1rsGOqOcWDS5NDrZpUQGGF+yE/JEOiZcPwgH+vcjaMtp0TAfRzuQEz
NzV8YGwxB4mtC7E/ViIuVULHAk4ZGZI8PbFkDxjKgQKBgG8jEuLTI1tsP3kyaF3j
Aylnw7SkBc4gfe9knsYlw44YlrDSKr8AOp/zSgwvMYvqT+fygaJ3yf9uIBdrIilq
CHKXccZ9uA/bT5JfIi6jbg3EoE9YhB0+1aGAS1O2dBvUiD8tJ+BjAT4OB0UDpmM6
QsFLQgFyXgvDnzr/o+hQJelW
-----END PRIVATE KEY-----
";

    // ============================================================================
    //  拉卡拉验签公钥证书 (lkl-apigw-v2.cer 测试环境)
    //  生产环境需向拉卡拉申请, 拿到 .cer 文件后粘贴到这里替换.
    // ============================================================================
    public string PubCert { get; set; } = @"-----BEGIN CERTIFICATE-----
MIIEMTCCAxmgAwIBAgIGAXRTgcMnMA0GCSqGSIb3DQEBCwUAMHYxCzAJBgNVBAYT
AkNOMRAwDgYDVQQIDAdCZWlKaW5nMRAwDgYDVQQHDAdCZWlKaW5nMRcwFQYDVQQK
DA5MYWthbGEgQ28uLEx0ZDEqMCgGA1UEAwwhTGFrYWxhIE9yZ2FuaXphdGlvbiBW
YWxpZGF0aW9uIENBMB4XDTIwMTAxMDA1MjQxNFoXDTMwMTAwODA1MjQxNFowZTEL
MAkGA1UEBhMCQ04xEDAOBgNVBAgMB0JlaUppbmcxEDAOBgNVBAcMB0JlaUppbmcx
FzAVBgNVBAoMDkxha2FsYSBDby4sTHRkMRkwFwYDVQQDDBBBUElHVy5MQUtBTEEu
Q09NMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAt1zHL54HiI8d2sLJ
lwoQji3/ln0nsvfZ/XVpOjuB+1YR6/0LdxEDMC/hxI6iH2Rm5MjwWz3dmN/6BZeI
gwGeTOWJUZFARo8UduKrlhC6gWMRpAiiGC8wA8stikc5gYB+UeFVZi/aJ0WN0cpP
JYCvPBhxhMvhVDnd4hNohnR1L7k0ypuWg0YwGjC25FaNAEFBYP9EYUyCJjE//9Z7
sMzHR9SJYCqqo6r9bOH9G6sWKuEp+osuAh+kJIxJMHfipw7w3tEcWG0hce9u/el4
cYJtg8/PPMVoccKmeCzMvarr7jdKP4lenJbtwlgyfs+JgNu60KMUJH8RS72wC9NY
uFz09wIDAQABo4HVMIHSMIGSBgNVHSMEgYowgYeAFCnH4DkZPR6CZxRn/kIqVsMo
dJHpoWekZTBjMQswCQYDVQQGEwJDTjEQMA4GA1UECAwHQmVpSmluZzEQMA4GA1UE
BwwHQmVpSmluZzEXMBUGA1UECgwOTGFrYWxhIENvLixMdGQxFzAVBgNVBAMMDkxh
a2FsYSBSb290IENBggYBaiUALIowHQYDVR0OBBYEFJ2Kx9YZfmWpkKFnC33C0r5D
K3rFMAwGA1UdEwEB/wQCMAAwDgYDVR0PAQH/BAQDAgeAMA0GCSqGSIb3DQEBCwUA
A4IBAQBZoeU0XyH9O0LGF9R+JyGwfU/O5amoB97VeM+5n9v2z8OCiIJ8eXVGKN9L
tl9QkpTEanYwK30KkpHcJP1xfVkhPi/cCMgfTWQ5eKYC7Zm16zk7n4CP6IIgZIqm
TVGsIGKk8RzWseyWPB3lfqMDR52V1tdA1S8lJ7a2Xnpt5M2jkDXoArl3SVSwCb4D
AmThYhak48M++fUJNYII9JBGRdRGbfJ2GSFdPXgesUL2CwlReQwbW4GZkYGOg9LK
CNPK6XShlNdvgPv0CCR08KCYRwC3HZ0y1F0NjaKzYdGNPrvOq9lA495ONZCvzYDo
gmsu/kd6eqxTs/JwdaIYr4sCMg8Z
-----END CERTIFICATE-----
";

    // ============================================================================
    //  懒加载 RSA 实例 (instance field, 不是 static — 避免多套配置冲突)
    // ============================================================================
    private RSA? _priRsa;
    private RSA? _pubRsa;
    private readonly object _priLock = new();
    private readonly object _pubLock = new();

    /// <summary>
    /// 加载私钥 PEM. 用跨平台的 RSA.Create() + ImportFromPem(), 不用 RSACryptoServiceProvider (Windows-only).
    /// 需要 .NET 5+, 支持 PKCS#1 (BEGIN RSA PRIVATE KEY) 和 PKCS#8 (BEGIN PRIVATE KEY) 两种 PEM 格式.
    /// </summary>
    public RSA GetPriRsa()
    {
        if (_priRsa != null) return _priRsa;
        lock (_priLock)
        {
            if (_priRsa != null) return _priRsa;
            var rsa = RSA.Create();
            rsa.ImportFromPem(PriPem.AsSpan());
            _priRsa = rsa;
            return _priRsa;
        }
    }

    /// <summary>
    /// 加载拉卡拉验签公钥. 用 X509Certificate2.CreateFromPem (.NET 5+).
    /// </summary>
    public RSA GetPubRsa()
    {
        if (_pubRsa != null) return _pubRsa;
        lock (_pubLock)
        {
            if (_pubRsa != null) return _pubRsa;
            using var cert = X509Certificate2.CreateFromPem(PubCert.AsSpan());
            var pub = cert.GetRSAPublicKey()
                ?? throw new InvalidOperationException("证书里没有 RSA 公钥");
            _pubRsa = pub;
            return _pubRsa;
        }
    }
}
