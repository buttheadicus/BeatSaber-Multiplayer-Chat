using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaber.AvatarCore;
using CustomAvatar.Avatar;
using CustomAvatar.Player;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

public sealed class MpChatLobbyCustomAvatarDriver : MonoBehaviour
{
    private AvatarSpawner _avatarSpawner = null!;
    private AvatarLoader _avatarLoader = null!;
    private IConnectedPlayer _connectedPlayer = null!;
    private MultiplayerAvatarPoseController _poseController = null!;
    private IMultiplayerSessionManager _sessionManager = null!;

    private MpChatLobbyLivePoseInput? _avatarInput;
    private SpawnedAvatar? _spawnedAvatar;
    private Coroutine? _loadCoroutine;

    private string? _lastSpawnedHash;

    [Inject]
    public void Construct(
        AvatarSpawner avatarSpawner,
        AvatarLoader avatarLoader,
        IConnectedPlayer connectedPlayer,
        MultiplayerAvatarPoseController poseController,
        IMultiplayerSessionManager sessionManager)
    {
        _avatarSpawner = avatarSpawner;
        _avatarLoader = avatarLoader;
        _connectedPlayer = connectedPlayer;
        _poseController = poseController;
        _sessionManager = sessionManager;
    }

    private bool IsLocalPedestal()
    {
        var lp = _sessionManager.localPlayer;
        return lp != null && lp.userId == _connectedPlayer.userId;
    }

    private void OnEnable()
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars || IsLocalPedestal())
            return;

        MpCustomAvatarSyncManager.RemoteLobbyAvatarUpdated += OnRemoteLobbyAvatarUpdated;
        RefreshFromSyncState();
    }

    private void OnDisable()
    {
        MpCustomAvatarSyncManager.RemoteLobbyAvatarUpdated -= OnRemoteLobbyAvatarUpdated;
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        DestroySpawned();
    }

    private void OnRemoteLobbyAvatarUpdated(string userId)
    {
        if (userId == _connectedPlayer.userId)
            RefreshFromSyncState();
    }

    private void RefreshFromSyncState()
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars || IsLocalPedestal())
            return;

        if (!MpCustomAvatarSyncManager.TryGetRemoteState(_connectedPlayer.userId, out var row))
            return;

        var hash = row.AvatarDescriptorId?.Trim().ToUpperInvariant() ?? "";
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(hash))
            return;

        if (string.Equals(hash, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", StringComparison.Ordinal))
            return;

        if (string.Equals(_lastSpawnedHash, hash, StringComparison.Ordinal) && _spawnedAvatar != null)
            return;

        if (_loadCoroutine != null)
            StopCoroutine(_loadCoroutine);
        _loadCoroutine = StartCoroutine(LoadAndSpawnCoroutine(hash));
    }

    private IEnumerator LoadAndSpawnCoroutine(string md5HexUpper)
    {
        if (!CustomAvatarLobbyHashCache.TryGetPath(md5HexUpper, out var path))
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] No matching local .avatar for hash {md5HexUpper}");
            _loadCoroutine = null;
            yield break;
        }

        System.Threading.Tasks.Task<AvatarPrefab?> task;
        try
        {
            task = _avatarLoader.LoadFromFileAsync(path, null, System.Threading.CancellationToken.None);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] LoadFromFileAsync threw: {ex.Message}");
            _loadCoroutine = null;
            yield break;
        }

        while (!task.IsCompleted)
            yield return null;

        var prefab = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? task.Result : null;
        if (prefab == null)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Failed loading avatar file: {path}");
            _loadCoroutine = null;
            yield break;
        }

        CreateSpawned(prefab, md5HexUpper);
        _loadCoroutine = null;
    }

    private void CreateSpawned(AvatarPrefab prefab, string hashUpper)
    {
        DestroySpawned();
        _avatarInput ??= new MpChatLobbyLivePoseInput(_poseController);

        _spawnedAvatar = _avatarSpawner.SpawnAvatar(prefab, _avatarInput, _poseController.transform);
        _avatarInput.SetEnabled(true);

        EnableVrikLocomotion(_spawnedAvatar);
        _spawnedAvatar.gameObject.transform.localScale = Vector3.one;
        _lastSpawnedHash = hashUpper;
    }

    private static void EnableVrikLocomotion(SpawnedAvatar spawned)
    {
        foreach (var mb in spawned.gameObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb.GetType().Name != "VRIK")
                continue;
            var prop = mb.GetType().GetProperty("isLocomotionEnabled");
            prop?.SetValue(mb, true, null);
            break;
        }
    }

    private void DestroySpawned()
    {
        if (_spawnedAvatar != null)
        {
            Destroy(_spawnedAvatar.gameObject);
            _spawnedAvatar = null;
        }

        _lastSpawnedHash = null;
    }
}
