using UnityEngine;

namespace MultiplayerChat.AvatarExtras.Utils;

public static class ColorExtensions
{
    public static bool ApproximatelyEquals(this Color a, Color b) =>
        ApproximatelyWithTolerance(a.r, b.r) &&
        ApproximatelyWithTolerance(a.g, b.g) &&
        ApproximatelyWithTolerance(a.b, b.b) &&
        ApproximatelyWithTolerance(a.a, b.a);

    public static bool ApproximatelyEquals(this Color a, Color? b)
    {
        if (!b.HasValue)
            return false;

        return a.ApproximatelyEquals(b.Value);
    }

    private static bool ApproximatelyWithTolerance(float a, float b, float tolerance = 0.02f)
        => Mathf.Abs(a - b) < tolerance;
}
