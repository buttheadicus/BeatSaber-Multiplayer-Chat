using System.Collections.Generic;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

// One throttled poll loop for all lobby custom-avatar pose inputs (avoids per-driver LateUpdate + FindObjectsOfType).
internal static class MpChatLobbyPosePoll
{
    private const float PollIntervalSeconds = 0.05f;

    private static readonly List<MpChatLobbyLivePoseInput> ActiveInputs = new();

    private static float _nextPollTime;

    internal static void Register(MpChatLobbyLivePoseInput input)
    {
        if (input == null || ActiveInputs.Contains(input))
            return;

        ActiveInputs.Add(input);
    }

    internal static void Unregister(MpChatLobbyLivePoseInput? input)
    {
        if (input == null)
            return;

        ActiveInputs.Remove(input);
    }

    internal static void ClearAll()
    {
        ActiveInputs.Clear();
        MpChatLocalPlayerPoseBridge.ClearLocalTarget();
        MpChatLocalCaPoseSampler.InvalidateCache();
        _nextPollTime = 0f;
    }

    internal static void TickFromHost()
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return;

        if (Time.unscaledTime < _nextPollTime)
            return;

        _nextPollTime = Time.unscaledTime + PollIntervalSeconds;

        if (MpChatPerformanceGate.ShouldDeferLobbyPedestalAvatarRefresh)
            return;

        MpChatLocalPlayerPoseBridge.TickCached();

        for (var i = ActiveInputs.Count - 1; i >= 0; i--)
            ActiveInputs[i]?.PollThrottled();
    }
}
