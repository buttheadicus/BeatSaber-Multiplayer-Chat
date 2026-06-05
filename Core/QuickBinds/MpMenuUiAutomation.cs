using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MultiplayerChat.Core.QuickBinds;

// Drives menu button presses for Quick Binds (confirm dialogs, leave lobby, quick play join).
internal static class MpMenuUiAutomation
{
    private sealed class ScheduledStep
    {
        public float ExecuteAt;
        public string Name = "";
        public Action? Action;
    }

    private static readonly List<ScheduledStep> Pending = new(16);
    private static readonly MethodInfo? ButtonPressMethod = typeof(Button).GetMethod(
        "Press",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Type? _mainMenuVcType;
    private static Type? _mainFcType;
    private static Type? _mpFcType;
    private static Type? _mpVcType;
    private static Type? _joinVcType;
    private static Type? _joiningVcType;
    private static Type? _simpleDialogType;
    private static Type? _disconnectPromptType;
    private static Type? _lobbyFcType;
    private static Type? _resultsVcType;
    private static Type? _missionResultsVcType;
    private static Type? _mpResultsVcType;

    private static int _quickJoinFlowId;
    private static bool _quickPlayMenuDone;
    private static bool _quickJoinDone;
    private static float _quickPlayCompletedAt;
    private static float _joinScreenFirstSeenAt;
    private static float _matchmakingNavRequestedAt;
    private static float _matchmakingScreenReadyAt;
    private static float _blockingUiDismissedAt;

    private const float RequeueStepDelaySeconds = 0.3f;
    private const float MatchmakingNavSettleSeconds = 1.0f;
    private const float QuickPlayAfterReadySeconds = 0.5f;
    private const float BlockingUiSettleSeconds = 0.8f;
    private const int MaxQuickJoinPollAttempts = 24;
    private const float QuickJoinPollIntervalSeconds = 0.15f + RequeueStepDelaySeconds;

    private const string MainMenuMultiplayerClickHandler = "<DidActivate>b__20_6";
    private const string QuickPlayClickHandler = "<DidActivate>b__11_0";
    private const string JoinQuickPlayClickHandler = "<DidActivate>b__12_0";
    private const string JoiningLobbyCancelClickHandler = "<DidActivate>b__8_0";
    private const string PresentMpDisclaimerCallback = "<PresentMultiplayerModeSelectionFlowCoordinatorWithDisclaimerAndAvatarCreator>b__43_0";

    private const int MainMenuMultiplayerButton = 6;
    private const int MultiplayerQuickPlayButton = 0;

    private static readonly string[] ContinueHandlerMethodNames =
    {
        "ContinueButtonPressed",
        "BackToMenuPressed",
        "BackToLobbyPressed"
    };

    private static readonly string[] QuickPlayLabelNeedles =
    {
        "quick play",
        "quickplay",
        "quick-play"
    };

    private static readonly string[] DialogDismissLabelNeedles =
    {
        "cancel",
        "no",
        "close",
        "back",
        "ok",
        "dismiss"
    };

    internal static bool HasPending => Pending.Count > 0;

    internal static void Tick()
    {
        if (Pending.Count == 0)
            return;

        var now = Time.realtimeSinceStartup;
        for (var i = Pending.Count - 1; i >= 0; i--)
        {
            if (now < Pending[i].ExecuteAt)
                continue;

            var item = Pending[i];
            Pending.RemoveAt(i);
            try
            {
                item.Action?.Invoke();
            }
            catch
            {
            }
        }
    }

    internal static void ScheduleQuickDisconnectFlow()
    {
        Pending.Clear();
        Schedule(0f, "ClearBlockingUi", () => RunStep(TryClearBlockingDialogs));
        Schedule(0.35f, "Continue", () => RunStep(TryPressContinue));
        Schedule(0.7f, "LeaveSession", () => RunStep(TryLeaveSessionForMenuFlow));
        Schedule(1.05f, "ConfirmLeave", () => RunStep(TryClearBlockingDialogs));
        MultiplayerChat.Plugin.Log?.Info("[MPChat][QuickBinds] Quick Disconnect flow scheduled.");
    }

    internal static void ScheduleQuickJoinQuickPlayFlow()
    {
        Pending.Clear();
        _quickJoinFlowId++;
        _quickPlayMenuDone = false;
        _quickJoinDone = false;
        _quickPlayCompletedAt = 0f;
        _joinScreenFirstSeenAt = 0f;
        _matchmakingNavRequestedAt = 0f;
        _matchmakingScreenReadyAt = 0f;
        _blockingUiDismissedAt = 0f;

        if (IsMainMenuMatchmakingReady())
            _matchmakingScreenReadyAt = Time.realtimeSinceStartup;

        Schedule(0f, "ClearBlockingUi", () => RunStep(TryClearBlockingDialogs));
        Schedule(0f, "LeaveSession", () => RunStep(TryLeaveSessionForMenuFlow));
        Schedule(0.1f + RequeueStepDelaySeconds, "Continue", () => RunStep(TryPressContinueIfOnResults));
        Schedule(QuickJoinPollIntervalSeconds, "QuickJoinPoll#0", () => RunQuickJoinPollStep(_quickJoinFlowId, 0));
        MultiplayerChat.Plugin.Log?.Info("[MPChat][QuickBinds] Quick Join QuickPlay flow scheduled.");
    }

    private static void Schedule(float delaySeconds, string name, Action action)
    {
        Pending.Add(new ScheduledStep
        {
            ExecuteAt = Time.realtimeSinceStartup + delaySeconds,
            Name = name,
            Action = action
        });
    }

    private static bool RunStep(Func<bool> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return false;
        }
    }

