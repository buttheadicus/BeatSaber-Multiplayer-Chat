using System;
using System.Reflection;
using System.Text;
using MultiplayerChat.UI;
using MultiplayerCore.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core;

public static class MpChatLobbyDiagnostics
{
    private static float _lastFullSnapshotRealtime = -999f;
    private const float FullSnapshotCooldownSec = 1.25f;

    private static Type? _spectatingSpotType;
    private static PropertyInfo? _spectatingIsObservedProperty;
    private static bool _spectatingReflectionReady;
    private static float _spectatingCacheTime = -999f;
    private static bool _spectatingCached;
    private const float SpectatingCacheTtlSec = 0.4f;

    public static void LogVoipTransition(string tag, string? detail = null)
    {
        if (!MpChatVerboseDebug.IsOn)
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
        MpChatLog.Info(sb.ToString());
    }

    public static void LogFullUiSnapshotThrottled(string reason)
    {
        if (!MpChatVerboseDebug.IsOn)
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
        MpChatLog.Info(sb.ToString());
    }

    public static bool QuickBindsAllowedDuringGameplay()
    {
        if (!SongGameplayLikelyActive())
            return true;
        return IsSpectatingInActiveMultiplayerSong();
    }

    public static bool IsSpectatingInActiveMultiplayerSong()
    {
        if (!SongGameplayLikelyActive())
            return false;

        var now = Time.realtimeSinceStartup;
        if (now - _spectatingCacheTime < SpectatingCacheTtlSec)
            return _spectatingCached;
        _spectatingCached = IsSpectatingInActiveMultiplayerSongUncached();
        _spectatingCacheTime = now;
        return _spectatingCached;
    }

