using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MultiplayerChat.Core;

/// <summary>
/// End-to-end encryption for chat messages. Uses AES-256-CBC with HMAC-SHA256.
/// Session key is derived from lobby state - only players in the lobby can derive it.
/// </summary>
public class EncryptionManager
{
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int HmacSize = 32;
    private const string KeyDerivationSalt = "MultiplayerChat.v1";

    private byte[]? _sessionKey;
    private string _lastSessionState = "";

    /// <summary>
    /// Derives session key from player IDs. Only lobby members can compute this.
    /// </summary>
    public void UpdateSessionKey(IReadOnlyList<string> playerIds)
    {
        if (playerIds == null || playerIds.Count == 0)
        {
            _sessionKey = null;
            _lastSessionState = "";
            return;
        }

        var sortedIds = playerIds.OrderBy(id => id).ToList();
        var state = string.Join(",", sortedIds);
        if (state == _lastSessionState)
            return;

        _lastSessionState = state;

        using var deriveBytes = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(state),
            Encoding.UTF8.GetBytes(KeyDerivationSalt),
            10000,
            HashAlgorithmName.SHA256);

        _sessionKey = deriveBytes.GetBytes(KeySize);
    }

    /// <summary>
    /// Encrypts plaintext. Returns null if encryption fails (e.g. no session key).
    /// </summary>
    public byte[]? Encrypt(string plaintext)
    {
        if (_sessionKey == null || string.IsNullOrEmpty(plaintext))
            return null;

        return Encrypt(Encoding.UTF8.GetBytes(plaintext));
    }

    /// <summary>
    /// Encrypts plaintext bytes. Format: IV (16) + Ciphertext + HMAC (32)
    /// </summary>
    public byte[]? Encrypt(byte[] plaintext)
    {
        if (_sessionKey == null || plaintext == null || plaintext.Length == 0)
            return null;

        var iv = new byte[IvSize];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(iv);

        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = _sessionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        var hmac = ComputeHmac(iv.Concat(ciphertext).ToArray());

        var result = new byte[IvSize + ciphertext.Length + HmacSize];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        Buffer.BlockCopy(hmac, 0, result, IvSize + ciphertext.Length, HmacSize);

        return result;
    }

    /// <summary>
    /// Decrypts ciphertext. Returns null if decryption fails.
    /// </summary>
    public string? Decrypt(byte[] encrypted)
    {
        var decrypted = DecryptToBytes(encrypted);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
    }

    /// <summary>
    /// Decrypts to raw bytes.
    /// </summary>
    public byte[]? DecryptToBytes(byte[] encrypted)
    {
        if (_sessionKey == null || encrypted == null || encrypted.Length < IvSize + HmacSize)
            return null;

        var iv = new byte[IvSize];
        var ciphertextLen = encrypted.Length - IvSize - HmacSize;
        if (ciphertextLen <= 0)
            return null;

        var receivedHmac = new byte[HmacSize];
        Buffer.BlockCopy(encrypted, 0, iv, 0, IvSize);
        Buffer.BlockCopy(encrypted, IvSize + ciphertextLen, receivedHmac, 0, HmacSize);

        var ciphertext = new byte[ciphertextLen];
        Buffer.BlockCopy(encrypted, IvSize, ciphertext, 0, ciphertextLen);

        var computedHmac = ComputeHmac(iv.Concat(ciphertext).ToArray());
        if (!computedHmac.SequenceEqual(receivedHmac))
            return null;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _sessionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertextLen);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(_sessionKey!);
        return hmac.ComputeHash(data);
    }

    public bool HasSessionKey => _sessionKey != null;
}
