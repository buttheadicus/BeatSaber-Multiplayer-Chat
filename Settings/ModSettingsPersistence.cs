using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MultiplayerChat.Core;

namespace MultiplayerChat.Settings;

internal static class ModSettingsPersistence
{
    private static readonly object Gate = new();

    private static Data? _cache;

    private static string FilePath => ChatIdFilePaths.ModSettingsFilePath;

    internal sealed class AddonsSettingsSection
    {
        [JsonProperty("enableAvatarColoringExtensions")]
        public bool EnableAvatarColoringExtensions { get; set; }
    }

    internal sealed class PerformanceSettingsSection
    {
    }

    internal sealed class Data
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 2;

        [JsonProperty("bubbleDuration")] public float BubbleDuration { get; set; } = 15f;

        [JsonProperty("showSystemMessages")] public bool ShowSystemMessages { get; set; } = true;

        [JsonProperty("nameColor")] public string NameColor { get; set; } = "87CEEB";

        [JsonProperty("customPlacement")] public bool CustomPlacement { get; set; }

        [JsonProperty("lobbyChatPosX")] public float LobbyChatPosX { get; set; }

        [JsonProperty("lobbyChatPosY")] public float LobbyChatPosY { get; set; }

        [JsonProperty("chatBubbleSoundsEnabled")] public bool ChatBubbleSoundsEnabled { get; set; } = true;

        [JsonProperty("micInputDeviceName")] public string MicInputDeviceName { get; set; } = "";

        [JsonProperty("pushToTalkEnabled")] public bool PushToTalkEnabled { get; set; }

        [JsonProperty("pttBindingIndex")] public int PttBindingIndex { get; set; }

        [JsonProperty("voiceDuckingEnabled")] public bool VoiceDuckingEnabled { get; set; }

        [JsonProperty("voiceDuckTargetPercent")] public int VoiceDuckTargetPercent { get; set; } = 35;

        [JsonProperty("muteMicDuringSongPlaying")] public bool MuteMicDuringSongPlaying { get; set; }

        [JsonProperty("deafDuringSongPlaying")] public bool DeafDuringSongPlaying { get; set; }

        [JsonProperty("enableAvatarExtensions")] public bool EnableAvatarExtensions { get; set; }

        [JsonProperty("enableLobbyCustomAvatars")] public bool EnableLobbyCustomAvatars { get; set; }

        [JsonProperty("lobbyCustomAvatarRelativePath")] public string LobbyCustomAvatarRelativePath { get; set; } = "";

        [JsonProperty("lobbyCustomAvatarContentHash")] public string LobbyCustomAvatarContentHash { get; set; } = "";

        [JsonProperty("enableCau")] public bool EnableCau { get; set; }

        [JsonProperty("debugLogging")] public bool DebugLogging { get; set; }

        [JsonProperty("addons")] public AddonsSettingsSection Addons { get; set; } = null!;

