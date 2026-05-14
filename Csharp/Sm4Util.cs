using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace LakalaDemo;

/// <summary>
/// SM4/ECB/PKCS5Padding 加解密 (用 BouncyCastle).
///
/// 拉卡拉的整体加密接口 (如商户进件 /api/v3/tkbs/merchant_encry):
///   - HTTP 请求 body 就是 SM4 base64 密文字符串 (不是 JSON)
///   - HTTP 响应 body 也是 SM4 base64 密文字符串
///   - 签名是对 SM4 密文做的, 不是对明文
///
/// 单字段加密接口: 只对 req_data 里的某些字段做 SM4, 整体仍是 JSON.
///
/// 注: PKCS5Padding 和 PKCS7Padding 对 SM4 (16 字节块) 完全等价, BouncyCastle 用 Pkcs7Padding.
/// </summary>
public static class Sm4Util
{
    /// <summary>
    /// 加密. 输入 UTF-8 字符串, 输出 base64 密文.
    /// </summary>
    /// <param name="keyBase64">SM4 密钥 (拉卡拉发的 Base64 字符串, 解码后必须 16 字节)</param>
    /// <param name="plaintext">明文 UTF-8 字符串</param>
    public static string Encrypt(string keyBase64, string plaintext)
    {
        var key  = ValidateKey(keyBase64);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipher = MakeCipher(forEncryption: true, key);
        var output = new byte[cipher.GetOutputSize(data.Length)];
        var len = cipher.ProcessBytes(data, 0, data.Length, output, 0);
        len += cipher.DoFinal(output, len);
        return Convert.ToBase64String(output, 0, len);
    }

    /// <summary>
    /// 解密. 输入 base64 密文, 输出 UTF-8 字符串.
    /// </summary>
    public static string Decrypt(string keyBase64, string cipherBase64)
    {
        var key    = ValidateKey(keyBase64);
        var cipher = Convert.FromBase64String(cipherBase64);
        var bc     = MakeCipher(forEncryption: false, key);
        var output = new byte[bc.GetOutputSize(cipher.Length)];
        var len = bc.ProcessBytes(cipher, 0, cipher.Length, output, 0);
        len += bc.DoFinal(output, len);
        return Encoding.UTF8.GetString(output, 0, len);
    }

    private static byte[] ValidateKey(string keyBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        if (key.Length != 16)
            throw new ArgumentException($"SM4 密钥 Base64 解码后应该是 16 字节, 实际 {key.Length} 字节. 拉卡拉测试 SM4Key 是 'LHo55AjrT4aDhAIBZhb5KQ==' (16 字节)");
        return key;
    }

    private static PaddedBufferedBlockCipher MakeCipher(bool forEncryption, byte[] key)
    {
        var engine = new SM4Engine();
        var cipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding());
        cipher.Init(forEncryption, new KeyParameter(key));
        return cipher;
    }
}
