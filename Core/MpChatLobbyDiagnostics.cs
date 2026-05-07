using System.Text;
using MultiplayerChat;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core;

public static class MpChatLobbyDiagnostics
{
    public static readonly bool DetailedVoipSnapshots = false;

    public static bool VerboseVoipReloadLogs;

    private static float _lastFullSnapshotRealtime = -999f;
    private const float FullSnapshotCooldownSec = 1.25f;

    public static void LogVoipTransition(string tag, string? detail = null)
    {
        if (!DetailedVoipSnapshots)
            return;

        var sb = new StringBuilder(256);
        sb.Append("[MPChat][Diag][VoIP] ").Append(tag);
        sb.Append(" activeScene=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" sceneCount=").Append(SceneManager.sceneCount);
        sb.Append(" anyGameCore=").Append(AnyGameCoreLoaded());
        sb.Append(" songGameplay=").Append(SongGameplayLikelyActive());
        sb.Append(" resultsLike=").Append(ResultsLikeUiVisible());
        sb.Append(" lobbyHeuristic=").Append(LobbyHierarchyLooksLikeMultiplayerLobby());
        sb.Append(" chatMgr=").Append(ChatManager.Instance != null ? ChatManager.Instance.GetHashCode().ToString() : "null");
        sb.Append(" bubbles=").Append(ChatBubbleManager.Instance != null ? ChatBubbleManager.Instance.GetHashCode().ToString() : "null");
        sb.Append(" hotMicMgr=").Append(VoiceHotMicManager.Instance != null ? VoiceHotMicManager.Instance.GetHashCode().ToString() : "null");
        if (!string.IsNullOrEmpty(detail))
            sb.Append(" | ").Append(detail);
        Plugin.Log?.Info(sb.ToString());
    }

    public static void LogFullUiSnapshotThrottled(string reason)
    {
        if (!DetailedVoipSnapshots)
            return;

        var now = Time.realtimeSinceStartup;
        if (now - _lastFullSnapshotRealtime < FullSnapshotCooldownSec)
            return;
        _lastFullSnapshotRealtime = now;

        var sb = new StringBuilder(512);
        sb.Append("[MPChat][Diag][UI] snapshot reason=").Append(reason);
        sb.Append(" active=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" loaded=[");
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (i > 0) sb.Append(',');
            sb.Append(s.name).Append(s.isLoaded ? "" : "(!loaded)");
        }

        sb.Append("] gameplaySetup=");
        try
        {
            var gs = BeatSaberMarkupLanguage.GameplaySetup.GameplaySetup.Instance;
            sb.Append(gs != null ? "present" : "null");
        }
        catch
        {
            sb.Append("error");
        }

        sb.Append(" resultsLike=").Append(ResultsLikeUiVisible());
        Plugin.Log?.Info(sb.ToString());
    }

    public static bool AnyGameCoreLoaded()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && string.Equals(s.name, "GameCore", System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static void InvalidateSceneHeuristicCaches()
    {
        _lobbyHeuristicCacheTime = -999f;
        _songGameplayCacheTime = -999f;
    }

    private static float _lobbyHeuristicCacheTime = -999f;
    private static bool _lobbyHeuristicCached;
    private const float LobbyHeuristicCacheTtlSec = 0.35f;

    private static float _songGameplayCacheTime = -999f;
    private static bool _songGameplayCached;
    private const float SongGameplayCacheTtlSec = 0.35f;

    public static bool SongGameplayLikelyActive()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _songGameplayCacheTime < SongGameplayCacheTtlSec)
            return _songGameplayCached;
        _songGameplayCached = SongGameplayLikelyActiveUncached();
        _songGameplayCacheTime = now;
        return _songGameplayCached;
    }

    private static bool SongGameplayLikelyActiveUncached()
    {
        if (AnyGameCoreLoaded())
            return true;
        // Multiplayer lobby UI: not beatmap gameplay  -  avoids repeated FindObjectOfType scans on policy ticks.
        if (LobbyHierarchyLooksLikeMultiplayerLobbyUncached())
            return false;
        try
        {
            if (Object.FindObjectOfType<AudioTimeSyncController>() != null)
                return true;
            if (Object.FindObjectOfType<BeatmapObjectSpawnController>() != null)
                return true;
            if (Object.FindObjectOfType<SongController>() != null)
                return true;
            if (Object.FindObjectOfType<StandardLevelGameplayManager>() != null)
                return true;
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

    public static bool ResultsLikeUiVisible()
    {
        try
        {
            foreach (var tmp in Object.FindObjectsOfType<TMPro.TMP_Text>())
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                var t = (tmp.text ?? "").ToUpperInvariant();
                if (t.Contains("RESULT") || t.Contains("CONTINUE") || t.Contains("LEVEL COMPLETE") ||
                    t.Contains("LEVEL FAILED") || t.Contains("CLEARED") || t.Contains("SCORE"))
                    return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }

    public static bool LobbyHierarchyLooksLikeMultiplayerLobby()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _lobbyHeuristicCacheTime < LobbyHeuristicCacheTtlSec)
            return _lobbyHeuristicCached;
        _lobbyHeuristicCached = LobbyHierarchyLooksLikeMultiplayerLobbyUncached();
        _lobbyHeuristicCacheTime = now;
        return _lobbyHeuristicCached;
    }

    private static bool LobbyHierarchyLooksLikeMultiplayerLobbyUncached()
    {
        var center = GameObject.Find("MultiplayerLobbyCenterStage");
        if (center != null && center.activeInHierarchy)
            return true;
        var lobby = GameObject.Find("LobbySetup");
        if (lobby != null && lobby.activeInHierarchy)
            return true;
        var alt = GameObject.Find("CenterStage");
        if (alt != null && alt.activeInHierarchy)
            return true;
        var host = GameObject.Find("HostSetup");
        return host != null && host.activeInHierarchy;
    }
}
