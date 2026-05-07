using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MultiplayerChat.Core;

internal static class VoiceReceiveDiagnostics
{
    public static bool EnableVerboseChunkLogs;

    private const float DropLogThrottleSec = 1.0f;
    private const float HotMicChunkLogIntervalSec = 0.35f;
    private const int MaxScheduleDetailLogs = 12;

    private static readonly Dictionary<string, float> s_nextDropLog = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> s_nextHotMicChunkLog = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> s_nextVoiceMsgChunkLog = new(StringComparer.Ordinal);
    private static readonly HashSet<string> s_hotMicFirstChunkLogged = new(StringComparer.Ordinal);
    private static int s_scheduleDetailLogs;

    public static void ResetSession()
    {
        s_nextDropLog.Clear();
        s_nextHotMicChunkLog.Clear();
        s_nextVoiceMsgChunkLog.Clear();
        s_hotMicFirstChunkLogged.Clear();
        s_scheduleDetailLogs = 0;
    }

    public static void LogVoiceReceiveDropThrottled(string reason, string? detail = null)
    {
        if (VoiceBareStreamMode.Enabled) return;
        var key = reason + "|" + (detail ?? "");
        var now = Time.realtimeSinceStartup;
        if (s_nextDropLog.TryGetValue(key, out var t) && now < t) return;
        s_nextDropLog[key] = now + DropLogThrottleSec;
        MultiplayerChat.Plugin.Log?.Warn($"[MPChat][VoiceRx DROP] {reason}{(detail != null ? " " + detail : "")}");
    }

    public static void LogDecryptFailedWithFingerprintThrottled(string? senderUserId, string sessionStateFingerprint)
    {
        if (VoiceBareStreamMode.Enabled) return;
        const string key = "decrypt_failed_fingerprint";
        var now = Time.realtimeSinceStartup;
        if (s_nextDropLog.TryGetValue(key, out var t) && now < t) return;
        s_nextDropLog[key] = now + DropLogThrottleSec * 2f;
        var fp = sessionStateFingerprint.Length > 240 ? sessionStateFingerprint.Substring(0, 240) + "…" : sessionStateFingerprint;
        MultiplayerChat.Plugin.Log?.Warn(
            $"[MPChat][VoiceRx DROP] decrypt_failed (after retry) sender={ShortId(senderUserId)} keyState=[{fp}]");
    }

    public static void LogHotMicFirstChunkFromUser(string userId, int decryptedBytes)
    {
        if (VoiceBareStreamMode.Enabled) return;
        if (string.IsNullOrEmpty(userId)) return;
        if (!s_hotMicFirstChunkLogged.Add(userId)) return;
        MultiplayerChat.Plugin.Log?.Info($"[MPChat][HotMic] First VHOT chunk from {ShortId(userId)} ({decryptedBytes} bytes decrypted)");
    }

    public static bool ShouldLogHotMicChunkLine(string userId) => false;

    public static void LogHotMicChunkLine(string userId, int blobBytes, int volPct, float gain01, float peakDecode, float peakPostFade, int rate, int ch, int sampleCount)
    {
    }

    public static void LogHotMicDecodeFailed(string userId, int blobBytes)
    {
        MultiplayerChat.Plugin.Log?.Warn($"[MPChat][HotMicRx] TryDecodeToFloatSamples FAILED uid={ShortId(userId)} blob={blobBytes}B");
    }

    public static bool ShouldLogVoiceMessageChunkLine(string userId)
    {
        if (!EnableVerboseChunkLogs || VoiceBareStreamMode.Enabled) return false;
        var now = Time.realtimeSinceStartup;
        if (s_nextVoiceMsgChunkLog.TryGetValue(userId, out var next) && now < next) return false;
        s_nextVoiceMsgChunkLog[userId] = now + HotMicChunkLogIntervalSec;
        return true;
    }

    public static void LogVoiceMessageChunkLine(string userId, int volPct, float gain01, float peakDecode, int rate, int ch, int sampleCount)
    {
        if (VoiceBareStreamMode.Enabled) return;
        MultiplayerChat.Plugin.Log?.Info(
            $"[MPChat][VoiceMsgRx] uid={ShortId(userId)} vol%={volPct} srcVol01={gain01:F4} peak={peakDecode:F6} {rate}Hz ch={ch} n={sampleCount}");
    }

    public static void MaybeLogHotMicSchedule(double dspNow, double playAt, float lenSec, int srcIndex, int clipSamples, int clipHz)
    {
        if (VoiceBareStreamMode.Enabled) return;
        if (s_scheduleDetailLogs >= MaxScheduleDetailLogs) return;
        s_scheduleDetailLogs++;
        MultiplayerChat.Plugin.Log?.Info(
            $"[MPChat][HotMicSched] #{s_scheduleDetailLogs} dspNow={dspNow:F4} playAt={playAt:F4} delta={(playAt - dspNow) * 1000:F1}ms len={lenSec:F4}s src={srcIndex} clip={clipSamples}@{clipHz}Hz");
    }

    public static void LogFilterSnapshotForBlockedSender(string senderUserId)
    {
        if (VoiceBareStreamMode.Enabled) return;
        // One throttle key: otherwise each sender produces a new key and spam floods the log.
        var key = "incoming_voice_filter_blocked_global";
        var now = Time.realtimeSinceStartup;
        if (s_nextDropLog.TryGetValue(key, out var t) && now < t) return;
        s_nextDropLog[key] = now + DropLogThrottleSec;

        var sb = new StringBuilder();
        sb.Append("ShouldPlayIncomingVoiceFrom=false blocked sender=").Append(ShortId(senderUserId));
        if (VoiceChatRuntimeState.IsTalkToActive)
        {
            sb.Append(" talkToActive=true ids=");
            AppendTruncatedIds(sb, VoiceChatRuntimeState.TalkToUserIds);
        }
        else sb.Append(" talkToActive=false");

        if (VoiceChatRuntimeState.IsListenFilterActive)
        {
            sb.Append(" listenActive=true ids=");
            AppendTruncatedIds(sb, VoiceChatRuntimeState.ListenUserIds);
        }
        else sb.Append(" listenActive=false");

        MultiplayerChat.Plugin.Log?.Warn($"[MPChat][VoiceRx DROP] {sb}");
    }

    private static void AppendTruncatedIds(StringBuilder sb, IReadOnlyCollection<string> ids)
    {
        var n = 0;
        foreach (var id in ids)
        {
            if (n++ > 0) sb.Append(',');
            if (n > 6)
            {
                sb.Append("…");
                break;
            }
            sb.Append(ShortId(id));
        }
    }

    private static string ShortId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "?";
        var s = id!;
        return s.Length <= 14 ? s : s.Substring(0, 14) + "…";
    }
}
