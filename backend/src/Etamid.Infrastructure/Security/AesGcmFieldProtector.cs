using System.Security.Cryptography;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Etamid.Infrastructure.Security;

public sealed class DataProtectionOptions
{
    public const string Section = "Security";

    /// <summary>Base64 of exactly 32 random bytes. Keep it in a secret store; losing it makes stored IBANs unreadable.</summary>
    public string DataKey { get; set; } = string.Empty;
}

/// <summary>AES-256-GCM for values stored at rest, HMAC-SHA256 for deterministic lookups.</summary>
public sealed class AesGcmFieldProtector : IFieldProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmFieldProtector(IOptions<DataProtectionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.DataKey);
        if (_key.Length != 32)
            throw new InvalidOperationException("Security:DataKey must be a base64 string of exactly 32 bytes.");
    }

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, NonceSize);
        cipher.CopyTo(output, NonceSize + TagSize);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedValue)
    {
        var data = Convert.FromBase64String(protectedValue);
        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var cipher = data.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string Hash(string value)
        => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
