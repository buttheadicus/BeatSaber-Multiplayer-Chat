using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

// Lobby-only .avatar fan-out: one host upload can fill every peer's local cache (see performance gate).
public sealed class MpCustomAvatarLobbyTransferManager : MonoBehaviour, IInitializable
{
    public static MpCustomAvatarLobbyTransferManager? Instance { get; private set; }

    public static event Action<string>? LobbyAvatarFileCached;

    private const float RequestCooldownSeconds = 12f;

    private const float ChunkSendIntervalSeconds = 0.02f;

    private static readonly object Gate = new();

    private static readonly Dictionary<string, float> RequestSentAt =
        new(StringComparer.Ordinal);

    private static readonly Dictionary<string, IncomingAssembly> IncomingByKey =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> OutboundHashesInFlight = new(StringComparer.Ordinal);

    private static readonly Queue<PendingCacheWrite> DeferredCacheWrites = new();

    private static readonly Queue<OutboundJob> DeferredOutboundJobs = new();

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    private Coroutine? _sendRoutine;

    private readonly Queue<OutboundJob> _sendQueue = new();

    private readonly MpCustomAvatarFileRequestPacket _requestPacket = new();

    private readonly MpCustomAvatarFileChunkPacket _chunkPacket = new();

    public void Initialize()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    public static void RequestLobbyAvatarFile(string md5HexUpper, string ownerUserId)
    {
        if (!MpChatFeatures.LobbyCustomAvatars)
            return;
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(md5HexUpper))
            return;
        if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
            return;

        md5HexUpper = md5HexUpper.ToUpperInvariant();
        if (CustomAvatarLobbyHashCache.TryGetPath(md5HexUpper, out _))
            return;