    private static void RunQuickJoinPollStep(int flowId, int attempt)
    {
        if (flowId != _quickJoinFlowId)
            return;

        if (_quickJoinDone && IsQuickPlayJoinInProgress())
        {
            CancelPendingQuickJoinPolls();
            return;
        }

        if (_quickJoinDone)
        {
            CancelPendingQuickJoinPolls();
            return;
        }

        if (!_quickPlayMenuDone && TryClearBlockingDialogs())
        {
            _matchmakingScreenReadyAt = 0f;
            _joinScreenFirstSeenAt = 0f;
        }
        else
        {
            if (IsJoinQuickPlayScreenActive())
            {
                if (_joinScreenFirstSeenAt <= 0f)
                    _joinScreenFirstSeenAt = Time.realtimeSinceStartup;

                var joinScreenAge = Time.realtimeSinceStartup - _joinScreenFirstSeenAt;
                var sinceQuickPlay = _quickPlayMenuDone
                    ? Time.realtimeSinceStartup - _quickPlayCompletedAt
                    : 0f;

                if (joinScreenAge >= RequeueStepDelaySeconds
                    && (!_quickPlayMenuDone || sinceQuickPlay >= RequeueStepDelaySeconds)
                    && RunStep(TryPressJoinQuickPlayOnce))
                {
                    CancelPendingQuickJoinPolls();
                    return;
                }
            }
            else
            {
                _joinScreenFirstSeenAt = 0f;
            }

            if (!_quickPlayMenuDone && !IsValidQuickJoinDestinationUi())
            {
                if (!IsMpNavigationSettling())
                    RunStep(TryNavigateBackTowardQuickJoinDestination);
            }
            else if (!_quickPlayMenuDone && IsMainMenuMatchmakingReady())
            {
                UpdateMatchmakingReadyTimestamp();
                if (CanPressQuickPlayYet() && RunStep(TryPressQuickPlayOnce))
                    _quickPlayMenuDone = true;
            }
            else if (!_quickPlayMenuDone && !IsMpNavigationSettling())
            {
                RunStep(TryEnsureMainMenuMatchmaking);
            }
        }

        if (attempt + 1 >= MaxQuickJoinPollAttempts)
            return;

        Schedule(
            QuickJoinPollIntervalSeconds,
            "QuickJoinPoll#" + (attempt + 1),
            () => RunQuickJoinPollStep(flowId, attempt + 1));
    }

    private static void CancelPendingQuickJoinPolls()
    {
        for (var i = Pending.Count - 1; i >= 0; i--)
        {
            if (Pending[i].Name.StartsWith("QuickJoinPoll", StringComparison.Ordinal))
                Pending.RemoveAt(i);
        }
    }

    private static bool TryClearBlockingDialogs()
    {
        var acted = false;
        if (TryCancelJoiningLobby())
            acted = true;
        if (TryDismissBlockingErrorDialog())
            acted = true;
        if (TryCancelDisconnectPrompt())
            acted = true;
        if (acted)
            MarkBlockingUiDismissed();
        return acted;
    }

    private static bool TryPressContinueIfOnResults()
    {
        if (!IsOnAnyResultsScreen())
            return true;
        return TryPressContinue();
    }

    private static bool TryPressContinue()
    {
        if (TryInvokeContinueHandlers())
            return true;
        if (TryFinishSimpleDialogContinue())
            return true;
        if (TryPressDisconnectPromptOk())
            return true;
        return IsOnAnyResultsScreen() && TryPressButtonByLabelNeedles(new[] { "continue" });
    }

    private static bool TryLeaveSessionForMenuFlow()
    {
        if (TryPressMultiplayerResultsBackToMenu())
            return !MpLobbySessionExit.IsInCustomServerLobby();

        if (!MpLobbySessionExit.IsInCustomServerLobby() && !MpLobbySessionExit.IsSessionConnected())
            return true;

        return MpLobbySessionExit.TryLeaveLobbyImmediately();
    }

