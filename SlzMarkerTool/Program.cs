using System;
using System.IO;

namespace SlzMarkerTool;

/// <summary>
/// Creates or removes <see cref="MarkerFileName"/> in Beat Saber's Plugins folder (same directory as MultiplayerChat.dll).
/// Multiplayer Chat enables SLZ companion signaling when that file exists.
/// </summary>
internal static class Program
{
    /// <summary>Must match <c>MultiplayerChat.Core.SlzMode.MarkerFileName</c>.</summary>
    private const string MarkerFileName = "SLZ.dat";

    private static int Main(string[] args)
    {
        try
        {
            var remove = args.Length > 0 && string.Equals(args[0], "--remove", StringComparison.OrdinalIgnoreCase);
            var pathArgs = remove ? TrimArgs(args, 1) : args;

            string pluginsDir;
            if (pathArgs.Length == 0)
            {
                pluginsDir = Directory.GetCurrentDirectory();
                Console.WriteLine($"Using current directory: {pluginsDir}");
            }
            else if (pathArgs.Length == 1)
            {
                pluginsDir = Path.GetFullPath(pathArgs[0]);
            }
            else
            {
                PrintUsage();
                return 2;
            }

            if (!Directory.Exists(pluginsDir))
            {
                Console.Error.WriteLine($"Directory does not exist: {pluginsDir}");
                return 3;
            }

            var markerPath = Path.Combine(pluginsDir, MarkerFileName);

            if (remove)
            {
                if (!File.Exists(markerPath))
                {
                    Console.WriteLine($"Nothing to do — file not found: {markerPath}");
                    return 0;
                }

                File.Delete(markerPath);
                Console.WriteLine($"Removed: {markerPath}");
                return 0;
            }

            File.WriteAllText(markerPath, $"SLZ marker for Multiplayer Chat — created {DateTime.UtcNow:u} UTC\r\n");
            Console.WriteLine($"Created: {markerPath}");
            Console.WriteLine("Restart Beat Saber if it is running. Multiplayer Chat loads SLZ mode when this file sits next to MultiplayerChat.dll.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string[] TrimArgs(string[] args, int skip)
    {
        if (skip <= 0 || skip >= args.Length)
            return Array.Empty<string>();
        var rest = new string[args.Length - skip];
        Array.Copy(args, skip, rest, 0, rest.Length);
        return rest;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            SlzMarkerTool — create or remove SLZ.dat for Multiplayer Chat (SLZ companion mode).

            The marker file must live in the same folder as MultiplayerChat.dll (usually Beat Saber\Plugins).

            Usage:
              SlzMarkerTool [plugins-folder]
                  Creates SLZ.dat in that folder. If omitted, uses the current directory.

              SlzMarkerTool --remove [plugins-folder]
                  Deletes SLZ.dat from that folder. If omitted, uses the current directory.

            Example:
              SlzMarkerTool "D:\Steam\steamapps\common\Beat Saber\Plugins"
            """);
    }
}
