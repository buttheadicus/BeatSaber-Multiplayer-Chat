namespace MultiplayerChat.Core;

internal static class MpChatFeatures
{
    internal static readonly bool LobbyCustomAvatars = true;

    internal static readonly bool LobbyCustomAvatarsInArena = true;

    // Mirror/local pedestal: Custom Avatars TrackingRig when multiplayer bones stay static (FPFC / deck).
    internal static readonly bool LobbyUseCustomAvatarTrackingRig = true;

    // Remote pedestals: wait for separated head/hand bones before driving custom avatars.
    internal static readonly bool LobbyDeferCollapsedRemoteBones = true;
}
