using System;
using System.IO;
using System.Reflection;

namespace MultiplayerChat.Core;

// Hotfix build counter embedded from the repo "version" file (0, 1, 2, ...).
internal static class ModBuildVersion
{
    public const string EmbeddedResourceName = "MultiplayerChat.version";

    public static bool TryGetEmbeddedBuildNumber(out int buildNumber)
    {
        buildNumber = -1;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null)
                return false;

            using var reader = new StreamReader(stream);
            return TryParseBuildNumber(reader.ReadToEnd(), out buildNumber);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseBuildNumber(string? text, out int buildNumber)
    {
        buildNumber = -1;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text!.Trim();
        return int.TryParse(trimmed, out buildNumber) && buildNumber >= 0;
    }
}
