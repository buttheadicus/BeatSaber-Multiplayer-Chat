using System;
using System.Text;

namespace MultiplayerChat.Core;

/// <summary>
/// Validates <see cref="Core.SlzMode.MarkerFileName"/> body (0.3.1+). Format must stay in sync with <c>SlzMarkerTool</c>
/// (same file is compile-linked into that project).
/// </summary>
/// <remarks>
/// Plain marker strings are not stored as IL string literals — XOR-encoded blobs plus split key masks.
/// This deters casual extraction; it is not cryptographic protection against a determined reverser.
/// </remarks>
public static class SlzMarkerProof
{
    /// <summary>Second line must be this many hex characters.</summary>
    public const int ProofHexCharLength = 62;

    /// <summary>Rolling XOR key = <see cref="_xorKeyMaskA"/>[i%8] XOR <see cref="_xorKeyMaskB"/>[i%8].</summary>
    private static readonly byte[] _xorKeyMaskA = { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0 };

    private static readonly byte[] _xorKeyMaskB = { 0x48, 0xa5, 0x78, 0xbc, 0xe9, 0xb4, 0x61, 0x91 };

    /// <summary>UTF-8 XOR ciphertext for header line (decoded equals format header).</summary>
    private static readonly byte[] _encHdr =
    {
        0x17, 0xc1, 0x6d, 0xac, 0x12, 0x7c, 0x92, 0x32, 0x16, 0xcb, 0x03, 0xf5
    };

    /// <summary>ASCII XOR ciphertext for proof line (62 chars).</summary>
    private static readonly byte[] _encProof =
    {
        0x3b, 0xa0, 0x4c, 0xf6, 0x10, 0x3b, 0xdb, 0x55, 0x3f, 0xa4, 0x48, 0xf2, 0x43, 0x3f, 0x8e, 0x59,
        0x68, 0xa8, 0x1d, 0xa5, 0x47, 0x6a, 0x8a, 0x02, 0x6c, 0xf5, 0x19, 0xa1, 0x4b, 0x6e, 0x86, 0x51,
        0x6b, 0xa3, 0x1d, 0xf0, 0x46, 0x3e, 0x88, 0x59, 0x63, 0xa1, 0x4f, 0xa6, 0x10, 0x6c, 0xda, 0x07,
        0x6b, 0xa3, 0x1d, 0xf0, 0x46, 0x3e, 0x88, 0x59, 0x63, 0xa1, 0x4f, 0xa6, 0x10, 0x6c
    };

    private static string? _cachedHdrUtf8;
    private static string? _cachedProofAscii;

    /// <summary>UTF-8 text written by SlzMarkerTool (LF line endings).</summary>
    public static string BuildMarkerFileContent()
    {
        var h = HeaderUtf8();
        var p = ProofAscii();
        var sb = new StringBuilder(h.Length + p.Length + 8);
        sb.Append(h).Append('\n').Append(p).Append('\n');
        return sb.ToString();
    }

    /// <summary>True if <paramref name="raw"/> matches the expected two-line marker (whitespace-tolerant).</summary>
    public static bool TryValidateMarkerContent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw!;
        var normalized = s.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return false;

        if (!string.Equals(lines[0].Trim(), HeaderUtf8(), StringComparison.Ordinal))
            return false;

        var secret = lines[1].Trim();
        if (secret.Length != ProofHexCharLength)
            return false;

        for (var i = 0; i < secret.Length; i++)
        {
            var c = secret[i];
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return string.Equals(secret, ProofAscii(), StringComparison.OrdinalIgnoreCase);
    }

    private static string HeaderUtf8() =>
        _cachedHdrUtf8 ??= DecodeXorUtf8(_encHdr);

    private static string ProofAscii() =>
        _cachedProofAscii ??= DecodeXorAscii(_encProof);

    private static string DecodeXorUtf8(byte[] cipher)
    {
        var buf = new byte[cipher.Length];
        XorDecodeInto(cipher, buf);
        return Encoding.UTF8.GetString(buf);
    }

    private static string DecodeXorAscii(byte[] cipher)
    {
        var buf = new byte[cipher.Length];
        XorDecodeInto(cipher, buf);
        return Encoding.ASCII.GetString(buf);
    }

    private static void XorDecodeInto(byte[] cipher, byte[] destination)
    {
        for (var i = 0; i < cipher.Length; i++)
        {
            var k = (byte)(_xorKeyMaskA[i % 8] ^ _xorKeyMaskB[i % 8]);
            destination[i] = (byte)(cipher[i] ^ k);
        }
    }
}
