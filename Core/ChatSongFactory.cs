using System;
using System.IO;
using System.Text;
using UnityEngine;

// if you have a golden ticket you can visit my factory, whats that? you do? i dont believe you, but you can look at visit it anyway since i feel pity for you. scroll down now.

namespace MultiplayerChat.Core;

internal static class ChatSongFactory
{
    private const string FolderName = "chat song 1";
    private const string SongFileName = "song.egg";
    private const string CoverFileName = "cover.png";
    private const string BeatmapFileName = "EasyStandard.dat";

    internal static string CustomLevelsPath =>
        Path.Combine(Application.dataPath, "CustomLevels");

    internal static bool TryCreate(string message, out string status)
    {
        var text = (message ?? "").Trim();
        if (string.IsNullOrEmpty(text))
        {
            status = "type a message first";
            return false;
        }

        try
        {
            var root = CustomLevelsPath;
            Directory.CreateDirectory(root);
            var folder = Path.Combine(root, FolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(Path.Combine(folder, "Info.dat"), BuildInfoDat(text), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, BeatmapFileName), EmptyBeatmapJson, new UTF8Encoding(false));

            var songPath = Path.Combine(folder, SongFileName);
            if (!File.Exists(songPath))
                File.WriteAllBytes(songPath, Array.Empty<byte>());

            var coverPath = Path.Combine(folder, CoverFileName);
            if (!File.Exists(coverPath))
                File.WriteAllBytes(coverPath, TinyPng);

            SongCoreRefresh.TryRefresh(out var refreshNote);
            status = "created " + FolderName;
            if (!string.IsNullOrEmpty(refreshNote))
                status += "; " + refreshNote;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Error("[MPChat][Experimental] chat song failed: " + ex.Message);
            status = "failed: " + ex.Message;
            return false;
        }
    }

    private static string BuildInfoDat(string songName)
    {
        var escaped = EscapeJson(songName);
        return
            "{\n" +
            "  \"_version\": \"2.0.0\",\n" +
            "  \"_songName\": \"" + escaped + "\",\n" +
            "  \"_songSubName\": \"Chat Message\",\n" +
            "  \"_songAuthorName\": \"MultiplayerChat\",\n" +
            "  \"_levelAuthorName\": \"MultiplayerChat\",\n" +
            "  \"_beatsPerMinute\": 120,\n" +
            "  \"_shuffle\": 0,\n" +
            "  \"_shufflePeriod\": 0.5,\n" +
            "  \"_previewStartTime\": 0,\n" +
            "  \"_previewDuration\": 1,\n" +
            "  \"_songFilename\": \"" + SongFileName + "\",\n" +
            "  \"_coverImageFilename\": \"" + CoverFileName + "\",\n" +
            "  \"_environmentName\": \"DefaultEnvironment\",\n" +
            "  \"_songTimeOffset\": 0,\n" +
            "  \"_difficultyBeatmapSets\": [\n" +
            "    {\n" +
            "      \"_beatmapCharacteristicName\": \"Standard\",\n" +
            "      \"_difficultyBeatmaps\": [\n" +
            "        {\n" +
            "          \"_difficulty\": \"Easy\",\n" +
            "          \"_difficultyRank\": 1,\n" +
            "          \"_beatmapFilename\": \"" + BeatmapFileName + "\",\n" +
            "          \"_noteJumpMovementSpeed\": 10,\n" +
            "          \"_noteJumpStartBeatOffset\": 0\n" +
            "        }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";
    }

    private const string EmptyBeatmapJson =
        "{\n  \"_version\": \"2.0.0\",\n  \"_notes\": [],\n  \"_obstacles\": [],\n  \"_events\": []\n}\n";

    private static readonly byte[] TinyPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    };

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
}
