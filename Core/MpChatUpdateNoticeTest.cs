using MultiplayerChat.Core.QuickBinds;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using UnityEngine;

namespace MultiplayerChat.Core;

// Debug only: grip press previews the update-available header system message (30s).
internal sealed class MpChatUpdateNoticeTest : MonoBehaviour
{
    private void Update()
    {
        if (!ModSettings.DebugLogging)
            return;
        if (MpChatLobbyDiagnostics.SongGameplayLikelyActive())
            return;
        if (!VrQuickBindInput.TryConsumeDebugGripEdge())
            return;

        ChatBubbleManager.Instance?.ShowUpdateAvailableNoticeTest();
    }
}