    private static bool TryPressQuickPlayOnce()
    {
        if (_quickPlayMenuDone || IsJoinQuickPlayScreenActive())
            return true;

        if (!CanPressQuickPlayYet())
            return false;

        if (TryClearBlockingDialogs())
        {
            _matchmakingScreenReadyAt = 0f;
            return false;
        }

        if (!IsMainMenuMatchmakingReady())
            return false;

        if (!TryPressQuickPlay(skipNavigation: true))
            return false;

        _quickPlayMenuDone = true;
        _quickPlayCompletedAt = Time.realtimeSinceStartup;
        return true;
    }

    private static bool TryPressJoinQuickPlayOnce()
    {
        if (_quickJoinDone || IsQuickPlayJoinInProgress())
            return true;

        if (MpLobbySessionExit.IsInCustomServerLobby())
            return false;

        if (!IsJoinQuickPlayScreenActive())
            return false;

        if (!TryPressJoinQuickPlay())
            return false;

        _quickJoinDone = true;
        return true;
    }

    private static bool TryPressQuickPlay(bool skipNavigation = false)
    {
        if (MpLobbySessionExit.IsInCustomServerLobby())
            return false;

        if (!skipNavigation)
        {
            TryOpenMultiplayerFromMainMenu();
            TryDismissMultiplayerDisclaimerIfNeeded();
        }

        if (!IsMainMenuMatchmakingReady())
            return false;

        var mpFcType = MpFcType;
        var mpVcType = MpVcType;
        var menuButtonType = MpUiReflection.ResolveNestedEnum("MultiplayerModeSelectionViewController", "MenuButton");
        if (menuButtonType == null)
            return false;

        var quickPlayButton = Enum.ToObject(menuButtonType, MultiplayerQuickPlayButton);
        var mpFc = MpUiReflection.GetBestFlowCoordinator(mpFcType, "_multiplayerModeSelectionViewController");
        var mpVc = GetActiveMatchmakingViewController();
        if (mpVc == null && mpFc != null)
            mpVc = MpUiReflection.GetInstanceField(mpFc, "_multiplayerModeSelectionViewController");

        if (mpVc is MonoBehaviour mpBehaviour && !mpBehaviour.gameObject.activeInHierarchy)
            return false;

        if (mpVc != null && !IsQuickPlayButtonReady(mpVc, mpVcType))
            return false;

        if (mpVc != null && TryPressFieldButton(mpVc, mpVcType, "_quickPlayButton"))
            return true;

        if (mpFc != null
            && mpVc != null
            && MpUiReflection.TryInvoke(mpFc, "HandleMultiplayerLobbyControllerDidFinish", mpVc, quickPlayButton))
            return true;

        if (mpVc != null)
        {
            if (MpUiReflection.TryInvokeParameterless(mpVc, QuickPlayClickHandler))
                return true;
            if (MpUiReflection.TryInvoke(mpVc, "HandleMenuButton", quickPlayButton))
                return true;
        }

        foreach (var vc in MpUiReflection.FindAllInLoadedScenes(mpVcType, requireHierarchy: true))
        {
            if (MpUiReflection.TryInvokeParameterless(vc, QuickPlayClickHandler))
                return true;
            if (MpUiReflection.TryInvoke(vc, "HandleMenuButton", quickPlayButton))
                return true;
            if (TryPressFieldButton(vc, mpVcType, "_quickPlayButton"))
                return true;
        }

        return TryPressButtonByLabelNeedles(QuickPlayLabelNeedles);
    }

    private static bool TryPressJoinQuickPlay()
    {
        var joinVc = MpUiReflection.GetFlowCoordinatorField(MpFcType, "_joinQuickPlayViewController");
        if (joinVc != null && TryPressJoinOnViewController(joinVc, JoinVcType))
            return true;

        foreach (var vc in MpUiReflection.FindAllInLoadedScenes(JoinVcType, requireHierarchy: true))
        {
            if (TryPressJoinOnViewController(vc, JoinVcType))
                return true;
        }

        return TryPressJoinButtonByLabel();
    }

