using System;
using System.Collections.Generic;
using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

// Layers push-to-talk and optional named suppressors over outbound capture logic so outbound voice stops when PTT is not held.
// VoiceHotMicManager refreshes hold state on a throttled cadence (LastPolledPushToTalkHeld). Chunk sends still evaluate suppressors every poll tick.
// Extra suppressors are for future policies (for example song-phase mute) without toggling the lobby mute button state.
public static class VoiceDynamicTransmitGate
{
    private static readonly HashSet<string> ExtraSuppressors = new(StringComparer.Ordinal);

    private static bool _lastPolledPushToTalkHeld;

    // Cached binding poll updated by VoiceHotMicManager (throttled, not necessarily each Unity frame).
    public static bool LastPolledPushToTalkHeld => _lastPolledPushToTalkHeld;

    internal static void NotifyPushToTalkHeld(bool held) => _lastPolledPushToTalkHeld = held;

    public static void SetSuppressor(string id, bool active)
    {
        if (string.IsNullOrEmpty(id))
            return;
        if (active)
            ExtraSuppressors.Add(id);
        else
            ExtraSuppressors.Remove(id);
    }

    public static bool ShouldSuppressOutboundVoice()
    {
        if (ExtraSuppressors.Count > 0)
            return true;
        if (!ModSettings.PushToTalkEnabled)
            return false;
        return !_lastPolledPushToTalkHeld;
    }
}
