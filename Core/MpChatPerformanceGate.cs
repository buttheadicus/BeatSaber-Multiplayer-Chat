using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

internal static class MpChatPerformanceGate
{
    // spawn, load, disk cache, and avatar file transfer (never during active beatmap).
    public static bool ShouldBlockAvatarHeavyWork =>
        MpChatLobbyDiagnostics.BeatmapGameplayLikelyActive();

    // arena: keep custom avatars on waiting platforms and in-song; defer only lobby pedestal work.
    public static bool ShouldBlockAvatarHeavyWorkForDriver(bool arenaContext) =>
        !arenaContext && ShouldBlockAvatarHeavyWork;

    public static bool ShouldDeferLobbyPedestalAvatarRefresh =>
        ShouldBlockAvatarHeavyWork ||
        IsMultiplayerSceneTransitionLikely() ||
        (ModSettings.LimitIncomingAvatarDataDuringSongs &&
         MpChatLobbyDiagnostics.SongGameplayLikelyActive());

    public static bool ShouldDeferIncomingAvatarData => ShouldDeferLobbyPedestalAvatarRefresh;

    // menu load, GameCore unload, or spectate handoff: avoid avatar storms that hitch networking.
    public static bool IsMultiplayerSceneTransitionLikely()
    {
        if (MpChatLobbyDiagnostics.AnyGameCoreLoaded() &&
            !MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby())
            return true;

        return false;
    }

    public static bool ShouldThrottleAvatarFileSend => ShouldBlockAvatarHeavyWork;

    // .avatar send/download only in the multiplayer lobby UI (never during arena / GameCore).
    public static bool IsLobbyAvatarFileTransferAllowed =>
        MpChatFeatures.LobbyCustomAvatars &&
        !ShouldBlockAvatarHeavyWork &&
        MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby();

    public static bool CanRunLobbyAvatarFileTransfer => IsLobbyAvatarFileTransferAllowed;

    public static bool CanAcceptLobbyAvatarFileChunks =>
        CustomAvatarDependenciesBootstrap.IsSessionActive() &&
        IsLobbyAvatarFileTransferAllowed;
}
