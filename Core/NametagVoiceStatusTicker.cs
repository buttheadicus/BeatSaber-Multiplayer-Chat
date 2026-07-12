using UnityEngine;

namespace MultiplayerChat.Core;

// one shared 10 Hz tick for all nametag voice icons instead of per-avatar Update loops.
internal sealed class NametagVoiceStatusTicker : MonoBehaviour
{
    private const float TickIntervalSec = 0.1f;

    private static NametagVoiceStatusTicker? _instance;
    private float _nextTick;

    internal static void EnsureRunning()
    {
        if (_instance != null)
            return;

        var go = new GameObject("MPChatNametagVoiceStatusTicker");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<NametagVoiceStatusTicker>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            return;

        var now = Time.realtimeSinceStartup;
        if (now < _nextTick)
            return;
        _nextTick = now + TickIntervalSec;
        if (UI.ChatBubbleAnchor.ActiveStatusAnchorCount == 0)
            return;
        UI.ChatBubbleAnchor.TickAllStatusIcons();
    }
}
