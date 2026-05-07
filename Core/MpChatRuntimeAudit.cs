using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;

namespace MultiplayerChat.Core;

// One-shot diagnostics when debug mode is on (safe on any thread that already holds session state).
internal static class MpChatRuntimeAudit
{
    internal static void LogAfterLobbyChatInit(
        IMultiplayerSessionManager session,
        EncryptionManager encryption,
        bool lobbyUiInstaller)
    {
        if (!ModSettings.DebugLogging)
            return;

        var local = session.localPlayer;
        var connected = session.connectedPlayers;
        var n = connected?.Count ?? 0;
        MultiplayerChat.Plugin.Log?.Debug(
            $"[MPChat][Audit] ChatManager init scope={(lobbyUiInstaller ? "Menu/Lobby" : "GameCore")} " +
            $"localUserId={(local == null ? "null" : local.userId ?? "empty")} " +
            $"connectedSlots={n} hasSessionKey={encryption.HasSessionKey} " +
            $"keyFpLen={encryption.LastSessionStateFingerprint.Length}");
    }
}
