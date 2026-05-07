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

public class LearnedChatIdsStore : IInitializable
{
    private readonly Dictionary<string, string> _platformUserIdToChatId = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private bool _loaded;

    public void Initialize()
    {
        LoadFromDisk();
    }

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
                        _platformUserIdToChatId[e.PlatformUserId!] = e.ChatId!;
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
        if (string.IsNullOrEmpty(platformUserId) || !ChatPersistentId.IsValidFormat(chatId))
        {
            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.LearnedStoreBlock(
                    "SetMapping REJECT invalid args.\n" +
                    "platformUserId empty=" + string.IsNullOrEmpty(platformUserId) +
                    " chatIdValidFormat=" + ChatPersistentId.IsValidFormat(chatId) + '\n' +
                    "chatId literal=" + (chatId ?? "(null)") + '\n' +
                    "chatId charCodes=" + MpChatVerboseDebug.CharCodes(chatId) + '\n' +
                    "Stack:\n" + Environment.StackTrace);
            return;
        }

        lock (_lock)
        {
            var hadPrior = _platformUserIdToChatId.TryGetValue(platformUserId, out var prior);
            if (hadPrior && prior == chatId)
            {
                if (MpChatVerboseDebug.IsOn)
                    MpChatVerboseDebug.LearnedStoreBlock(
                        "SetMapping no-op (already mapped).\nplatformUserId=" +
                        MpChatVerboseDebug.TruncPlatformUserId(platformUserId) + "\nchatId=" + chatId);
                return;
            }

            _platformUserIdToChatId[platformUserId] = chatId;

            if (MpChatVerboseDebug.IsOn)
            {
                var sb = new StringBuilder(2048);
                sb.Append("SetMapping WRITE (memory); calling SaveToDisk after lock.\n");
                sb.Append("platformUserId=").Append(MpChatVerboseDebug.TruncPlatformUserId(platformUserId)).Append('\n');
                sb.Append("prior=").Append(hadPrior ? prior : "(none)").Append(" new=").Append(chatId).Append('\n');
                sb.Append("prior official=").Append(hadPrior && ChatPersistentId.IsOfficialTaggedChatId(prior))
                    .Append(" new official=").Append(ChatPersistentId.IsOfficialTaggedChatId(chatId)).Append('\n');
                sb.Append("FULL TABLE snapshot count=").Append(_platformUserIdToChatId.Count).Append('\n');
                foreach (var kv in _platformUserIdToChatId.OrderBy(k => k.Key, StringComparer.Ordinal))
                    sb.Append("  ").Append(kv.Key).Append(" -> ").Append(kv.Value).Append('\n');
                sb.Append("Stack:\n").Append(Environment.StackTrace);
                MpChatVerboseDebug.LearnedStoreBlock(sb.ToString());
            }
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