        var key = md5HexUpper + "|" + (ownerUserId ?? "");
        var now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            if (RequestSentAt.TryGetValue(key, out var last) && now - last < RequestCooldownSeconds)
                return;
            RequestSentAt[key] = now;
        }

        if (Instance == null)
            return;

        Instance.SendFileRequest(md5HexUpper);
    }

    private void SendFileRequest(string hash)
    {
        _requestPacket.HashMd5Hex = hash;
        try
        {
            _sessionManager.Send(_requestPacket);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] File request send failed: {ex.Message}");
        }
    }

    public void HandleFileRequest(MpCustomAvatarFileRequestPacket packet, IConnectedPlayer sender)
    {
        if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
            return;
        if (sender == null)
            return;

        var hash = (packet.HashMd5Hex ?? "").Trim().ToUpperInvariant();
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(hash))
            return;

        var localHash = (ModSettings.LobbyCustomAvatarContentHash ?? "").Trim().ToUpperInvariant();
        if (!string.Equals(hash, localHash, StringComparison.Ordinal))
            return;
        if (!ModSettings.EnableLobbyCustomAvatars)
            return;

        if (!CustomAvatarLobbyHashCache.TryGetPath(hash, out var path) || !File.Exists(path))
            return;

        lock (Gate)
        {
            if (OutboundHashesInFlight.Contains(hash))
                return;
            OutboundHashesInFlight.Add(hash);
        }

        var job = new OutboundJob(path, hash);
        if (MpChatPerformanceGate.ShouldBlockAvatarHeavyWork)
        {
            lock (Gate)
                DeferredOutboundJobs.Enqueue(job);
            return;
        }

        _sendQueue.Enqueue(job);
        if (_sendRoutine == null)
            _sendRoutine = StartCoroutine(SendChunksRoutine());
    }

    public void HandleFileChunk(MpCustomAvatarFileChunkPacket packet, IConnectedPlayer sender)
    {
        if (!MpChatPerformanceGate.CanAcceptLobbyAvatarFileChunks)
            return;
        if (sender == null || string.IsNullOrEmpty(sender.userId))
            return;

        var hash = (packet.HashMd5Hex ?? "").Trim().ToUpperInvariant();
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(hash))
            return;
        if (packet.ChunkCount == 0 || packet.Payload == null || packet.Payload.Length == 0)
            return;
        if (packet.ChunkIndex >= packet.ChunkCount)
            return;

        if (CustomAvatarLobbyHashCache.TryGetPath(hash, out _))
            return;

        var key = sender.userId + ":" + hash;
        IncomingAssembly assembly;
        var startedNewDownload = false;
        lock (Gate)
        {
            if (!IncomingByKey.TryGetValue(key, out assembly!))
            {
                assembly = new IncomingAssembly(hash, packet.ChunkCount);
                IncomingByKey[key] = assembly;
                startedNewDownload = true;
            }

            if (assembly.ChunkCount != packet.ChunkCount)
                return;

            assembly.Chunks[packet.ChunkIndex] = packet.Payload;
        }

        if (startedNewDownload)
            MpCustomAvatarUserNotifier.PostDownloading(sender.userId, sender.userName);

        if (!assembly.IsComplete())
            return;

        byte[] fileBytes;
        try
        {
            fileBytes = assembly.Build();
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Assemble failed for {hash}: {ex.Message}");
            lock (Gate)
                IncomingByKey.Remove(key);
            return;
        }

        if (fileBytes.Length > MpCustomAvatarFileChunkPacket.MaxTotalFileBytes)
        {
            lock (Gate)
                IncomingByKey.Remove(key);
            return;
        }

        var computed = CustomAvatarHashUtil.Md5HexBytes(fileBytes);
        if (!string.Equals(computed, hash, StringComparison.OrdinalIgnoreCase))
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Download hash mismatch for {hash} (got {computed})");
            lock (Gate)
                IncomingByKey.Remove(key);
            return;
        }

        lock (Gate)
            IncomingByKey.Remove(key);

        if (MpChatPerformanceGate.ShouldBlockAvatarHeavyWork)
        {
            lock (Gate)
                DeferredCacheWrites.Enqueue(new PendingCacheWrite(fileBytes, hash, sender.userId));
            return;
        }

        FinishCachedRemoteAvatar(fileBytes, hash, sender.userId);
    }

    public static void PollDeferredCacheWrites()
    {
        if (MpChatPerformanceGate.ShouldBlockAvatarHeavyWork)
            return;

        PendingCacheWrite[] batch;
        lock (Gate)
        {
            if (DeferredCacheWrites.Count == 0)
                return;
            batch = new PendingCacheWrite[DeferredCacheWrites.Count];
            DeferredCacheWrites.CopyTo(batch, 0);
            DeferredCacheWrites.Clear();
        }

        foreach (var job in batch)
            FinishCachedRemoteAvatar(job.FileBytes, job.Hash, job.OwnerUserId);
    }

    private static void FinishCachedRemoteAvatar(byte[] fileBytes, string hash, string ownerUserId)
    {
        try
        {
            Directory.CreateDirectory(CustomAvatarLobbyCachePaths.CacheDirectory);
            var dest = CustomAvatarLobbyCachePaths.PathForHash(hash);
            File.WriteAllBytes(dest, fileBytes);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Cache write failed: {ex.Message}");
            return;
        }

        CustomAvatarLobbyHashCache.Invalidate();
        MultiplayerChat.Plugin.Log?.Info($"[MPChat][LobbyAvatar] Cached remote .avatar {hash} ({fileBytes.Length} bytes)");
        LobbyAvatarFileCached?.Invoke(hash);
        MpCustomAvatarSyncManager.NotifyRemoteAvatarMayBeReady(ownerUserId);
    }

    public static void PollDeferredOutbound()
    {
        if (MpChatPerformanceGate.ShouldBlockAvatarHeavyWork || Instance == null)
            return;

        OutboundJob[] batch;
        lock (Gate)
        {
            if (DeferredOutboundJobs.Count == 0)
                return;
            batch = new OutboundJob[DeferredOutboundJobs.Count];
            DeferredOutboundJobs.CopyTo(batch, 0);
            DeferredOutboundJobs.Clear();
        }

        foreach (var job in batch)
            Instance._sendQueue.Enqueue(job);

        if (Instance._sendRoutine == null && Instance._sendQueue.Count > 0)
            Instance._sendRoutine = Instance.StartCoroutine(Instance.SendChunksRoutine());
    }

    private IEnumerator SendChunksRoutine()
    {
        var wait = new WaitForSeconds(ChunkSendIntervalSeconds);
        while (_sendQueue.Count > 0)
        {
            if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
            {
                while (_sendQueue.Count > 0)
                {
                    var deferred = _sendQueue.Dequeue();
                    lock (Gate)
                        DeferredOutboundJobs.Enqueue(deferred);
                }

                break;
            }

            var job = _sendQueue.Dequeue();
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(job.Path);
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Read for upload failed: {ex.Message}");
                lock (Gate)
                    OutboundHashesInFlight.Remove(job.Hash);
                continue;
            }

            if (bytes.Length > MpCustomAvatarFileChunkPacket.MaxTotalFileBytes)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Avatar too large to share: {bytes.Length} bytes");
                lock (Gate)
                    OutboundHashesInFlight.Remove(job.Hash);
                continue;
            }

            var chunkSize = MpCustomAvatarFileChunkPacket.MaxChunkPayloadBytes;
            var chunkCount = (ushort)((bytes.Length + chunkSize - 1) / chunkSize);
            if (chunkCount == 0)
                chunkCount = 1;

            for (ushort i = 0; i < chunkCount; i++)
            {
                if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
                    break;

                var offset = i * chunkSize;
                var len = Math.Min(chunkSize, bytes.Length - offset);
                var slice = new byte[len];
                Buffer.BlockCopy(bytes, offset, slice, 0, len);

                _chunkPacket.Version = MpCustomAvatarFileChunkPacket.WireVersion;
                _chunkPacket.HashMd5Hex = job.Hash;
                _chunkPacket.ChunkIndex = i;
                _chunkPacket.ChunkCount = chunkCount;
                _chunkPacket.Payload = slice;

                try
                {
                    _sessionManager.Send(_chunkPacket);
                }
                catch (Exception ex)
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Chunk send failed: {ex.Message}");
                    break;
                }

                yield return wait;
            }

            lock (Gate)
                OutboundHashesInFlight.Remove(job.Hash);
        }

        _sendRoutine = null;
    }

    private readonly struct PendingCacheWrite
    {
        public PendingCacheWrite(byte[] fileBytes, string hash, string ownerUserId)
        {
            FileBytes = fileBytes;
            Hash = hash;
            OwnerUserId = ownerUserId;
        }

        public byte[] FileBytes { get; }

        public string Hash { get; }

        public string OwnerUserId { get; }
    }

    private sealed class OutboundJob
    {
        public OutboundJob(string path, string hash)
        {
            Path = path;
            Hash = hash;
        }

        public string Path { get; }

        public string Hash { get; }
    }

    private sealed class IncomingAssembly
    {
        public IncomingAssembly(string hash, ushort chunkCount)
        {
            Hash = hash;
            ChunkCount = chunkCount;
            Chunks = new byte[chunkCount][];
        }

        public string Hash { get; }

        public ushort ChunkCount { get; }

        public byte[][] Chunks { get; }

        public bool IsComplete()
        {
            for (var i = 0; i < ChunkCount; i++)
            {
                if (Chunks[i] == null || Chunks[i].Length == 0)
                    return false;
            }

            return true;
        }

        public byte[] Build()
        {
            var total = 0;
            for (var i = 0; i < ChunkCount; i++)
                total += Chunks[i].Length;

            var buf = new byte[total];
            var pos = 0;
            for (var i = 0; i < ChunkCount; i++)
            {
                Buffer.BlockCopy(Chunks[i], 0, buf, pos, Chunks[i].Length);
                pos += Chunks[i].Length;
            }

            return buf;
        }
    }
}
