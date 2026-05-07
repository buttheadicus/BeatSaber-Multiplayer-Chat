using System;
using HMUI;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.Core;

public static class LobbyUiStartButtonLocator
{
    private static readonly string[] LobbyRoots =
    {
        "MultiplayerLobbyCenterStage", "CenterStage", "LobbySetup", "HostSetup"
    };

    public static Transform? FindStartButtonTransform()
    {
        var byName = GameObject.Find("StartButton") ?? GameObject.Find("HostSetup/StartButton");
        if (byName != null)
        {
            var btn = byName.GetComponent<Button>();
            if (btn != null)
                return btn.transform;
        }

        foreach (var rootName in LobbyRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
                continue;

            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                if (btn == null)
                    continue;

                if (ButtonNameLooksLikeStart(btn.gameObject.name))
                    return btn.transform;

                foreach (var tmp in btn.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    if (tmp != null && tmp.text.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
                        return btn.transform;
                }

                foreach (var curved in btn.GetComponentsInChildren<CurvedTextMeshPro>(true))
                {
                    if (curved != null && curved.text.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
                        return btn.transform;
                }
            }
        }

        return null;
    }

    private static bool ButtonNameLooksLikeStart(string goName)
    {
        var n = goName.ToUpperInvariant();
        if (n.Contains("RESTART"))
            return false;
        return n.Contains("STARTBUTTON") || n == "START" ||
               (n.Contains("START") && (n.Contains("GAME") || n.Contains("LEVEL") || n.Contains("BUTTON")));
    }
}
