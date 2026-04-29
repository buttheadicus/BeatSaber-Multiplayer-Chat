namespace MultiplayerChat.Core;

/// <summary>Hot-mic capture tuning (VHOT wire format and encryption live in <see cref="VoiceHotMicCodec"/> / <see cref="ChatManager"/>).</summary>
/// <remarks>
/// <b>Voice activity gate is mandatory and not optional.</b> Do not reintroduce a <c>Disable*</c> flag or any bypass in
/// <see cref="VoiceHotMicManager"/>  -  gating stays always on; past toggles caused regressions and confusion.
/// </remarks>
public static class VoiceHotMicTransport
{
    /// <summary>
    /// Chunks to drop after <see cref="Microphone.Start"/> before the first VHOT send. Kept at <c>0</c> so we do not
    /// mute the start of capture (testing whether warmup was contributing to &quot;cannot hear players&quot;).
    /// </summary>
    public static int WarmupChunksToSkip;
}
