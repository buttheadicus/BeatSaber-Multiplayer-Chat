using UnityEngine;

namespace MultiplayerChat.Core;

public static class VoiceChatAudioLevel
{
    public static bool ApplyStoredReceiveVolume = true;

    public static float GetVoiceChatPlaybackGain(string userId)
    {
        if (!ApplyStoredReceiveVolume)
            return 1f;
        return Mathf.Max(0f, PlayerVoiceVolumeStore.GetVolumePercent(userId) / 100f);
    }

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

    public static float GetVoiceChatPlaybackVolume01(string userId) => GetVoiceChatPlaybackGain(userId);
}
