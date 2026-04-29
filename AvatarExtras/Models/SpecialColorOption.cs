using System.Collections.Generic;
using MultiplayerChat.AvatarExtras.Networking;
using MultiplayerChat.AvatarExtras.Utils;
using UnityEngine;

namespace MultiplayerChat.AvatarExtras.Models;

public class SpecialColorOption
{
    public readonly string Key;
    public readonly string Label;
    public readonly Color? MagicColor;

    public SpecialColorOption(string key, string label, Color? magicColor = null)
    {
        Key = key;
        Label = label;
        MagicColor = magicColor;
    }

    public override string ToString() => Label;

    public static readonly SpecialColorOption Default
        = new SpecialColorOption("default", "Normal", null);

    public static readonly SpecialColorOption Rainbow =
        new SpecialColorOption("rainbow", "Rainbow Shader", Magic.MagicRainbowColor);

    public static readonly List<SpecialColorOption> AllOptions = new() { Default, Rainbow };

    public static SpecialColorOption DetectOptionMagically(Color c) =>
        c.ApproximatelyEquals(Magic.MagicRainbowColor) ? Rainbow : Default;

    public static SpecialColorOption? DetectNonDefaultOptionMagically(Color c) =>
        c.ApproximatelyEquals(Magic.MagicRainbowColor) ? Rainbow : null;

    public static SpecialColorOption? DetectNonDefaultOptionMagically(List<Color> colors)
    {
        for (var i = 0; i < colors.Count; i++)
            if (colors[i].ApproximatelyEquals(Magic.MagicRainbowColor))
                return Rainbow;
        return null;
    }
}