using UnityEngine;

namespace MultiplayerChat.Core;

/// <summary>
/// Receive-side gain for multiplayer voice (hot mic + voice messages). Stored levels are 0–500
/// (displayed as 0.0–5.0); applied by scaling PCM before <see cref="UnityEngine.AudioSource"/> at volume 1.
/// </summary>
public static class VoiceChatAudioLevel
{
    /// <summary>When true, use <see cref="PlayerVoiceVolumeStore"/> per sender. When false, unity gain (samples unchanged here).</summary>
    public static bool ApplyStoredReceiveVolume = true;

    /// <summary>Linear gain 0–5 from stored percent (100 = nominal 1×).</summary>
    public static float GetVoiceChatPlaybackGain(string userId)
    {
        if (!ApplyStoredReceiveVolume)
            return 1f;
        return Mathf.Clamp(PlayerVoiceVolumeStore.GetVolumePercent(userId) / 100f, 0f, 5f);
    }

    /// <summary>Multiplies decoded float samples in-place (used so gain can exceed Unity's usual 0–1 source volume).</summary>
    public static void ApplyReceiveGainToSamples(float[] samples, string userId)
    {
        if (samples == null || samples.Length == 0) return;
        var g = GetVoiceChatPlaybackGain(userId);
        if (g <= 0f)
        {
            for (var i = 0; i < samples.Length; i++)
                samples[i] = 0f;
            return;
        }

        for (var i = 0; i < samples.Length; i++)
            samples[i] *= g;
    }

    /// <summary>Kept for diagnostics; same as <see cref="GetVoiceChatPlaybackGain"/>.</summary>
    public static float GetVoiceChatPlaybackVolume01(string userId) => GetVoiceChatPlaybackGain(userId);
}
