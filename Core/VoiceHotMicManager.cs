using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat.Settings;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>Continuous hot mic capture (PCM chunks). Playback runs on <see cref="ChatManager"/>.</summary>
public class VoiceHotMicManager : MonoBehaviour, IInitializable
{
    public static VoiceHotMicManager? Instance { get; private set; }

    private static VoiceHotMicManager? _lobbyScopeInstance;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;
    [Inject] private readonly ChatManager _chatManager = null!;

    private string? _micDevice;
    /// <summary>Device name passed to <see cref="Microphone.Start"/>; null = OS default.</summary>
    private string? _micDeviceAtStart;
    private AudioClip? _micLoop;
    private int _lastReadFrame;
    private int _chunkMonoSamples = 960;
    private readonly List<float> _monoScratch = new();

    private bool _voiceGateActive;
    private int _voiceHangoverChunksRemaining;

    /// <summary>When <see cref="VoiceHotMicTransport.WarmupChunksToSkip"/> &gt; 0, drops that many post-start chunks before send (currently 0).</summary>
    private int _micWarmupChunksRemaining;

    /// <summary>RMS to open gate (float -1..1). Previous 0.008 blocked many real mics; keep low so quiet speech still passes.</summary>
    private const float VadOpenRms = 0.002f;

    private const float VadCloseRms = 0.001f;

    private const int VadHangoverChunks = 6;

    /// <summary>Linear crossfade at VHOT chunk joins on the send path (mono samples). Reduces boundary clicks from fixed-window cuts without DSP scheduling.</summary>
    private const int HotMicSendCrossfadeMonoSamples = 48;

    private float[]? _hotMicSendCrossfadeTail;

    private Coroutine? _deferredMicRestartCoroutine;

    /// <summary>Lobby + GameCore each get a <see cref="VoiceHotMicManager"/>; only <see cref="Instance"/> may capture (see <see cref="Update"/>).</summary>
    private bool IsActiveHotMicHost => ReferenceEquals(Instance, this);

    private bool _lastPttInputCombined;
    private bool _loggedPttPrefsOnce;
    private float _nextPttPeriodicLogTime;

    /// <summary>Dev-only: log Primary/Secondary/Grip/Trigger raw XR state ~1 Hz while in lobby.</summary>
    private static readonly bool TemporaryLogRawControllerBindings = false;

    private const float RawControllerBindingsLogIntervalSec = 1f;
    private float _nextRawControllerBindingsLogTime;

    public void Initialize()
    {
        // GameCore host always owns Instance during gameplay. Lobby host must not overwrite Instance while a
        // GameCore VoiceHotMicManager exists (additive scenes / VoIP reload order) — that caused open mic + broken PTT.
        if (MpChatSceneScope.IsGameCoreHost(this))
        {
            Instance = this;
            return;
        }

        _lobbyScopeInstance = this;

        if (Instance == null || !MpChatSceneScope.IsGameCoreHost(Instance))
            Instance = this;
    }

    /// <summary>Stops the mic ring buffer so the next capture cycle starts fresh (used when reloading VoIP on scene changes).</summary>
    public void ForceReloadMicrophone()
    {
        if (MpChatLobbyDiagnostics.VerboseVoipReloadLogs)
            MultiplayerChat.Plugin.Log?.Info("[MPChat][VoIP] VoiceHotMicManager.ForceReloadMicrophone()");
        if (_deferredMicRestartCoroutine != null)
        {
            StopCoroutine(_deferredMicRestartCoroutine);
            _deferredMicRestartCoroutine = null;
        }

        StopMic();
        _deferredMicRestartCoroutine = StartCoroutine(DeferredRestartMicrophoneRoutine());
    }

