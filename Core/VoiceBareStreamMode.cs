namespace MultiplayerChat.Core;

/// <summary>
/// When <see cref="Enabled"/> is true, throttled receive diagnostics
/// (<c>[VoiceRx DROP]</c>, <c>[HotMicRx]</c>, chunk lines, schedule detail) are suppressed; decode-fail still logs.
/// </summary>
/// <remarks>Default <c>false</c> so diagnostics stay on; set <c>true</c> to quiet noisy logs during experiments.</remarks>
public static class VoiceBareStreamMode
{
    public static bool Enabled = false;
}