    private static bool TryOpenMultiplayerFromMainMenu()
    {
        if (IsMainMenuMatchmakingReady())
            return true;

        if (MpLobbySessionExit.IsInCustomServerLobby() || MpLobbySessionExit.IsSessionConnected())
            return false;

        if (IsJoiningLobbyScreenActive() || IsMpNavigationSettling() || _matchmakingNavRequestedAt > 0f)
            return false;

        if (!IsMainMenuRootAvailable())
            return false;

        var mainFcType = MainFcType;
        var mainVcType = MainMenuVcType;
        var menuButtonType = MpUiReflection.ResolveNestedEnum("MainMenuViewController", "MenuButton");
        if (menuButtonType == null)
            return false;

        var multiplayerButton = Enum.ToObject(menuButtonType, MainMenuMultiplayerButton);
        var mainFc = MpUiReflection.GetBestFlowCoordinator(mainFcType, "_mainMenuViewController");
        var mainVc = GetMainMenuViewController();

        if (mainFc != null
            && mainVc != null
            && MpUiReflection.TryInvoke(mainFc, "HandleMainMenuViewControllerDidFinish", mainVc, multiplayerButton))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        if (mainVc != null && MpUiReflection.TryInvoke(mainVc, "HandleMenuButton", multiplayerButton))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        if (mainVc != null && MpUiReflection.TryInvokeParameterless(mainVc, MainMenuMultiplayerClickHandler))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        if (TryPressFieldButton(mainVc, mainVcType, "_multiplayerButton"))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        if (TryPressButtonByLabelNeedles(new[] { "multiplayer" }))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        if (mainFc != null
            && MpUiReflection.TryInvoke(mainFc, "PresentMultiplayerModeSelectionFlowCoordinatorWithDisclaimerAndAvatarCreator", true))
        {
            MarkMatchmakingNavigationRequested();
            return true;
        }

        return false;
    }

    private static bool TryDismissMultiplayerDisclaimerIfNeeded()
    {
        return !IsDisclaimerVisible() || TryDismissMultiplayerDisclaimer();
    }

    private static bool TryDismissMultiplayerDisclaimer()
    {
        var mainFc = MpUiReflection.GetBestFlowCoordinator(MainFcType, "_mainMenuViewController");
        if (mainFc == null)
            return false;

        if (MpUiReflection.TryInvoke(mainFc, "HandleMultiplayerDisclaimerDidFinishAction", 0))
            return true;

        if (MpUiReflection.TryInvoke(mainFc, PresentMpDisclaimerCallback, 0))
            return true;

        return TryPressButtonByLabelNeedles(new[] { "agree", "accept", "ok", "continue", "yes" });
    }

    private static bool TryEnsureMainMenuMatchmaking()
    {
        if (MpLobbySessionExit.IsInCustomServerLobby() || MpLobbySessionExit.IsSessionConnected())
        {
            TryLeaveSessionForMenuFlow();
            return false;
        }

        if (IsMainMenuMatchmakingReady())
            return true;

        if (TryClearBlockingDialogs())
            return false;

        if (!IsValidQuickJoinDestinationUi())
        {
            TryNavigateBackTowardQuickJoinDestination();
            return false;
        }

        if (IsMpNavigationSettling() || _matchmakingNavRequestedAt > 0f || !IsMainMenuRootAvailable())
            return false;

        TryOpenMultiplayerFromMainMenu();
        TryDismissMultiplayerDisclaimerIfNeeded();
        UpdateMatchmakingReadyTimestamp();
        return IsMainMenuMatchmakingReady();
    }

    private static bool TryNavigateBackTowardQuickJoinDestination()
    {
        if (IsValidQuickJoinDestinationUi())
            return false;

        if (MpLobbySessionExit.IsInCustomServerLobby() || MpLobbySessionExit.IsSessionConnected())
            return TryLeaveSessionForMenuFlow();

        if (IsOnAnyResultsScreen())
            return TryPressContinue();

        if (TryClearBlockingDialogs())
            return true;

        if (!TryPressScreenBackButton())
            return false;

        MarkBlockingUiDismissed();
        return true;
    }

    private static bool IsValidQuickJoinDestinationUi()
    {
        return IsMainMenuMatchmakingReady()
            || IsMainMenuRootAvailable()
            || IsJoinQuickPlayScreenActive();
    }

    private static bool IsMainMenuMatchmakingReady()
    {
        return GetActiveMatchmakingViewController() != null
            && !MpLobbySessionExit.IsInCustomServerLobby();
    }

    private static bool IsJoinQuickPlayScreenActive()
    {
        return MpUiReflection.FindBestActiveObject(JoinVcType) != null;
    }

    private static bool IsQuickPlayJoinInProgress()
    {
        if (MpLobbySessionExit.IsInCustomServerLobby())
            return false;
        if (IsJoiningLobbyScreenActive())
            return true;
        return _quickJoinDone && MpLobbySessionExit.IsSessionConnected();
    }

    private static bool IsJoiningLobbyScreenActive()
    {
        if (MpUiReflection.FindBestActiveObject(JoiningVcType) != null)
            return true;

        foreach (var vc in MpUiReflection.FindAllInLoadedScenes(JoiningVcType))
        {
            if (IsFieldButtonVisible(vc, JoiningVcType, "_cancelJoiningButton"))
                return true;
        }

        return false;
    }

