using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core;

// Refreshes lobby pedestal custom avatars after arena / GameCore transitions.
public sealed class MpChatLobbyAvatarLifecycleHost : MonoBehaviour
{
    public static MpChatLobbyAvatarLifecycleHost? Instance { get; private set; }

    private Coroutine? _pendingRefresh;

    private Coroutine? _pendingJoinBatch;

    private Coroutine? _pendingLeaveBatch;

    private static readonly List<string> PendingJoinUserIds = new(8);

    private static readonly List<string> PendingLeaveUserIds = new(8);

    private static bool _pendingJoinBroadcastMetadata;

    private static readonly object PendingJoinLock = new();

    private static readonly object PendingLeaveLock = new();

    private void Awake() => Instance = this;

    public static void QueuePlayerJoinAvatarWork(string userId, bool broadcastMetadata = false)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        lock (PendingJoinLock)
        {
            if (!PendingJoinUserIds.Contains(userId))
                PendingJoinUserIds.Add(userId);
            if (broadcastMetadata)
                _pendingJoinBroadcastMetadata = true;
        }

        if (Instance != null)
        {
            Instance.EnsureJoinBatchCoroutine();
            return;
        }

        MpChatLobbyCustomAvatarDriver.ProcessPlayerJoinedImmediate(userId);
        if (broadcastMetadata && MpChatFeatures.LobbyCustomAvatars && ModSettings.EnableLobbyCustomAvatars)
            MpCustomAvatarSyncManager.BroadcastMetadataNow(applySavedEyeHeight: false);
    }

    private void EnsureJoinBatchCoroutine()
    {
        if (_pendingJoinBatch != null)
            return;

        _pendingJoinBatch = StartCoroutine(FlushJoinBatchEndOfFrame());
    }

    private IEnumerator FlushJoinBatchEndOfFrame()
    {
        yield return null;

        string[] userIds;
        var broadcastMetadata = false;
        lock (PendingJoinLock)
        {
            if (PendingJoinUserIds.Count == 0)
            {
                _pendingJoinBatch = null;
                yield break;
            }

            userIds = PendingJoinUserIds.ToArray();
            PendingJoinUserIds.Clear();
            broadcastMetadata = _pendingJoinBroadcastMetadata;
            _pendingJoinBroadcastMetadata = false;
        }

        for (var i = 0; i < userIds.Length; i++)
        {
            MpChatLobbyCustomAvatarDriver.ProcessPlayerJoinedImmediate(userIds[i]);
            if (userIds.Length > 1 && i < userIds.Length - 1)
                yield return null;
        }

        if (broadcastMetadata && MpChatFeatures.LobbyCustomAvatars && ModSettings.EnableLobbyCustomAvatars)
            MpCustomAvatarSyncManager.BroadcastMetadataNow(applySavedEyeHeight: false);

        _pendingJoinBatch = null;
    }

    public static void QueuePlayerLeaveAvatarWork(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        lock (PendingLeaveLock)
        {
            if (!PendingLeaveUserIds.Contains(userId))
                PendingLeaveUserIds.Add(userId);
        }

        if (Instance != null)
        {
            Instance.EnsureLeaveBatchCoroutine();
            return;
        }

        MpChatLobbyCustomAvatarDriver.ProcessPlayerDisconnectedImmediate(userId);
    }

    private void EnsureLeaveBatchCoroutine()
    {
        if (_pendingLeaveBatch != null)
            return;

        _pendingLeaveBatch = StartCoroutine(FlushLeaveBatchEndOfFrame());
    }

    private IEnumerator FlushLeaveBatchEndOfFrame()
    {
        yield return null;

        string[] userIds;
        lock (PendingLeaveLock)
        {
            if (PendingLeaveUserIds.Count == 0)
            {
                _pendingLeaveBatch = null;
                yield break;
            }

            userIds = PendingLeaveUserIds.ToArray();
            PendingLeaveUserIds.Clear();
        }

        for (var i = 0; i < userIds.Length; i++)
        {
            MpChatLobbyCustomAvatarDriver.ProcessPlayerDisconnectedImmediate(userIds[i]);
            if (userIds.Length > 1 && i < userIds.Length - 1)
                yield return null;
        }

        _pendingLeaveBatch = null;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    public static void ScheduleSystemMessageRemoval(string message, float delaySeconds)
    {
        if (string.IsNullOrEmpty(message) || delaySeconds <= 0f)
            return;
        if (Instance == null)
            return;

        Instance.StartCoroutine(RemoveSystemMessageAfter(delaySeconds, message));
    }

    private static IEnumerator RemoveSystemMessageAfter(float delaySeconds, string message)
    {
        yield return new WaitForSeconds(delaySeconds);
        ChatManager.Instance?.RequestRemoveSystemMessage(message);
    }

    private void Update() => MpChatLobbyPosePoll.TickFromHost();

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (string.Equals(scene.name, "GameCore", System.StringComparison.Ordinal) ||
            string.Equals(scene.name, "MultiplayerGameplay", System.StringComparison.Ordinal))
        {
            MpChatArenaAvatarAttach.DestroyOrphanedArenaObjects();
            ScheduleLobbyAvatarRefresh($"scene unloaded: {scene.name}");
        }
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (string.Equals(newScene.name, "GameCore", System.StringComparison.Ordinal))
            ScheduleArenaAvatarScan();

        if (string.Equals(oldScene.name, "GameCore", System.StringComparison.Ordinal))
            ScheduleLobbyAvatarRefresh($"left GameCore -> {newScene.name}");
    }

    private Coroutine? _pendingArenaScan;

    private void ScheduleArenaAvatarScan()
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return;
        if (!MpChatFeatures.LobbyCustomAvatarsInArena)
            return;

        if (_pendingArenaScan != null)
            StopCoroutine(_pendingArenaScan);
        _pendingArenaScan = StartCoroutine(ScanArenaAvatarsAfterGameCoreLoad());
    }

    private IEnumerator ScanArenaAvatarsAfterGameCoreLoad()
    {
        yield return null;
        MpChatArenaAvatarAttach.ScanGameCoreAvatars();
        yield return new WaitForSecondsRealtime(0.5f);
        MpChatArenaAvatarAttach.ScanGameCoreAvatars();
        yield return new WaitForSecondsRealtime(3f);
        MpChatArenaAvatarAttach.ScanGameCoreAvatars();
        yield return new WaitForSecondsRealtime(5f);
        MpChatArenaAvatarAttach.ScanGameCoreAvatars();
        _pendingArenaScan = null;
    }

    private void ScheduleLobbyAvatarRefresh(string reason)
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return;

        if (_pendingRefresh != null)
            StopCoroutine(_pendingRefresh);
        _pendingRefresh = StartCoroutine(RefreshAfterLobbyReturn(reason));
    }

    private IEnumerator RefreshAfterLobbyReturn(string reason)
    {
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
        MpChatLobbyPosePoll.ClearAll();

        const float lobbyWaitTimeoutSeconds = 3f;
        var waitStart = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - waitStart < lobbyWaitTimeoutSeconds)
        {
            yield return null;
            if (MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby())
                break;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        yield return new WaitForSecondsRealtime(0.25f);

        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
        {
            _pendingRefresh = null;
            yield break;
        }

        MpCustomAvatarHeightCalibration.ApplySavedPresetIfAny();
        MpCustomAvatarSyncManager.PollDeferredAvatarUpdates();
        MpCustomAvatarSyncManager.InvalidateOutboundDedupe();
        MpCustomAvatarSyncManager.BroadcastMetadataNow();
        MpChatLobbyCustomAvatarDriver.RefreshAllLobbyAvatarDrivers(forceRespawn: false);

        MultiplayerChat.Plugin.Log?.Debug($"[MPChat][LobbyAvatar] Refreshed lobby avatars after {reason}");
        _pendingRefresh = null;
    }
}
