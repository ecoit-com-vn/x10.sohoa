using System.Security.Cryptography;
using System.Text;

namespace EvnHanoi.SyncService.Security;

/// <summary>
/// Mã hoá giá trị header nhạy cảm (ví dụ token xác thực PMIS) bằng AES-GCM trước khi lưu Oracle —
/// cùng cơ chế với <c>ExternalApiKeyProtector</c> (IdentityService) dùng cho EXTERNAL_API_KEYS.EncryptedKey.
/// </summary>
public sealed class PmisHeaderValueProtector : IPmisHeaderValueProtector
{
    private readonly byte[] _encryptionKey;

    public PmisHeaderValueProtector(IConfiguration configuration)
    {
        var secret = configuration["Pmis:EndpointHeaderEncryptionKey"]
            ?? configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("PMIS endpoint header encryption secret is not configured.");
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return null;

        try
        {
            var payload = Convert.FromBase64String(protectedValue);
            if (payload.Length <= 28) return null;

            var nonce = payload[..12];
            var tag = payload[12..28];
            var ciphertext = payload[28..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_encryptionKey, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