    private static bool IsOnAnyResultsScreen()
    {
        return MpUiReflection.FindBestActiveObject(ResultsVcType) != null
            || MpUiReflection.FindBestActiveObject(MissionResultsVcType) != null
            || MpUiReflection.FindBestActiveObject(MpResultsVcType) != null;
    }

    private static bool CanPressQuickPlayYet()
    {
        if (!IsMainMenuMatchmakingReady() || IsMpNavigationSettling())
            return false;

        var now = Time.realtimeSinceStartup;
        if (_matchmakingNavRequestedAt > 0f && now - _matchmakingNavRequestedAt < MatchmakingNavSettleSeconds)
            return false;

        if (_matchmakingScreenReadyAt > 0f && now - _matchmakingScreenReadyAt < QuickPlayAfterReadySeconds)
            return false;

        return true;
    }

    private static bool IsMpNavigationSettling()
    {
        var now = Time.realtimeSinceStartup;
        if (_blockingUiDismissedAt > 0f && now - _blockingUiDismissedAt < BlockingUiSettleSeconds)
            return true;
        if (_matchmakingNavRequestedAt > 0f && now - _matchmakingNavRequestedAt < MatchmakingNavSettleSeconds)
            return true;
        return false;
    }

    private static void MarkMatchmakingNavigationRequested()
    {
        if (_matchmakingNavRequestedAt <= 0f)
            _matchmakingNavRequestedAt = Time.realtimeSinceStartup;
    }

    private static void MarkBlockingUiDismissed()
    {
        _blockingUiDismissedAt = Time.realtimeSinceStartup;
        _matchmakingNavRequestedAt = 0f;
        _matchmakingScreenReadyAt = 0f;
    }

    private static void UpdateMatchmakingReadyTimestamp()
    {
        _matchmakingScreenReadyAt = IsMainMenuMatchmakingReady()
            ? _matchmakingScreenReadyAt > 0f ? _matchmakingScreenReadyAt : Time.realtimeSinceStartup
            : 0f;
    }

    private static object? GetActiveMatchmakingViewController()
    {
        foreach (var vc in MpUiReflection.FindAllInLoadedScenes(MpVcType, requireHierarchy: true))
        {
            if (vc is not MonoBehaviour behaviour || !behaviour.isActiveAndEnabled)
                continue;
            if (!IsQuickPlayButtonReady(vc, MpVcType))
                continue;
            return vc;
        }

        return null;
    }

    private static object? GetMainMenuViewController()
    {
        return MpUiReflection.GetFlowCoordinatorField(MainFcType, "_mainMenuViewController")
            ?? MpUiReflection.FindBestActiveObject(MainMenuVcType);
    }

    private static bool IsMainMenuRootAvailable()
    {
        if (IsMainMenuMatchmakingReady())
            return false;

        var mainVc = GetMainMenuViewController();
        if (mainVc is MonoBehaviour behaviour && !behaviour.gameObject.activeInHierarchy)
            return false;

        return mainVc != null && IsFieldButtonVisible(mainVc, MainMenuVcType, "_multiplayerButton");
    }

    private static bool IsQuickPlayButtonReady(object? viewController, Type? viewControllerType)
    {
        if (viewController == null || viewControllerType == null)
            return false;

        var field = viewControllerType.GetField("_quickPlayButton", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(viewController) is not Button button)
            return false;

        return button.gameObject.activeInHierarchy && button.interactable;
    }

    private static bool TryCancelJoiningLobby()
    {
        if (!IsJoiningLobbyScreenActive())
            return false;

        var mpFc = MpUiReflection.GetBestFlowCoordinator(MpFcType, "_joiningLobbyViewController");
        if (mpFc != null && MpUiReflection.TryInvokeParameterless(mpFc, "HandleJoiningLobbyViewControllerDidCancel"))
            return true;

        foreach (var joiningVc in MpUiReflection.FindAllInLoadedScenes(JoiningVcType))
        {
            if (TryPressFieldButton(joiningVc, JoiningVcType, "_cancelJoiningButton"))
                return true;
            if (MpUiReflection.TryInvokeParameterless(joiningVc, JoiningLobbyCancelClickHandler))
                return true;
        }

        return TryPressButtonByLabelNeedles(new[] { "cancel" });
    }

