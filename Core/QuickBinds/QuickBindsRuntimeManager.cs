using System.Collections.Generic;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core.QuickBinds;

public sealed class QuickBindsRuntimeManager : MonoBehaviour
{
    public static QuickBindsRuntimeManager? Instance { get; private set; }

    private readonly List<QuickBindButton> _quickJoinProgress = new(16);
    private readonly List<QuickBindButton> _quickDisconnectProgress = new(16);
    private readonly List<QuickBindButton> _quickReadyUpProgress = new(16);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!MpChatLobbyDiagnostics.SongGameplayLikelyActive())
            MpCustomAvatarSyncManager.PollDeferredAvatarUpdates();

        if (!ModSettings.EnableQuickBinds)
            return;

        if (!MpChatLobbyDiagnostics.QuickBindsAllowedDuringGameplay())
            return;

        MpMenuUiAutomation.Tick();

        if (VrQuickBindInput.IsSettingsRecordingCaptureActive)
            return;

        PollCombo(ModSettings.QuickJoinQuickPlayCombo, _quickJoinProgress,
            () => QuickBindMpActions.TryQuickJoinQuickPlay(this));
        PollCombo(ModSettings.QuickDisconnectCombo, _quickDisconnectProgress,
            QuickBindMpActions.TryQuickDisconnect);
        PollCombo(ModSettings.QuickReadyUpCombo, _quickReadyUpProgress,
            QuickBindMpActions.TryQuickReadyUp);
    }

    private void PollCombo(IReadOnlyList<int> storedCombo, List<QuickBindButton> progress, System.Action onMatch)
    {
        if (storedCombo == null || storedCombo.Count == 0)
        {
            progress.Clear();
            return;
        }

        if (!VrQuickBindInput.TryConsumeAnyEdge(out var pressed))
            return;

        var expected = (QuickBindButton)Mathf.Clamp(storedCombo[progress.Count], 0, 3);
        if (pressed == expected)
        {
            progress.Add(pressed);
            if (progress.Count >= storedCombo.Count)
            {
                progress.Clear();
                onMatch();
            }

            return;
        }

        progress.Clear();
        if (pressed == (QuickBindButton)Mathf.Clamp(storedCombo[0], 0, 3))
            progress.Add(pressed);
    }
}
