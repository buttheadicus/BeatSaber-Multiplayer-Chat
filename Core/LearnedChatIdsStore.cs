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

/// <summary>
/// Persists Beat Saber platform <c>userId</c> → other players' 8-digit chat IDs in
/// <see cref="ChatIdFilePaths.LearnedIdsFilePath"/> (DPAPI-encrypted JSON, same folder as ChatID / ChatIDConfig).
/// Loaded at multiplayer init; updated whenever we learn or confirm an ID.
/// </summary>
public class LearnedChatIdsStore : IInitializable
{
    private readonly Dictionary<string, string> _platformUserIdToChatId = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private bool _loaded;

    public void Initialize()
    {
        LoadFromDisk();
    }

    /// <summary>Plain JSON (legacy) or DPAPI blob, matching <see cref="ChatIdConfigStore"/>.</summary>
    private static Stream? OpenLearnedIdsJsonStream(byte[] raw)
    {
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
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] LearnedIDs.dat decrypt failed ({ex.Message}).");
            return null;
        }
    }

    private void LoadFromDisk()
    {
        lock (_lock)
        {
            _platformUserIdToChatId.Clear();
            var path = ChatIdFilePaths.LearnedIdsFilePath;
            if (!File.Exists(path))
            {
                _loaded = true;
                return;
            }

            try
            {
                var raw = File.ReadAllBytes(path);
                using var jsonStream = OpenLearnedIdsJsonStream(raw);
                if (jsonStream == null)
                {
                    _loaded = true;
                    return;
                }

                using (jsonStream)
                {
                    var ser = new DataContractJsonSerializer(typeof(LearnedChatIdsData));
                    var obj = ser.ReadObject(jsonStream) as LearnedChatIdsData;
                    if (obj?.Entries == null)
                    {
                        _loaded = true;
                        return;
                    }

                    foreach (var e in obj.Entries)
                    {
                        if (string.IsNullOrEmpty(e.PlatformUserId) || !ChatPersistentId.IsValidFormat(e.ChatId))
                            continue;
                        _platformUserIdToChatId[e.PlatformUserId] = e.ChatId!;
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] LearnedIDs.dat load failed: {ex.Message}");
            }

            _loaded = true;
        }
    }

    public bool TryGetChatId(string platformUserId, out string chatId)
    {
        chatId = "";
        if (string.IsNullOrEmpty(platformUserId)) return false;
        lock (_lock)
        {
            return _platformUserIdToChatId.TryGetValue(platformUserId, out chatId) && !string.IsNullOrEmpty(chatId);
        }
    }

    public void SetMapping(string platformUserId, string chatId)
    {
        if (string.IsNullOrEmpty(platformUserId) || !ChatPersistentId.IsValidFormat(chatId)) return;

        lock (_lock)
        {
            if (_platformUserIdToChatId.TryGetValue(platformUserId, out var existing) && existing == chatId)
                return;
            _platformUserIdToChatId[platformUserId] = chatId;
        }

        SaveToDisk();
    }

    public void SaveToDisk()
    {
        lock (_lock)
        {
            if (!_loaded) return;

            try
            {
                Directory.CreateDirectory(ChatIdFilePaths.RootDirectory);
                var data = new LearnedChatIdsData
                {
                    SchemaVersion = 1,
                    Entries = _platformUserIdToChatId
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => new LearnedIdEntry { PlatformUserId = kv.Key, ChatId = kv.Value })
                        .ToList()
                };

                using var ms = new MemoryStream();
                var ser = new DataContractJsonSerializer(typeof(LearnedChatIdsData));
                ser.WriteObject(ms, data);

                var path = ChatIdFilePaths.LearnedIdsFilePath;
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
                MultiplayerChat.Plugin.Log?.Error($"[MPChat] LearnedIDs.dat save failed: {ex}");
            }
        }
    }
}