    private static bool TryDismissBlockingErrorDialog()
    {
        if (!GetVisibleSimpleDialog(out var viewController, out var viewControllerType))
            return false;

        if (viewController == null || viewControllerType == null)
            return false;

        if (IsDisclaimerDialog(viewController, viewControllerType))
            return false;

        var buttonsField = viewControllerType.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        var textsField = viewControllerType.GetField("_buttonTexts", BindingFlags.Instance | BindingFlags.NonPublic);
        var finishField = viewControllerType.GetField("_didFinishAction", BindingFlags.Instance | BindingFlags.NonPublic);

        if (buttonsField?.GetValue(viewController) is not Button[] buttons || buttons.Length == 0)
            return false;

        var texts = textsField?.GetValue(viewController) as TextMeshProUGUI[];
        var dismissIndex = FindLabelButtonIndexOrNegative(buttons, texts, DialogDismissLabelNeedles);
        if (dismissIndex >= 0)
        {
            if (TryPressButton(buttons[dismissIndex]))
                return true;

            if (finishField?.GetValue(viewController) is Action<int> finishAction)
            {
                try
                {
                    finishAction.Invoke(dismissIndex);
                    return true;
                }
                catch
                {
                }
            }
        }

        return TryPressButtonByLabelNeedles(DialogDismissLabelNeedles);
    }

    private static bool TryCancelDisconnectPrompt()
    {
        if (!IsDisconnectPromptVisible())
            return false;

        var view = MpUiReflection.FindBestActiveObject(DisconnectPromptType);
        if (view == null)
            return false;

        if (TryPressFieldButton(view, DisconnectPromptType, "_cancelButton"))
            return true;

        return TryPressFieldButton(view, DisconnectPromptType, "_okButton");
    }

    private static bool TryPressMultiplayerResultsBackToMenu()
    {
        var mpResults = MpUiReflection.FindBestActiveObject(MpResultsVcType);
        if (mpResults == null)
            return false;

        if (MpUiReflection.TryInvokeParameterless(mpResults, "BackToMenuPressed"))
            return true;

        var lobbyFc = MpUiReflection.GetBestFlowCoordinator(LobbyFcType, "_lobbySetupViewController");
        if (lobbyFc != null
            && MpUiReflection.TryInvoke(lobbyFc, "HandleMultiplayerResultsViewControllerBackToMenuPressed", mpResults))
            return true;

        return TryPressFieldButton(mpResults, MpResultsVcType, "_backToMenuButton");
    }

    private static bool TryPressScreenBackButton()
    {
        if (TryPressBackOnFlowCoordinator(MpFcType))
            return true;
        if (TryPressBackOnFlowCoordinator(LobbyFcType))
            return true;
        if (TryPressBackOnFlowCoordinator(MainFcType))
            return true;
        return TryPressTitleBackButton();
    }

    private static bool TryPressBackOnFlowCoordinator(Type? fcType)
    {
        var fc = MpUiReflection.GetBestFlowCoordinator(fcType, null);
        if (fc == null || !FlowCoordinatorCanPressBack(fc))
            return false;
        return TryInvokeFlowCoordinatorBack(fc);
    }

