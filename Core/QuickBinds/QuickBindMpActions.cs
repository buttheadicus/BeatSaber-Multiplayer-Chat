using UnityEngine;

namespace MultiplayerChat.Core.QuickBinds;

internal static class QuickBindMpActions
{
    private static bool _quickPlayInFlight;

    internal static void TryQuickJoinQuickPlay(MonoBehaviour coroutineHost)
    {
        if (_quickPlayInFlight || coroutineHost == null)
            return;

        if (MpMenuUiAutomation.HasPending)
            return;

        _quickPlayInFlight = true;
        MpMenuUiAutomation.ScheduleQuickJoinQuickPlayFlow();
        coroutineHost.StartCoroutine(ClearQuickPlayInFlightWhenDone());
    }

    internal static void TryQuickDisconnect()
    {
        if (MpMenuUiAutomation.HasPending)
            return;

        MpMenuUiAutomation.ScheduleQuickDisconnectFlow();
    }

    internal static void TryQuickReadyUp()
    {
        if (MpMenuUiAutomation.HasPending)
            return;

        MpLobbyReady.TryReadyUp();
    }

    private static System.Collections.IEnumerator ClearQuickPlayInFlightWhenDone()
    {
        while (MpMenuUiAutomation.HasPending)
            yield return null;

        yield return new WaitForSeconds(0.5f);
        _quickPlayInFlight = false;
    }
}
