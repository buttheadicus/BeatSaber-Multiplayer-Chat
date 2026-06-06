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

    private const float RequestCooldownSeconds = 4f;

    private const int SendBytesBudgetSmallLobby = 768 * 1024;

    private const int SendBytesBudgetLargeLobby = 384 * 1024;

    private const int LargeLobbyPlayerThreshold = 10;

    private static readonly object Gate = new();

    private static readonly Dictionary<string, float> RequestSentAt =
        new(StringComparer.Ordinal);

    private static readonly Dictionary<string, IncomingAssembly> IncomingByHash =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> OutboundHashesInFlight = new(StringComparer.Ordinal);

    private static readonly HashSet<string> DownloadNotifiedHashes = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, byte[]> OutboundBytesByHash = new(StringComparer.Ordinal);

    private static readonly Queue<PendingCacheWrite> DeferredCacheWrites = new();

    private static readonly Queue<OutboundJob> DeferredOutboundJobs = new();

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    private Coroutine? _sendRoutine;

    private readonly Queue<OutboundJob> _sendQueue = new();

    private readonly MpCustomAvatarFileRequestPacket _requestPacket = new();

    private readonly MpCustomAvatarFileChunkPacket _chunkPacket = new();

    private byte[] _chunkScratch = Array.Empty<byte>();

    private Coroutine? _deferredPollRoutine;

    public void Initialize()
    {
        Instance = this;
        _deferredPollRoutine = StartCoroutine(PollDeferredTransferWorkRoutine());
    }

    private void OnDestroy()
    {
        if (_deferredPollRoutine != null)
        {
            StopCoroutine(_deferredPollRoutine);
            _deferredPollRoutine = null;
        }

        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    private static IEnumerator PollDeferredTransferWorkRoutine()
    {
        var wait = new WaitForSeconds(0.12f);
        while (true)
        {
            yield return wait;
            if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
                continue;

            PollDeferredCacheWrites();
            PollDeferredOutbound();
        }
    }

    public static void RequestLobbyAvatarFile(string md5HexUpper, string ownerUserId)
    {
        _ = ownerUserId;
        if (!MpChatFeatures.LobbyCustomAvatars)
            return;
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(md5HexUpper))
            return;
        if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
            return;

        md5HexUpper = md5HexUpper.ToUpperInvariant();
        if (CustomAvatarLobbyHashCache.TryGetPath(md5HexUpper, out _))
            return;

        var now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            if (IncomingByHash.ContainsKey(md5HexUpper))
                return;

            if (RequestSentAt.TryGetValue(md5HexUpper, out var last) && now - last < RequestCooldownSeconds)
                return;

            RequestSentAt[md5HexUpper] = now;
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
        _ = sender;
        if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
            return;
        if (!ModSettings.EnableLobbyCustomAvatars)
            return;

        var hash = (packet.HashMd5Hex ?? "").Trim().ToUpperInvariant();
        if (!CustomAvatarHashUtil.LooksLikeMd5Hex(hash))
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

        IncomingAssembly assembly;
        var startedNewDownload = false;
        lock (Gate)
        {
            if (!IncomingByHash.TryGetValue(hash, out assembly!))
            {
                assembly = new IncomingAssembly(hash, packet.ChunkCount);
                IncomingByHash[hash] = assembly;
                startedNewDownload = true;
            }

            if (assembly.ChunkCount != packet.ChunkCount)
                return;

            if (assembly.Chunks[packet.ChunkIndex] != null)
                return;

            assembly.Chunks[packet.ChunkIndex] = packet.Payload;
        }

        if (startedNewDownload && DownloadNotifiedHashes.Add(hash))
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
            {
                IncomingByHash.Remove(hash);
                DownloadNotifiedHashes.Remove(hash);
            }

            return;
        }

        lock (Gate)
        {
            IncomingByHash.Remove(hash);
            DownloadNotifiedHashes.Remove(hash);
        }

        if (fileBytes.Length > MpCustomAvatarFileChunkPacket.MaxTotalFileBytes)
            return;

        var computed = CustomAvatarHashUtil.Md5HexBytes(fileBytes);
        if (!string.Equals(computed, hash, StringComparison.OrdinalIgnoreCase))
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Download hash mismatch for {hash} (got {computed})");
            return;
        }

        if (MpChatPerformanceGate.ShouldBlockAvatarHeavyWork)
        {
            lock (Gate)
                DeferredCacheWrites.Enqueue(new PendingCacheWrite(fileBytes, hash));
            return;
        }

        FinishCachedRemoteAvatar(fileBytes, hash);
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
            FinishCachedRemoteAvatar(job.FileBytes, job.Hash);
    }

    private static void FinishCachedRemoteAvatar(byte[] fileBytes, string hash)
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

        lock (Gate)
            OutboundBytesByHash[hash] = fileBytes;

        CustomAvatarLobbyHashCache.Invalidate();
        MultiplayerChat.Plugin.Log?.Info($"[MPChat][LobbyAvatar] Cached remote .avatar {hash} ({fileBytes.Length} bytes)");
        LobbyAvatarFileCached?.Invoke(hash);
        MpCustomAvatarSyncManager.NotifyAllRemotesWithHash(hash);
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
            if (!TryGetOutboundBytes(job, out var bytes))
            {
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

            var bytesBudget = GetSendBytesBudgetPerFrame();
            var sentThisFrame = 0;

            for (ushort i = 0; i < chunkCount; i++)
            {
                if (!MpChatPerformanceGate.CanRunLobbyAvatarFileTransfer)
                    break;

                var offset = i * chunkSize;
                var len = Math.Min(chunkSize, bytes.Length - offset);
                if (_chunkScratch.Length < len)
                    _chunkScratch = new byte[len];

                Buffer.BlockCopy(bytes, offset, _chunkScratch, 0, len);

                _chunkPacket.Version = MpCustomAvatarFileChunkPacket.WireVersion;
                _chunkPacket.HashMd5Hex = job.Hash;
                _chunkPacket.ChunkIndex = i;
                _chunkPacket.ChunkCount = chunkCount;
                _chunkPacket.Payload = CopyChunkSlice(len);

                try
                {
                    _sessionManager.Send(_chunkPacket);
                }
                catch (Exception ex)
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Chunk send failed: {ex.Message}");
                    break;
                }

                sentThisFrame += len;
                if (sentThisFrame >= bytesBudget)
                {
                    sentThisFrame = 0;
                    yield return null;
                }
            }

            lock (Gate)
                OutboundHashesInFlight.Remove(job.Hash);
        }

        _sendRoutine = null;
    }

    private bool TryGetOutboundBytes(OutboundJob job, out byte[] bytes)
    {
        lock (Gate)
        {
            if (OutboundBytesByHash.TryGetValue(job.Hash, out var cached) && cached.Length > 0)
            {
                bytes = cached;
                return true;
            }
        }

        try
        {
            bytes = File.ReadAllBytes(job.Path);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Read for upload failed: {ex.Message}");
            bytes = Array.Empty<byte>();
            return false;
        }

        lock (Gate)
            OutboundBytesByHash[job.Hash] = bytes;

        return bytes.Length > 0;
    }

    private byte[] CopyChunkSlice(int len)
    {
        var slice = new byte[len];
        Buffer.BlockCopy(_chunkScratch, 0, slice, 0, len);
        return slice;
    }

    private int GetSendBytesBudgetPerFrame()
    {
        return GetConnectedPlayerCount() >= LargeLobbyPlayerThreshold
            ? SendBytesBudgetLargeLobby
            : SendBytesBudgetSmallLobby;
    }

    private int GetConnectedPlayerCount()
    {
        try
        {
            return _sessionManager.connectedPlayers?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private readonly struct PendingCacheWrite
    {
        public PendingCacheWrite(byte[] fileBytes, string hash)
        {
            FileBytes = fileBytes;
            Hash = hash;
        }

        public byte[] FileBytes { get; }

        public string Hash { get; }
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