    private static bool FlowCoordinatorCanPressBack(object flowCoordinator)
    {
        var showBack = flowCoordinator.GetType().GetProperty(
            "showBackButton",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (showBack?.GetValue(flowCoordinator) is bool visible && !visible)
            return false;

        for (var type = flowCoordinator.GetType(); type != null; type = type.BaseType)
        {
            var canPress = type.GetMethod(
                "CanPressBackButton",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (canPress == null || canPress.GetParameters().Length != 0)
                continue;

            try
            {
                return (bool)canPress.Invoke(flowCoordinator, null)!;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryInvokeFlowCoordinatorBack(object flowCoordinator)
    {
        for (var type = flowCoordinator.GetType(); type != null; type = type.BaseType)
        {
            var handleBack = type.GetMethod(
                "HandleScreenSystemBackButtonWasPressed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (handleBack == null || handleBack.GetParameters().Length != 0)
                continue;

            try
            {
                handleBack.Invoke(flowCoordinator, null);
                return true;
            }
            catch
            {
            }
        }

        var topVc = GetFlowCoordinatorTopViewController(flowCoordinator);
        return topVc != null && MpUiReflection.TryInvoke(flowCoordinator, "BackButtonWasPressed", topVc);
    }

    private static object? GetFlowCoordinatorTopViewController(object flowCoordinator)
    {
        for (var type = flowCoordinator.GetType(); type != null; type = type.BaseType)
        {
            var topProp = type.GetProperty(
                "topViewController",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (topProp != null)
                return topProp.GetValue(flowCoordinator);
        }

        return null;
    }

    private static bool TryPressTitleBackButton()
    {
        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!IsSceneButton(button))
                continue;
            if (!string.Equals(button.gameObject.name, "BackButton", StringComparison.Ordinal))
                continue;
            if (!IsUnderTitleViewController(button.transform))
                continue;
            if (!button.interactable)
                continue;
            return TryPressButton(button);
        }

        return false;
    }

    private static bool IsUnderTitleViewController(Transform transform)
    {
        for (var cur = transform; cur != null; cur = cur.parent)
        {
            if (cur.name.IndexOf("TitleViewController", StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    private static bool GetVisibleSimpleDialog(out object? viewController, out Type? viewControllerType)
    {
        viewController = null;
        viewControllerType = SimpleDialogType;
        if (viewControllerType == null)
            return false;

        foreach (var vc in MpUiReflection.FindAllInLoadedScenes(viewControllerType, requireHierarchy: true))
        {
            if (!HasVisibleSimpleDialogButton(vc, viewControllerType))
                continue;
            viewController = vc;
            return true;
        }

        return false;
    }

    private static bool HasVisibleSimpleDialogButton(object viewController, Type viewControllerType)
    {
        var buttonsField = viewControllerType.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        if (buttonsField?.GetValue(viewController) is not Button[] buttons)
            return false;

        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private static bool IsDisclaimerDialog(object viewController, Type viewControllerType)
    {
        var buttonsField = viewControllerType.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        var textsField = viewControllerType.GetField("_buttonTexts", BindingFlags.Instance | BindingFlags.NonPublic);
        if (buttonsField?.GetValue(viewController) is not Button[] buttons)
            return false;

        var texts = textsField?.GetValue(viewController) as TextMeshProUGUI[];
        return FindLabelButtonIndexOrNegative(buttons, texts, new[] { "agree", "accept" }) >= 0;
    }

    private static bool IsDisclaimerVisible()
    {
        var type = SimpleDialogType;
        var vc = MpUiReflection.GetFlowCoordinatorField(MainFcType, "_simpleDialogPromptViewController")
            ?? MpUiReflection.FindBestActiveObject(type);
        if (vc == null || type == null)
            return false;

        var buttonsField = type.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        if (buttonsField?.GetValue(vc) is not Button[] buttons || buttons.Length == 0)
            return false;

        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private static bool IsDisconnectPromptVisible()
    {
        var view = MpUiReflection.FindBestActiveObject(DisconnectPromptType);
        if (view == null)
            return false;

        var promptField = DisconnectPromptType!.GetField("_promptGameObject", BindingFlags.Instance | BindingFlags.NonPublic);
        if (promptField?.GetValue(view) is GameObject prompt && prompt != null)
            return prompt.activeInHierarchy;

        return false;
    }

    private static bool TryInvokeContinueHandlers()
    {
        Type?[] types = { ResultsVcType, MissionResultsVcType, MpResultsVcType };
        foreach (var type in types)
        {
            if (type == null)
                continue;

            foreach (var vc in MpUiReflection.FindAllInLoadedScenes(type, requireHierarchy: true))
            {
                foreach (var methodName in ContinueHandlerMethodNames)
                {
                    if (MpUiReflection.TryInvokeParameterless(vc, methodName))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryFinishSimpleDialogContinue()
    {
        var type = SimpleDialogType;
        var vc = MpUiReflection.GetFlowCoordinatorField(MpFcType, "_simpleDialogPromptViewController")
            ?? MpUiReflection.GetFlowCoordinatorField(LobbyFcType, "_simpleDialogPromptViewController")
            ?? MpUiReflection.GetFlowCoordinatorField(MainFcType, "_simpleDialogPromptViewController")
            ?? MpUiReflection.FindBestActiveObject(type);
        if (vc == null || type == null)
            return false;

        var buttonsField = type.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        var textsField = type.GetField("_buttonTexts", BindingFlags.Instance | BindingFlags.NonPublic);
        var finishField = type.GetField("_didFinishAction", BindingFlags.Instance | BindingFlags.NonPublic);

        if (buttonsField?.GetValue(vc) is not Button[] buttons || buttons.Length == 0)
            return false;

        var texts = textsField?.GetValue(vc) as TextMeshProUGUI[];
        var continueIndex = FindLabelButtonIndex(buttons, texts, new[] { "continue" });

        if (TryPressButton(buttons[continueIndex]))
            return true;

        if (finishField?.GetValue(vc) is Action<int> finishAction)
        {
            try
            {
                finishAction.Invoke(continueIndex);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool TryPressDisconnectPromptOk()
    {
        var view = MpUiReflection.FindBestActiveObject(DisconnectPromptType);
        if (view == null)
            return false;

        var promptField = DisconnectPromptType!.GetField("_promptGameObject", BindingFlags.Instance | BindingFlags.NonPublic);
        if (promptField?.GetValue(view) is GameObject prompt
            && prompt != null
            && !prompt.activeInHierarchy)
            return false;

        return TryPressFieldButton(view, DisconnectPromptType, "_okButton");
    }

    private static bool TryPressJoinOnViewController(object vc, Type? vcType)
    {
        if (MpUiReflection.TryInvoke(vc, "ButtonPressed", true))
            return true;
        if (TryPressFieldButton(vc, vcType, "_joinButton"))
            return true;
        return MpUiReflection.TryInvokeParameterless(vc, JoinQuickPlayClickHandler);
    }

    private static bool TryPressJoinButtonByLabel()
    {
        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!IsSceneButton(button))
                continue;

            var labelText = GetButtonLabelText(button);
            if (string.IsNullOrEmpty(labelText))
                continue;
            if (labelText!.IndexOf("join", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (labelText.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (TryPressButton(button))
                return true;
        }

        return false;
    }

    private static bool TryPressButtonByLabelNeedles(string[] needles)
    {
        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!IsSceneButton(button))
                continue;

            var labelText = GetButtonLabelText(button);
            if (string.IsNullOrEmpty(labelText))
                continue;

            foreach (var needle in needles)
            {
                if (string.IsNullOrEmpty(needle))
                    continue;
                if (labelText!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (TryPressButton(button))
                    return true;
            }
        }

        return false;
    }

    private static bool IsFieldButtonVisible(object? target, Type? type, string fieldName)
    {
        if (target == null || type == null)
            return false;

        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(target) is not Button button)
            return false;

        return button.gameObject.activeInHierarchy;
    }

    private static bool TryPressFieldButton(object? target, Type? type, string fieldName)
    {
        if (target == null || type == null)
            return false;

        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && TryPressButton(field.GetValue(target) as Button);
    }

    private static bool TryPressButton(Button? button)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
            return false;

        if (!button.interactable)
            button.interactable = true;

        if (ButtonPressMethod != null)
        {
            try
            {
                ButtonPressMethod.Invoke(button, null);
                return true;
            }
            catch
            {
            }
        }

        try
        {
            button.onClick?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSceneButton(Button? button)
    {
        if (button == null)
            return false;

        var go = button.gameObject;
        if (!go.activeInHierarchy)
            return false;

        var scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private static string? GetButtonLabelText(Button button)
    {
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;
        }

        return null;
    }

    private static int FindLabelButtonIndex(Button[] buttons, TextMeshProUGUI[]? texts, string[] needles)
    {
        var index = FindLabelButtonIndexOrNegative(buttons, texts, needles);
        return index >= 0 ? index : 0;
    }

    private static int FindLabelButtonIndexOrNegative(Button[] buttons, TextMeshProUGUI[]? texts, string[] needles)
    {
        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].gameObject.activeInHierarchy)
                continue;

            var label = texts != null && i < texts.Length && texts[i] != null
                ? texts[i].text
                : GetButtonLabelText(buttons[i]);

            if (string.IsNullOrEmpty(label))
                continue;

            foreach (var needle in needles)
            {
                if (string.IsNullOrEmpty(needle))
                    continue;
                if (label!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
        }

        return -1;
    }

    private static Type? MainMenuVcType =>
        _mainMenuVcType ??= MpUiReflection.ResolveType("MainMenuViewController");

    private static Type? MainFcType =>
        _mainFcType ??= MpUiReflection.ResolveType("MainFlowCoordinator");

    private static Type? MpFcType =>
        _mpFcType ??= MpUiReflection.ResolveType("MultiplayerModeSelectionFlowCoordinator");

    private static Type? MpVcType =>
        _mpVcType ??= MpUiReflection.ResolveType("MultiplayerModeSelectionViewController");

    private static Type? JoinVcType =>
        _joinVcType ??= MpUiReflection.ResolveType("JoinQuickPlayViewController");

    private static Type? JoiningVcType =>
        _joiningVcType ??= MpUiReflection.ResolveType("JoiningLobbyViewController");

    private static Type? SimpleDialogType =>
        _simpleDialogType ??= MpUiReflection.ResolveType("SimpleDialogPromptViewController");

    private static Type? DisconnectPromptType =>
        _disconnectPromptType ??= MpUiReflection.ResolveType("DisconnectPromptView");

    private static Type? LobbyFcType =>
        _lobbyFcType ??= MpUiReflection.ResolveType("GameServerLobbyFlowCoordinator");

    private static Type? ResultsVcType =>
        _resultsVcType ??= MpUiReflection.ResolveType("ResultsViewController");

    private static Type? MissionResultsVcType =>
        _missionResultsVcType ??= MpUiReflection.ResolveType("MissionResultsViewController");

    private static Type? MpResultsVcType =>
        _mpResultsVcType ??= MpUiReflection.ResolveType("MultiplayerResultsViewController");
}
