using System;
using TMPro;
using UnityEngine;

namespace MultiplayerChat.UI;

internal static class BsmlDefaultStringCleanup
{
    internal static void StripPlaceholderLabels(GameObject root)
    {
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null) continue;
            var s = t.text;
            if (string.IsNullOrEmpty(s)) continue;
            var trimmed = s.Trim();
            if (string.Equals(trimmed, "Default String", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Default Text", StringComparison.OrdinalIgnoreCase))
                t.text = "";
        }
    }
}
