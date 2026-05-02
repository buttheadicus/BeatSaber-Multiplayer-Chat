using System;
using TMPro;
using UnityEngine;

namespace MultiplayerChat.UI;

/// <summary>
/// BSML sometimes leaves Unity TMP defaults (&quot;Default String&quot;) on secondary labels next to settings rows.
/// Clears any matching text under a parsed view root.
/// </summary>
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