    /// <summary>
    /// After <see cref="StopMic"/>, <see cref="IMultiplayerSessionManager.localPlayer"/> can be null for many frames in host setup;
    /// retry <see cref="EnsureMic"/> once capture is allowed so open-mic / PTT works again without re-entering the game.
    /// </summary>
    private IEnumerator DeferredRestartMicrophoneRoutine()
    {
        try
        {
            for (var i = 0; i < 120; i++)
            {
                yield return null;
                if (!isActiveAndEnabled)
                    yield break;
                if (!IsActiveHotMicHost)
                    yield break;
                if (!CanCaptureHotMic())
                    continue;
                EnsureMic();
                if (MpChatLobbyDiagnostics.VerboseVoipReloadLogs)
                    MultiplayerChat.Plugin.Log?.Info("[MPChat][VoIP] VoiceHotMicManager: deferred mic EnsureMic() succeeded");
                yield break;
            }

            MultiplayerChat.Plugin.Log?.Warn("[MPChat][VoIP] VoiceHotMicManager: deferred mic restart gave up after 120 frames (CanCaptureHotMic stayed false)");
        }
        finally
        {
            _deferredMicRestartCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (_deferredMicRestartCoroutine != null)
        {
            StopCoroutine(_deferredMicRestartCoroutine);
            _deferredMicRestartCoroutine = null;
        }

        StopMic();

        var lobbyPeer = _lobbyScopeInstance;
        var iAmLobby = ReferenceEquals(_lobbyScopeInstance, this);
        var gameCore = MpChatSceneScope.IsGameCoreHost(this);

        if (iAmLobby)
            _lobbyScopeInstance = null;

        if (Instance == this)
        {
            if (gameCore && lobbyPeer != null && !ReferenceEquals(lobbyPeer, this))
                Instance = lobbyPeer;
            else
                Instance = null;
        }
    }

    private void Update()
    {
        // Only one manager (current Instance) may run the mic; the other Zenject context's component would otherwise
        // keep Microphone.Start alive and bypass PTT / doublesend.
        if (!IsActiveHotMicHost)
        {
            if (_micLoop != null)
                StopMic();
            return;
        }

        MaybeLogRawControllerBindings();
        MaybeLogPttState();

        if (!CanCaptureHotMic())
        {
            StopMic();
            return;
        }

        EnsureMic();
        PollMicAndSend();
    }

    private static readonly string[] PttBindingLegend = { "Primary", "Secondary", "Trigger", "Grip" };

    private void MaybeLogRawControllerBindings()
    {
        if (!TemporaryLogRawControllerBindings)
            return;
        if (_sessionManager?.localPlayer == null)
            return;

        var now = Time.realtimeSinceStartup;
        if (now < _nextRawControllerBindingsLogTime)
            return;
        _nextRawControllerBindingsLogTime = now + RawControllerBindingsLogIntervalSec;

        MultiplayerChat.Plugin.Log?.Info("[MPChat][PTT][Hw] " + VrPttInput.BuildRawControllerBindingsDiagnosticLine());
    }

    private void MaybeLogPttState()
    {
        if (_sessionManager?.localPlayer == null)
            return;

        var pttOn = ModSettings.PushToTalkEnabled;
        if (!pttOn)
        {
            _lastPttInputCombined = false;
            _loggedPttPrefsOnce = false;
            return;
        }

        var bindingIdx = ModSettings.PttBindingIndex;
        var vrHeld = VrPttInput.IsBindingHeld(bindingIdx);
        var keyboardPtt = Input.GetKey(KeyCode.Space) && !VrPttInput.HasAnyHandDeviceValid();
        var combined = vrHeld || keyboardPtt;

        if (!_loggedPttPrefsOnce)
        {
            _loggedPttPrefsOnce = true;
            var name = bindingIdx >= 0 && bindingIdx < PttBindingLegend.Length ? PttBindingLegend[bindingIdx] : "?";
            MultiplayerChat.Plugin.Log?.Info(
                $"[MPChat][PTT] Push-to-talk ON: binding={name} (idx={bindingIdx}) scene={gameObject.scene.name}. Press/hold logs on change.");
        }

        if (combined != _lastPttInputCombined)
        {
            var name = bindingIdx >= 0 && bindingIdx < PttBindingLegend.Length ? PttBindingLegend[bindingIdx] : "?";
            MultiplayerChat.Plugin.Log?.Info(
                $"[MPChat][PTT] {(combined ? "BUTTON HELD" : "button released")}: binding={name} vrBinding={vrHeld} SpaceFallback={keyboardPtt}{VrPttInput.FormatDiagnosticsSuffix(bindingIdx)}");
            _lastPttInputCombined = combined;
        }

        var now = Time.realtimeSinceStartup;
        if (now >= _nextPttPeriodicLogTime)
        {
            _nextPttPeriodicLogTime = now + 4f;
            var capturing = _micLoop != null;
            if (pttOn && !combined && capturing)
            {
                MultiplayerChat.Plugin.Log?.Warn(
                    $"[MPChat][PTT] Mic still recording while PTT on and button not held (unexpected). deaf={VoiceChatRuntimeState.IsDeaf} hotMicMuted={VoiceChatRuntimeState.IsHotMicMuted}");
            }
        }
    }

    private bool MicSelectionChanged()
    {
        if (_micLoop == null) return false;
        var want = ResolveMicDeviceName();
        return !SameMicSelection(want, _micDeviceAtStart);
    }

    private static bool SameMicSelection(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", System.StringComparison.Ordinal);

    private bool CanCaptureHotMic()
    {
        if (_sessionManager?.localPlayer == null) return false;
        if (VoiceChatRuntimeState.IsDeaf) return false;

        var pushToTalk = ModSettings.PushToTalkEnabled;
        // VR: rely on controller bindings only (Space would stick open mic from legacy input). No / broken XR hands: allow Space for desktop FPFC.
        var vrHeldRaw = VrPttInput.IsBindingHeld(ModSettings.PttBindingIndex);
        var keyboardPtt = Input.GetKey(KeyCode.Space) && !VrPttInput.HasAnyHandDeviceValid();
        var pttHeld = !pushToTalk || vrHeldRaw || keyboardPtt;

        if (VoiceChatRuntimeState.IsHotMicMuted)
        {
            // Manual / lobby mute: stay silent. Song policy mute (you were unmuted at map start): allow TX while PTT held.
            if (!pushToTalk || !GlobalChatAudioHost.SongMicCoercionAllowsPttBypass() || !pttHeld)
                return false;
        }

        if (pushToTalk && !pttHeld)
        {
            // Same hangover whether XR hands report valid or not (avoids open-mic when controllers are briefly invalid).
            var hasTail = _voiceGateActive || _voiceHangoverChunksRemaining > 0;
            var hasFullChunkPending = _monoScratch.Count >= _chunkMonoSamples;
            if (!hasTail && !hasFullChunkPending)
                return false;
        }

        return true;
    }

    private void EnsureMic()
    {
        if (_micLoop != null)
        {
            if (!MicSelectionChanged()) return;
            StopMic();
        }

        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Hot mic: no microphone devices.");
            return;
        }

        var want = ResolveMicDeviceName();
        _micDevice = want;
        _micDeviceAtStart = want;

        Microphone.GetDeviceCaps(_micDevice, out var minF, out var maxF);
        var hz = 24000;
        if (maxF > 0) hz = Mathf.Clamp(24000, minF, maxF);
        else if (minF > 0) hz = minF;

        _micLoop = Microphone.Start(_micDevice, true, 2, hz);
        if (_micLoop == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Hot mic: Microphone.Start failed.");
            _micDevice = null;
            _micDeviceAtStart = null;
            return;
        }

        _lastReadFrame = 0;
        // Slightly longer frames than 100ms = fewer send boundaries (was ~4×100ms → ~400ms coalesced clips with ~25ms inter-burst gaps).
        _chunkMonoSamples = Mathf.Max(256, (int)(_micLoop.frequency * 0.15f));
        _micWarmupChunksRemaining = Mathf.Max(0, VoiceHotMicTransport.WarmupChunksToSkip);
        _voiceGateActive = false;
        _voiceHangoverChunksRemaining = 0;
    }

    private void StopMic()
    {
        if (_micLoop != null)
        {
            try
            {
                if (Microphone.IsRecording(_micDevice))
                    Microphone.End(_micDevice);
            }
            catch
            {
                try
                {
                    Microphone.End(null);
                }
                catch { /* ignore */ }
            }

            Destroy(_micLoop);
            _micLoop = null;
        }

        _micDevice = null;
        _micDeviceAtStart = null;
        _lastReadFrame = 0;
        _monoScratch.Clear();
        _voiceGateActive = false;
        _voiceHangoverChunksRemaining = 0;
        _micWarmupChunksRemaining = 0;
        _hotMicSendCrossfadeTail = null;
    }

    private static string? ResolveMicDeviceName()
    {
        var name = ModSettings.MicInputDeviceName;
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var d in Microphone.devices ?? System.Array.Empty<string>())
            if (d == name) return name;
        return null;
    }