    private static bool IsSpectatingInActiveMultiplayerSongUncached()
    {
        EnsureSpectatingReflection();
        if (_spectatingSpotType == null || _spectatingIsObservedProperty == null)
            return false;

        try
        {
            var spot = UnityEngine.Object.FindObjectOfType(_spectatingSpotType);
            if (spot == null)
                return false;
            var val = _spectatingIsObservedProperty.GetValue(spot);
            return val is bool observed && observed;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSpectatingReflection()
    {
        if (_spectatingReflectionReady)
            return;
        _spectatingReflectionReady = true;
        _spectatingSpotType = Type.GetType("MultiplayerCore.Gameplay.MultiplayerConnectedPlayerSpectatingSpot, MultiplayerCore")
                              ?? Type.GetType("MultiplayerConnectedPlayerSpectatingSpot, MultiplayerCore");
        if (_spectatingSpotType == null)
            return;
        _spectatingIsObservedProperty = _spectatingSpotType.GetProperty(
            "isObserved",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

    public static bool ActiveSceneIsMainMenuWithoutGameCore()
    {
        if (AnyGameCoreLoaded())
            return false;
        var active = SceneManager.GetActiveScene();
        return active.IsValid() && string.Equals(active.name, "MainMenu", System.StringComparison.Ordinal);
    }

    public static void InvalidateSceneHeuristicCaches()
    {
        _lobbyHeuristicCacheTime = -999f;
        _songGameplayCacheTime = -999f;
        _beatmapGameplayCacheTime = -999f;
        _spectatingCacheTime = -999f;
        _resultsLikeCacheTime = -999f;
        _inactiveLobbyChromeCacheTime = -999f;
    }

    private static float _lobbyHeuristicCacheTime = -999f;
    private static bool _lobbyHeuristicCached;
    private const float LobbyHeuristicCacheTtlSec = 0.35f;

    private static float _songGameplayCacheTime = -999f;
    private static bool _songGameplayCached;
    private const float SongGameplayCacheTtlSec = 0.35f;

    private static float _beatmapGameplayCacheTime = -999f;
    private static bool _beatmapGameplayCached;
    private const float BeatmapGameplayCacheTtlSec = 0.35f;

    // Active beatmap (notes spawning / song time advancing). False during GameCore intro and lobby.
    public static bool BeatmapGameplayLikelyActive()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _beatmapGameplayCacheTime < BeatmapGameplayCacheTtlSec)
            return _beatmapGameplayCached;
        _beatmapGameplayCached = BeatmapGameplayLikelyActiveUncached();
        _beatmapGameplayCacheTime = now;
        return _beatmapGameplayCached;
    }

    private static bool BeatmapGameplayLikelyActiveUncached()
    {
        if (ActiveSceneIsMainMenuWithoutGameCore())
            return false;
        if (LobbyHierarchyLooksLikeMultiplayerLobbyUncached())
            return false;

        try
        {
            var spawn = UnityEngine.Object.FindObjectOfType<BeatmapObjectSpawnController>();
            if (spawn != null && spawn.isActiveAndEnabled)
                return true;

            var levelGm = UnityEngine.Object.FindObjectOfType<StandardLevelGameplayManager>();
            if (levelGm != null && levelGm.isActiveAndEnabled)
                return true;

            var atsc = UnityEngine.Object.FindObjectOfType<AudioTimeSyncController>();
            if (atsc != null && atsc.isActiveAndEnabled && atsc.songTime > 0.05f)
                return true;
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

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
        {
            // Map ended: MP results on lobby return is not active gameplay even if GameCore is still loaded.
            if (ResultsLikeUiVisibleUncached() && !BeatmapGameplayLikelyActiveUncached())
                return false;
            return true;
        }
        if (ActiveSceneIsMainMenuWithoutGameCore())
            return false;
        // Multiplayer lobby UI: not beatmap gameplay  -  avoids repeated FindObjectOfType scans on policy ticks.
        if (LobbyHierarchyLooksLikeMultiplayerLobbyUncached())
            return false;
        try
        {
            if (UnityEngine.Object.FindObjectOfType<AudioTimeSyncController>() != null)
                return true;
            if (UnityEngine.Object.FindObjectOfType<BeatmapObjectSpawnController>() != null)
                return true;
            if (UnityEngine.Object.FindObjectOfType<SongController>() != null)
                return true;
            if (UnityEngine.Object.FindObjectOfType<StandardLevelGameplayManager>() != null)
                return true;
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

    private static float _resultsLikeCacheTime = -999f;
    private static bool _resultsLikeCached;
    private const float ResultsLikeCacheTtlSec = 1.25f;

    public static bool ResultsLikeUiVisible()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _resultsLikeCacheTime < ResultsLikeCacheTtlSec)
            return _resultsLikeCached;
        _resultsLikeCached = ResultsLikeUiVisibleUncached();
        _resultsLikeCacheTime = now;
        return _resultsLikeCached;
    }

    private static bool ResultsLikeUiVisibleUncached()
    {
        try
        {
            foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
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

    public static bool ShouldSkipMultiplayerPlayerSessionHooks()
    {
        if (BeatmapGameplayLikelyActive())
            return true;
        if (LobbyHierarchyLooksLikeMultiplayerLobby())
            return false;
        return SongGameplayLikelyActive();
    }

    // Lobby pedestal sync and metadata ticks only in lobby UI or arena (GameCore), not main menu while still in session.
    public static bool MultiplayerAvatarSyncContextActive(IMultiplayerSessionManager? sessionManager)
    {
        if (AnyGameCoreLoaded())
            return true;
        return LobbyHierarchyLooksLikeMultiplayerLobby();
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

    private static float _inactiveLobbyChromeCacheTime = -999f;
    private static bool _inactiveLobbyChromeCached;
    private const float InactiveLobbyChromeCacheTtlSec = 0.5f;

    // Lobby chrome can stay inactive under the MP results overlay while the session is still in lobby.
    public static bool InactiveMultiplayerLobbyChromeExists()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _inactiveLobbyChromeCacheTime < InactiveLobbyChromeCacheTtlSec)
            return _inactiveLobbyChromeCached;
        _inactiveLobbyChromeCached = InactiveMultiplayerLobbyChromeExistsUncached();
        _inactiveLobbyChromeCacheTime = now;
        return _inactiveLobbyChromeCached;
    }

    private static bool InactiveMultiplayerLobbyChromeExistsUncached()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (DescendantHasInactiveMpLobbyChromeName(root.transform))
                    return true;
            }
        }

        return false;
    }

    private static bool DescendantHasInactiveMpLobbyChromeName(Transform root)
    {
        var stack = new System.Collections.Generic.Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            var n = t.gameObject.name;
            if (n == "MultiplayerLobbyCenterStage" || n == "LobbySetup" || n == "HostSetup")
                return true;
            for (var c = 0; c < t.childCount; c++)
                stack.Push(t.GetChild(c));
        }

        return false;
    }

    // Active lobby UI or arena-return results where inactive lobby chrome still exists underneath.
    public static bool MultiplayerLobbyReturnContextActive()
    {
        if (LobbyHierarchyLooksLikeMultiplayerLobby())
            return true;
        if (!ResultsLikeUiVisible())
            return false;
        return InactiveMultiplayerLobbyChromeExists();
    }
}
