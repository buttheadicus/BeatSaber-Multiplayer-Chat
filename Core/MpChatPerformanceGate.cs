using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

internal static class MpChatPerformanceGate
{
    // Spawn, load, disk cache, and avatar file transfer (never during active beatmap).
    public static bool ShouldBlockAvatarHeavyWork =>
        MpChatLobbyDiagnostics.BeatmapGameplayLikelyActive();

    // Arena: keep custom avatars on waiting platforms and in-song; defer only lobby pedestal work.
    public static bool ShouldBlockAvatarHeavyWorkForDriver(bool arenaContext) =>
        !arenaContext && ShouldBlockAvatarHeavyWork;

    public static bool ShouldDeferLobbyPedestalAvatarRefresh =>
        ShouldBlockAvatarHeavyWork ||
        (ModSettings.LimitIncomingAvatarDataDuringSongs &&
         MpChatLobbyDiagnostics.SongGameplayLikelyActive());

    public static bool ShouldDeferIncomingAvatarData => ShouldDeferLobbyPedestalAvatarRefresh;

    public static bool CanRunLobbyAvatarFileTransfer =>
        !ShouldBlockAvatarHeavyWork &&
        MpChatFeatures.LobbyCustomAvatars &&
        (MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby() ||
         MpChatLobbyDiagnostics.AnyGameCoreLoaded());

    // Buffer incoming chunks in memory during songs; disk cache flush uses PollDeferredCacheWrites.
    public static bool CanAcceptLobbyAvatarFileChunks =>
        MpChatFeatures.LobbyCustomAvatars && ModSettings.EnableLobbyCustomAvatars;
}