    private void PollMicAndSend()
    {
        if (_micLoop == null) return;

        var ch = Mathf.Max(1, _micLoop.channels);
        var clipFrames = _micLoop.samples;
        var pos = Microphone.GetPosition(_micDevice);
        if (pos < 0) return;

        var nFrames = (pos - _lastReadFrame + clipFrames) % clipFrames;
        if (nFrames <= 0) return;

        var floatCount = nFrames * ch;
        var buf = new float[floatCount];
        _micLoop.GetData(buf, _lastReadFrame);
        _lastReadFrame = pos;

        AppendMonoFromInterleavedBuf(buf, ch, nFrames);

        while (_monoScratch.Count >= _chunkMonoSamples)
        {
            var chunk = new float[_chunkMonoSamples];
            for (var i = 0; i < _chunkMonoSamples; i++)
                chunk[i] = _monoScratch[i];
            _monoScratch.RemoveRange(0, _chunkMonoSamples);

            var warmupActive = _micWarmupChunksRemaining > 0;
            var willTransmit = !warmupActive && ShouldTransmitVoiceChunk(chunk);
            if (willTransmit)
                ApplySendSideChunkCrossfade(chunk);

            var encoded = VoiceHotMicCodec.EncodeChunk(chunk, 1, _micLoop.frequency);
            if (encoded == null) continue;
            if (warmupActive)
            {
                _micWarmupChunksRemaining--;
                _hotMicSendCrossfadeTail = null;
                continue;
            }

            if (!willTransmit)
            {
                _hotMicSendCrossfadeTail = null;
                continue;
            }

            VoipPipelineTrace.TxChunk(_chunkMonoSamples, _micLoop.frequency, encoded.Length, _micDevice);
            _chatManager.SendVoiceHotMicChunk(encoded);
            SaveSendCrossfadeTail(chunk);
        }
    }

