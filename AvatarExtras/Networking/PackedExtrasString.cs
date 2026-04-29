using System.Text;
using BeatSaber.BeatAvatarSDK;

namespace MultiplayerChat.AvatarExtras.Networking;

/// <summary>
/// Packs extra ids into <see cref="AvatarData.facialHairId"/> for vanilla networking while syncing
/// <see cref="AvatarData.glassesId"/> for the stock glasses MPB path.
/// </summary>
public struct PackedExtrasString
{
    public const char PackStart = '#';
    public const char PackDelim = '$';

    public string? GlassesId;
    public string? FacialHairId;

    public PackedExtrasString(string? glassesId, string? facialHairId)
    {
        GlassesId = glassesId;
        if (GlassesId is "" or "None") GlassesId = null;

        FacialHairId = facialHairId;
        if (FacialHairId is "" or "None") FacialHairId = null;
    }

    public static PackedExtrasString? TryFromAvatarData(AvatarData avatarData) =>
        TryFromString(avatarData.facialHairId);

    public static PackedExtrasString? TryFromString(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return null;

        var s = str!;
        if (s[0] != PackStart)
            return null;

        var delim = s.IndexOf(PackDelim, 1);
        if (delim < 0)
            return null;

        var g = s.Substring(1, delim - 1);
        var afterFirst = s.Substring(delim + 1);
        var secondDelim = afterFirst.IndexOf(PackDelim);
        // Match legacy string.Split: only the segment between first and second '$' is the facial id.
        var f = secondDelim < 0 ? afterFirst : afterFirst.Substring(0, secondDelim);
        return new PackedExtrasString(g, f);
    }

    public void ApplyTo(AvatarData avatarData)
    {
        avatarData.glassesId = GlassesId;
        avatarData.facialHairId = ToString();
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append(PackStart);

        if (GlassesId is not null)
            sb.Append(GlassesId);

        sb.Append(PackDelim);

        if (FacialHairId is not null)
            sb.Append(FacialHairId);

        return sb.ToString();
    }
}