        [JsonProperty("performance")] public PerformanceSettingsSection Performance { get; set; } = null!;
    }

    private sealed class LegacyModFlagsDto
    {
        [JsonProperty("enableCau")] public bool EnableCau;
    }

    private static class LegacyKeys
    {
        internal const string BubbleDuration = "MultiplayerChat.BubbleDuration";
        internal const string ShowSystemMessages = "MultiplayerChat.ShowSystemMessages";
        internal const string NameColor = "MultiplayerChat.NameColor";
        internal const string CustomPlacement = "MultiplayerChat.CustomPlacement";
        internal const string LobbyChatPosX = "MultiplayerChat.LobbyChatPosX";
        internal const string LobbyChatPosY = "MultiplayerChat.LobbyChatPosY";
        internal const string ChatBubbleSounds = "MultiplayerChat.ChatBubbleSounds";
        internal const string MicInputDevice = "MultiplayerChat.MicInputDevice";
        internal const string PushToTalk = "MultiplayerChat.PushToTalk";
        internal const string PttBinding = "MultiplayerChat.PttBinding";
        internal const string VoiceDuckEnabled = "MultiplayerChat.VoiceDuckEnabled";
        internal const string VoiceDuckTargetPercent = "MultiplayerChat.VoiceDuckTargetPercent";
        internal const string MuteMicDuringSongPlaying = "MultiplayerChat.MuteMicDuringSongPlaying";
        internal const string DeafDuringSongPlaying = "MultiplayerChat.DeafDuringSongPlaying";
        internal const string EnableAvatarExtensions = "MultiplayerChat.EnableAvatarExtensions";
    }

    private const string LegacyModFlagsFileName = "MultiplayerChat.ModFlags.json";

    internal static Data Instance
    {
        get
        {
            lock (Gate)
            {
                if (_cache != null)
                    return _cache;

                try
                {
                    var path = FilePath;
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var jo = JObject.Parse(json);
                        var dto = jo.ToObject<Data>();
                        if (dto != null && dto.SchemaVersion >= 1)
                        {
                            CoalesceAddonsPerformanceSections(dto, jo);
                            Normalize(dto);
                            _cache = dto;
                            PushNameColorToInstallExtensions(_cache.NameColor);
                            return _cache;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Settings file read failed, migrating: {ex.Message}");
                }

                _cache = MigrateFromLegacy();
                Normalize(_cache);
                SaveLocked();
                PushNameColorToInstallExtensions(_cache.NameColor);
                return _cache;
            }
        }
    }

    internal static void Save()
    {
        lock (Gate)
        {
            if (_cache == null)
                return;
            SaveLocked();
        }
    }

    private static void SaveLocked()
    {
        if (_cache == null)
            return;

        try
        {
            Directory.CreateDirectory(ChatIdFilePaths.RootDirectory);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_cache, Formatting.Indented));
            PushNameColorToInstallExtensions(_cache.NameColor);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Settings file write failed: {ex.Message}");
        }
    }

    private static void CoalesceAddonsPerformanceSections(Data d, JObject? sourceJo)
    {
        d.Addons ??= new AddonsSettingsSection();
        d.Performance ??= new PerformanceSettingsSection();

        if (sourceJo == null)
        {
            if (d.SchemaVersion < 2)
                d.SchemaVersion = 2;
            return;
        }

        var addonsObj = sourceJo["addons"] as JObject;
        var nestedDefined = addonsObj != null &&
                            addonsObj.TryGetValue("enableAvatarColoringExtensions", out var nestedTok) &&
                            nestedTok != null &&
                            nestedTok.Type != JTokenType.Null &&
                            nestedTok.Type != JTokenType.Undefined;

        if (!nestedDefined &&
            sourceJo.TryGetValue("enableAvatarColoringExtensions", out var legacyTok) &&
            legacyTok != null &&
            legacyTok.Type != JTokenType.Null &&
            legacyTok.Type != JTokenType.Undefined)
        {
            try
            {
                d.Addons.EnableAvatarColoringExtensions = legacyTok.Value<bool>();
            }
            catch
            {
                // ignore bad legacy token
            }
        }

        if (d.SchemaVersion < 2)
            d.SchemaVersion = 2;
    }

    private static void Normalize(Data d)
    {
        d.Addons ??= new AddonsSettingsSection();
        d.Performance ??= new PerformanceSettingsSection();

        d.BubbleDuration = Mathf.Clamp(d.BubbleDuration, 15f, 60f);
        d.PttBindingIndex = Mathf.Clamp(d.PttBindingIndex, 0, 3);
        d.VoiceDuckTargetPercent = Mathf.Clamp(d.VoiceDuckTargetPercent, 5, 100);
        var hex = (d.NameColor ?? "").Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length > 6) hex = hex.Substring(0, 6);
        if (hex.Length != 6 || !IsValidHex(hex))
            hex = "87CEEB";
        d.NameColor = hex;

        var desc = (d.LobbyCustomAvatarRelativePath ?? "").Trim();
        if (desc.Length > 260)
            desc = desc.Substring(0, 260);
        d.LobbyCustomAvatarRelativePath = desc.Replace('\\', '/');

        var hash = (d.LobbyCustomAvatarContentHash ?? "").Trim().ToUpperInvariant();
        if (hash.Length > 32)
            hash = hash.Substring(0, 32);
        d.LobbyCustomAvatarContentHash = CustomAvatarHashUtil.LooksLikeMd5Hex(hash) ? hash : "";
    }

    private static void PushNameColorToInstallExtensions(string normalizedHex6)
    {
        try
        {
            if (string.IsNullOrEmpty(normalizedHex6) || normalizedHex6.Length != 6 || !IsValidHex(normalizedHex6))
                return;
            MultiplayerExtensionsJson.SetPlayerColorHex(normalizedHex6);
        }
        catch
        {
            // ignore
        }
    }

    private static bool IsValidHex(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 6) return false;
        foreach (var c in s)
            if (!char.IsDigit(c) && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
                return false;
        return true;
    }

    private static Data MigrateFromLegacy()
    {
        var d = new Data
        {
            SchemaVersion = 2,
            Addons = new AddonsSettingsSection(),
            Performance = new PerformanceSettingsSection()
        };

        if (PlayerPrefs.HasKey(LegacyKeys.BubbleDuration))
            d.BubbleDuration = PlayerPrefs.GetFloat(LegacyKeys.BubbleDuration);

        d.ShowSystemMessages = !PlayerPrefs.HasKey(LegacyKeys.ShowSystemMessages) ||
                               PlayerPrefs.GetInt(LegacyKeys.ShowSystemMessages) != 0;

        var fromMpex = MultiplayerExtensionsJson.GetPlayerColorHex();
        if (fromMpex is { Length: 6 } && IsValidHex(fromMpex))
            d.NameColor = fromMpex;
        else
        {
            var migratedName = PlayerPrefs.GetString(LegacyKeys.NameColor, "");
            if (!string.IsNullOrEmpty(migratedName))
            {
                var h = migratedName.Trim();
                if (h.StartsWith("#")) h = h.Substring(1);
                if (h.Length == 6 && IsValidHex(h))
                    d.NameColor = h;
            }
        }

        d.CustomPlacement = PlayerPrefs.HasKey(LegacyKeys.CustomPlacement) &&
                            PlayerPrefs.GetInt(LegacyKeys.CustomPlacement) != 0;

        d.LobbyChatPosX = PlayerPrefs.GetFloat(LegacyKeys.LobbyChatPosX, 0f);
        d.LobbyChatPosY = PlayerPrefs.GetFloat(LegacyKeys.LobbyChatPosY, 0f);

        d.ChatBubbleSoundsEnabled = !PlayerPrefs.HasKey(LegacyKeys.ChatBubbleSounds) ||
                                    PlayerPrefs.GetInt(LegacyKeys.ChatBubbleSounds) != 0;

        d.MicInputDeviceName = PlayerPrefs.GetString(LegacyKeys.MicInputDevice, "");

        d.PushToTalkEnabled = PlayerPrefs.HasKey(LegacyKeys.PushToTalk) &&
                              PlayerPrefs.GetInt(LegacyKeys.PushToTalk) != 0;

        d.PttBindingIndex = PlayerPrefs.GetInt(LegacyKeys.PttBinding, 0);

        d.VoiceDuckingEnabled = PlayerPrefs.HasKey(LegacyKeys.VoiceDuckEnabled) &&
                                PlayerPrefs.GetInt(LegacyKeys.VoiceDuckEnabled) != 0;

        d.VoiceDuckTargetPercent = PlayerPrefs.GetInt(LegacyKeys.VoiceDuckTargetPercent, 35);

        d.MuteMicDuringSongPlaying = PlayerPrefs.HasKey(LegacyKeys.MuteMicDuringSongPlaying) &&
                                     PlayerPrefs.GetInt(LegacyKeys.MuteMicDuringSongPlaying) != 0;

        d.DeafDuringSongPlaying = PlayerPrefs.HasKey(LegacyKeys.DeafDuringSongPlaying) &&
                                  PlayerPrefs.GetInt(LegacyKeys.DeafDuringSongPlaying) != 0;

        d.EnableAvatarExtensions = PlayerPrefs.HasKey(LegacyKeys.EnableAvatarExtensions) &&
                                   PlayerPrefs.GetInt(LegacyKeys.EnableAvatarExtensions) != 0;

        try
        {
            var flagsPath = Path.Combine(ChatIdFilePaths.RootDirectory, LegacyModFlagsFileName);
            if (File.Exists(flagsPath))
            {
                var fj = File.ReadAllText(flagsPath);
                var flags = JsonConvert.DeserializeObject<LegacyModFlagsDto>(fj);
                if (flags?.EnableCau == true)
                    d.EnableCau = true;
            }
        }
        catch
        {
            // ignore legacy flags read failure
        }

        return d;
    }
}
