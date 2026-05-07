using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MultiplayerChat.Core;

public class EncryptionManager
{
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int HmacSize = 32;
    private const string KeyDerivationSalt = "MultiplayerChat.v1";

    private readonly object _sync = new();

    private byte[]? _sessionKey;
    private string _lastSessionState = "";

    public void UpdateSessionKey(IReadOnlyList<string> playerIds)
    {
        lock (_sync)
        {
            InnerUpdateSessionKey(playerIds);
        }
    }

    private void InnerUpdateSessionKey(IReadOnlyList<string> playerIds)
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

    public byte[]? Encrypt(string plaintext)
    {
        lock (_sync)
        {
            if (_sessionKey == null || string.IsNullOrEmpty(plaintext))
                return null;

            return EncryptUnlocked(Encoding.UTF8.GetBytes(plaintext));
        }
    }

    public byte[]? Encrypt(byte[] plaintext)
    {
        lock (_sync)
        {
            if (_sessionKey == null || plaintext == null || plaintext.Length == 0)
                return null;

            return EncryptUnlocked(plaintext);
        }
    }

    private byte[]? EncryptUnlocked(byte[] plaintext)
    {
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

        var hmac = ComputeHmacUnlocked(iv.Concat(ciphertext).ToArray());

        var result = new byte[IvSize + ciphertext.Length + HmacSize];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        Buffer.BlockCopy(hmac, 0, result, IvSize + ciphertext.Length, HmacSize);

        return result;
    }

    public string? Decrypt(byte[] encrypted)
    {
        var decrypted = DecryptToBytes(encrypted);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
    }

    public byte[]? DecryptToBytes(byte[] encrypted)
    {
        lock (_sync)
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

            var computedHmac = ComputeHmacUnlocked(iv.Concat(ciphertext).ToArray());
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
    }

    private byte[] ComputeHmacUnlocked(byte[] data)
    {
        using var hmac = new HMACSHA256(_sessionKey!);
        return hmac.ComputeHash(data);
    }

    public bool HasSessionKey
    {
        get
        {
            lock (_sync)
                return _sessionKey != null;
        }
    }

    public string LastSessionStateFingerprint
    {
        get
        {
            lock (_sync)
                return _lastSessionState;
        }
    }
}
