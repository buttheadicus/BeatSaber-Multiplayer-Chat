using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using MultiplayerChat.Settings;
using Zenject;

namespace MultiplayerChat.Core;

public class ChatIdConfigStore : IInitializable
{
    private readonly object _lock = new();
    private readonly HashSet<string> _mutedChatIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mutedPlatformUserIds = new(StringComparer.Ordinal);
    private bool _loaded;

    public event Action? MutedStateChanged;

    public void Initialize()
    {
        LoadFromDisk();
    }

    private static Stream? OpenChatIdConfigJsonStream(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length == 0)
            return null;

        var asText = Encoding.UTF8.GetString(raw).TrimStart();
        if (asText.StartsWith("{", StringComparison.Ordinal))
            return new MemoryStream(raw);

        try
        {
            var plain = ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
            return new MemoryStream(plain);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ChatIDConfig.dat decrypt failed ({ex.Message}).");
            return null;
        }
    }

    public void LoadFromDisk()
    {
        lock (_lock)
        {
            _mutedChatIds.Clear();
            _mutedPlatformUserIds.Clear();
            var path = ChatIdFilePaths.ChatIdConfigFilePath;
            if (!File.Exists(path))
            {
                _loaded = true;
                return;
            }

            try
            {
                Stream? jsonStream = OpenChatIdConfigJsonStream(path);
                if (jsonStream == null)
                {
                    _loaded = true;
                    return;
                }

                using (jsonStream)
                {
                    var ser = new DataContractJsonSerializer(typeof(ChatIdConfigData));
                    var obj = ser.ReadObject(jsonStream) as ChatIdConfigData;
                    if (obj != null)
                    {
                        foreach (var id in obj.MutedChatIds ?? Enumerable.Empty<string>())
                            if (ChatPersistentId.IsValidFormat(id))
                                _mutedChatIds.Add(id);
                        foreach (var pid in obj.MutedPlatformUserIds ?? Enumerable.Empty<string>())
                            if (!string.IsNullOrEmpty(pid))
                                _mutedPlatformUserIds.Add(pid);
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ChatIDConfig.dat load failed: {ex.Message}");
            }

            _loaded = true;
        }
    }

    public bool IsMutedChatId(string chatId)
    {
        if (!ChatPersistentId.IsValidFormat(chatId)) return false;
        lock (_lock) return _mutedChatIds.Contains(chatId);
    }

    public bool IsMutedPlatformUserId(string platformUserId)
    {
        if (string.IsNullOrEmpty(platformUserId)) return false;
        lock (_lock) return _mutedPlatformUserIds.Contains(platformUserId);
    }

    public bool HasAnyMutedEntry()
    {
        lock (_lock) return _mutedChatIds.Count > 0 || _mutedPlatformUserIds.Count > 0;
    }

    public void ClearAllMutes()
    {
        lock (_lock)
        {
            if (_mutedChatIds.Count == 0 && _mutedPlatformUserIds.Count == 0)
                return;
            _mutedChatIds.Clear();
            _mutedPlatformUserIds.Clear();
        }

        SaveToDisk();
        MutedStateChanged?.Invoke();
    }

    public void ToggleMutedChatId(string chatId)
    {
        if (!ChatPersistentId.IsValidFormat(chatId)) return;
        lock (_lock)
        {
            if (!_mutedChatIds.Remove(chatId))
                _mutedChatIds.Add(chatId);
        }

        SaveToDisk();
        MutedStateChanged?.Invoke();
    }

    public void ToggleMutedPlatformUserId(string platformUserId)
    {
        if (string.IsNullOrEmpty(platformUserId)) return;
        lock (_lock)
        {
            if (!_mutedPlatformUserIds.Remove(platformUserId))
                _mutedPlatformUserIds.Add(platformUserId);
        }

        SaveToDisk();
        MutedStateChanged?.Invoke();
    }

    public void OnChatIdLearnedForUser(string platformUserId, string chatId)
    {
        if (string.IsNullOrEmpty(platformUserId) || !ChatPersistentId.IsValidFormat(chatId)) return;
        lock (_lock)
        {
            if (_mutedPlatformUserIds.Remove(platformUserId))
                _mutedChatIds.Add(chatId);
        }

        SaveToDisk();
        MutedStateChanged?.Invoke();
    }

    public void SaveToDisk()
    {
        lock (_lock)
        {
            if (!_loaded) return;

            try
            {
                Directory.CreateDirectory(ChatIdFilePaths.RootDirectory);
                var data = new ChatIdConfigData
                {
                    SchemaVersion = 1,
                    MutedChatIds = _mutedChatIds.OrderBy(s => s).ToList(),
                    MutedPlatformUserIds = _mutedPlatformUserIds.OrderBy(s => s).ToList()
                };

                using var ms = new MemoryStream();
                var ser = new DataContractJsonSerializer(typeof(ChatIdConfigData));
                ser.WriteObject(ms, data);

                var path = ChatIdFilePaths.ChatIdConfigFilePath;
                var tmp = path + ".tmp";
                var jsonBytes = ms.ToArray();
                var protectedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(tmp, protectedBytes);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Error($"[MPChat] ChatIDConfig.dat save failed: {ex}");
            }
        }
    }
}
