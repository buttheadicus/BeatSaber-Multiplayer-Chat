using System;
using System.IO;
using UnityEngine;

namespace MultiplayerChat.Settings;

public static class MultiplayerExtensionsJson
{
    private static string GetPath()
    {
        var dataPath = Application.dataPath;
        var installRoot = Path.GetDirectoryName(dataPath);
        return Path.Combine(installRoot ?? "", "UserData", "MultiplayerExtensions.json");
    }

    public static string? GetPlayerColorHex()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var match = System.Text.RegularExpressions.Regex.Match(json, @"""PlayerColor""\s*:\s*""#?([^""]+)""");
            if (!match.Success) return null;
            var hex = match.Groups[1].Value.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            return hex.Length == 6 ? hex : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SetPlayerColorHex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length != 6) return;
        var path = GetPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var valueWithHash = "#" + hex;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var regex = new System.Text.RegularExpressions.Regex(@"""PlayerColor""\s*:\s*""[^""]*""");
                json = regex.Replace(json, $"\"PlayerColor\": \"{valueWithHash}\"");
                File.WriteAllText(path, json);
            }
            else
            {
                var defaultJson = @"{
  ""SoloEnvironment"": false,
  ""SideBySide"": false,
  ""SideBySideDistance"": 4.0,
  ""DisableAvatarConstraints"": true,
  ""DisableMultiplayerPlatforms"": false,
  ""DisableMultiplayerLights"": false,
  ""DisableMultiplayerObjects"": false,
  ""DisableMultiplayerColors"": false,
  ""DisablePlatformMovement"": false,
  ""MissLighting"": true,
  ""PersonalMissLightingOnly"": false,
  ""PlayerColor"": """ + valueWithHash + @""",
  ""MissColor"": ""#C000FF""
}";
                File.WriteAllText(path, defaultJson);
            }
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to write MultiplayerExtensions.json: {ex.Message}");
        }
    }
}
