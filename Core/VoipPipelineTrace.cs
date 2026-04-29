using MultiplayerChat;
using UnityEngine;

namespace MultiplayerChat.Core;

/// <summary>Verbose trace lines for diagnosing VHOT (send → queue → sequential play). Off by default — logging every chunk hurts frame time.</summary>
public static class VoipPipelineTrace
{
    /// <summary>Dev-only. When true, logs every outbound/inbound chunk (very noisy).</summary>
    public static bool Enabled;

    public static void TxChunk(int monoSamples, int sampleRate, int encodedBytes, string? micDevice)
    {
        if (!Enabled) return;
        Plugin.Log?.Info(
            $"[MPChat][VoIP][Tx] rt={Time.realtimeSinceStartup:F4} monoSamples={monoSamples} hz={sampleRate} encBytes={encodedBytes} mic={(micDevice ?? "default")}");
    }

    public static void TxDispatch(int plainVhotBytes, int encryptedBytes)
    {
        if (!Enabled) return;
        Plugin.Log?.Info(
            $"[MPChat][VoIP][Tx][Dispatch] rt={Time.realtimeSinceStartup:F4} vhotPlainB={plainVhotBytes} encB={encryptedBytes}");
    }

    public static void RxEnqueue(string userId, int queueDepthAfter, int blobBytes)
    {
        if (!Enabled) return;
        Plugin.Log?.Info(
            $"[MPChat][VoIP][Rx][Enqueue] rt={Time.realtimeSinceStartup:F4} uid={ShortId(userId)} depthAfter={queueDepthAfter} blobB={blobBytes}");
    }

    public static void RxMerge(string userId, int coalescedPackets, int pcmFrames, int channels, int rate, float estimatedDurationSec, int qDepthAfterMerge)
    {
        if (!Enabled) return;
        Plugin.Log?.Info(
            $"[MPChat][VoIP][Rx][Merge] rt={Time.realtimeSinceStartup:F4} uid={ShortId(userId)} coalescedPkts={coalescedPackets} pcmFrames={pcmFrames} ch={channels} rate={rate} estSec={estimatedDurationSec:F4} qAfter={qDepthAfterMerge}");
    }

    private static string ShortId(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "?";
        var id = userId!;
        return id.Length <= 12 ? id : id.Substring(0, 12) + "...";
    }
}
