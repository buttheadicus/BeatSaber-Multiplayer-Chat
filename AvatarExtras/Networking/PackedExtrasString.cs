using System.Globalization;
using System.Text;
using BeatSaber.BeatAvatarSDK;
using UnityEngine;

namespace MultiplayerChat.AvatarExtras.Networking;

public struct PackedExtrasString
{
    public const char PackStart = '#';
    public const char PackDelim = '$';
    public const char WireColorSep = '|';

    public string? GlassesId;
    public string? FacialHairId;

    public Color? WireGlassesColor;

    public Color? WireFacialHairColor;

    public PackedExtrasString(string? glassesId, string? facialHairId, Color? wireGlassesColor = null,
        Color? wireFacialHairColor = null)
    {
        GlassesId = NormalizeId(glassesId);
        FacialHairId = NormalizeId(facialHairId);
        WireGlassesColor = wireGlassesColor;
        WireFacialHairColor = wireFacialHairColor;
    }

    private static string? NormalizeId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id is "" or "None")
            return null;
        return id;
    }

    public static PackedExtrasString? TryFromString(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return null;

        var s = str!;
        if (s[0] != PackStart)
            return null;

        var idPart = s;
        Color? wireG = null;
        Color? wireF = null;
        var pipe = s.IndexOf(WireColorSep);
        if (pipe >= 0)
        {
            idPart = s.Substring(0, pipe);
            var tail = s.Substring(pipe + 1);
            var hexParts = tail.Split(WireColorSep);
            if (hexParts.Length > 0 && TryParseHexRgb(hexParts[0], out var cg))
                wireG = cg;
            if (hexParts.Length > 1 && TryParseHexRgb(hexParts[1], out var cf))
                wireF = cf;
        }

        var delim = idPart.IndexOf(PackDelim, 1);
        if (delim < 0)
            return null;

        var g = idPart.Substring(1, delim - 1);
        var afterFirst = idPart.Substring(delim + 1);
        var secondDelim = afterFirst.IndexOf(PackDelim);
        var f = secondDelim < 0 ? afterFirst : afterFirst.Substring(0, secondDelim);

        return new PackedExtrasString(
            string.IsNullOrEmpty(g) ? null : g,
            string.IsNullOrEmpty(f) ? null : f,
            wireG,
            wireF);
    }

    public static PackedExtrasString FromEditorState(AvatarData ad)
    {
        var parsed = TryFromString(ad.facialHairId);
        if (parsed.HasValue)
        {
            var x = parsed.Value;
            if (!x.WireGlassesColor.HasValue)
                x.WireGlassesColor = ad.glassesColor;
            if (!x.WireFacialHairColor.HasValue)
                x.WireFacialHairColor = ad.facialHairColor;
            return x;
        }

        return new PackedExtrasString(ad.glassesId, ad.facialHairId, ad.glassesColor, ad.facialHairColor);
    }

    public void ApplyTo(AvatarData avatarData)
    {
        // BinaryWriter.Write(string) throws if null; vanilla JSON uses "None" for absent accessory ids.
        avatarData.glassesId = GlassesId ?? "None";

        if (WireGlassesColor.HasValue)
            avatarData.glassesColor = WireGlassesColor.Value;
        if (WireFacialHairColor.HasValue)
            avatarData.facialHairColor = WireFacialHairColor.Value;

        if (GlassesId != null || FacialHairId != null)
            avatarData.facialHairId = ToWireString();

        // Same BinaryWriter null rule as glassesId when neither packed id nor wire string was written.
        avatarData.facialHairId ??= "None";
    }

    public string ToWireString()
    {
        var sb = new StringBuilder();

        sb.Append(PackStart);

        if (GlassesId is not null)
            sb.Append(GlassesId);

        sb.Append(PackDelim);

        if (FacialHairId is not null)
            sb.Append(FacialHairId);

        var emitColors = GlassesId != null || FacialHairId != null;
        if (emitColors && (WireGlassesColor.HasValue || WireFacialHairColor.HasValue))
        {
            sb.Append(WireColorSep);
            sb.Append(ColorToHexRgb(WireGlassesColor ?? Color.black));
            sb.Append(WireColorSep);
            sb.Append(ColorToHexRgb(WireFacialHairColor ?? Color.black));
        }

        return sb.ToString();
    }

    public static void SyncSeparateColorsFromPackedWire(AvatarData avatarData)
    {
        if (avatarData == null)
            return;

        var parsed = TryFromString(avatarData.facialHairId);
        if (parsed == null)
            return;

        var x = parsed.Value;
        if (x.WireGlassesColor.HasValue)
            avatarData.glassesColor = x.WireGlassesColor.Value;
        if (x.WireFacialHairColor.HasValue)
            avatarData.facialHairColor = x.WireFacialHairColor.Value;
    }

    private static bool TryParseHexRgb(string hex, out Color c)
    {
        c = Color.black;
        hex = hex.Trim();
        if (hex.Length < 6)
            return false;
        try
        {
            var r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            c = new Color(r / 255f, g / 255f, b / 255f, 1f);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ColorToHexRgb(Color c)
    {
        byte B(float x) => (byte)Mathf.Clamp(Mathf.RoundToInt(x * 255f), 0, 255);
        return $"{B(c.r):X2}{B(c.g):X2}{B(c.b):X2}";
    }
}