    private void ApplySendSideChunkCrossfade(float[] chunk)
    {
        var k = HotMicSendCrossfadeMonoSamples;
        var tail = _hotMicSendCrossfadeTail;
        if (tail == null || tail.Length != k) return;
        if (chunk.Length <= k) return;
        for (var i = 0; i < k; i++)
        {
            var t = (i + 1f) / k;
            chunk[i] = tail[i] * (1f - t) + chunk[i] * t;
        }
    }

    private void SaveSendCrossfadeTail(float[] chunk)
    {
        var k = HotMicSendCrossfadeMonoSamples;
        if (chunk.Length < k) return;
        if (_hotMicSendCrossfadeTail == null || _hotMicSendCrossfadeTail.Length != k)
            _hotMicSendCrossfadeTail = new float[k];
        Array.Copy(chunk, chunk.Length - k, _hotMicSendCrossfadeTail, 0, k);
    }

    /// <summary>RMS voice gate — always on; no bypass (see <see cref="VoiceHotMicTransport"/> class remarks).</summary>
    private bool ShouldTransmitVoiceChunk(float[] chunk)
    {
        var rms = ComputeRms(chunk, chunk.Length);
        if (!_voiceGateActive)
        {
            if (rms < VadOpenRms)
                return false;
            _voiceGateActive = true;
            _voiceHangoverChunksRemaining = VadHangoverChunks;
            return true;
        }

        if (rms >= VadCloseRms)
        {
            _voiceHangoverChunksRemaining = VadHangoverChunks;
            return true;
        }

        if (_voiceHangoverChunksRemaining > 0)
        {
            _voiceHangoverChunksRemaining--;
            return true;
        }

        _voiceGateActive = false;
        return false;
    }

    private static float ComputeRms(float[] samples, int count)
    {
        if (count <= 0 || samples.Length < count)
            return 0f;
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            var s = samples[i];
            sum += s * s;
        }

        return (float)Math.Sqrt(sum / count);
    }

    private void AppendMonoFromInterleavedBuf(float[] data, int channels, int frameCount)
    {
        if (channels <= 1)
        {
            for (var i = 0; i < frameCount && i < data.Length; i++)
                _monoScratch.Add(data[i]);
            return;
        }

        for (var i = 0; i < frameCount; i++)
        {
            double s = 0;
            for (var c = 0; c < channels; c++)
                s += data[i * channels + c];
            _monoScratch.Add((float)(s / channels));
        }
    }
}
