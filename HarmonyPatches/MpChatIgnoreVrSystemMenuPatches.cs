using System;
using System.Reflection;
using HarmonyLib;

namespace MultiplayerChat.HarmonyPatches;

// SteamVR / system dashboard steals VR input focus and user presence; Beat Saber
// pauses, fails multiplayer, and deactivates controllers. no-op all of that.
internal static class MpChatIgnoreVrSystemMenuPatches
{
    internal static void Apply(Harmony harmony)
    {
        TryPatch(harmony, typeof(PauseControllerIgnoreHmdUnmountPatch));
        TryPatch(harmony, typeof(PauseControllerIgnoreFocusCapturedPatch));
        TryPatch(harmony, typeof(PauseControllerIgnoreApplicationPausePatch));
        TryPatch(harmony, typeof(MultiplayerGameplayIgnoreHmdUnmountPatch));
        TryPatch(harmony, typeof(MultiplayerGameplayIgnoreInputFocusCapturedPatch));
        TryPatch(harmony, typeof(MultiplayerGameplayIgnoreVrFocusCapturedPatch));
        TryPatch(harmony, typeof(MultiplayerInGameMenuIgnoreInputFocusCapturedPatch));
        TryPatch(harmony, typeof(MultiplayerInGameMenuIgnoreApplicationPausePatch));
        TryPatch(harmony, typeof(KeepControllersActiveOnFocusCapturePatch));
        TryPatch(harmony, typeof(IgnoreUserPresenceLossPatch));
        TryPatch(harmony, typeof(AlwaysHasInputFocusPatch));
        TryPatch(harmony, typeof(AlwaysHasVrFocusPatch));
        TryPatch(harmony, typeof(SongPreviewIgnoreInputFocusCapturedPatch));
        TryPatch(harmony, typeof(LevelGridIgnoreInputFocusCapturedPatch));
    }

    private static void TryPatch(Harmony harmony, Type patchType)
    {
        try
        {
            harmony.CreateClassProcessor(patchType).Patch();
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"[MPChat][VRMenu] Ignore system menu patch {patchType.Name} failed: {ex.Message}");
        }
    }

    [HarmonyPatch]
    private static class PauseControllerIgnoreHmdUnmountPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(PauseController), "HandleHMDUnmounted")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class PauseControllerIgnoreFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(PauseController), "HandleFocusWasCaptured")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class PauseControllerIgnoreApplicationPausePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(PauseController), "OnApplicationPause")!;

        // block pause-on-background only; allow resume when focus returns.
        private static bool Prefix(bool pauseStatus) => !pauseStatus;
    }

    [HarmonyPatch]
    private static class MultiplayerGameplayIgnoreHmdUnmountPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(MultiplayerLocalActivePlayerGameplayManager), "HandleHmdUnmounted")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class MultiplayerGameplayIgnoreInputFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(MultiplayerLocalActivePlayerGameplayManager), "HandleInputFocusCaptured")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class MultiplayerGameplayIgnoreVrFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(MultiplayerLocalActivePlayerGameplayManager), "HandleVrFocusWasCapturedEvent")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class MultiplayerInGameMenuIgnoreInputFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(MultiplayerLocalActivePlayerInGameMenuController), "HandleInputFocusWasCaptured")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class MultiplayerInGameMenuIgnoreApplicationPausePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(MultiplayerLocalActivePlayerInGameMenuController), "OnApplicationPause")!;

        private static bool Prefix(bool pauseStatus) => !pauseStatus;
    }

    // SteamVR overlay clears user presence; that deactivates controller GameObjects and freezes hands
    [HarmonyPatch]
    private static class KeepControllersActiveOnFocusCapturePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(DeactivateVRControllersOnFocusCapture), "UpdateVRControllerActiveState")!;

        private static bool Prefix() => false;
    }

    // SteamVR dashboard cancels OpenXR user presence; swallow loss so capture/hmd-unmount events never fire
    [HarmonyPatch]
    private static class IgnoreUserPresenceLossPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(UnityXRHelper), "set_userPresence")!;

        private static bool Prefix(bool value) => value;
    }

    [HarmonyPatch]
    private static class AlwaysHasInputFocusPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.DeclaredMethod(typeof(UnityXRHelper), "get_hasInputFocus")!;

        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    private static class AlwaysHasVrFocusPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.DeclaredMethod(typeof(UnityXRHelper), "get_hasVrFocus")!;

        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    private static class SongPreviewIgnoreInputFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(SongPreviewPlayerPauseOnInputFocusLost), "HandleInputFocusCaptured")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class LevelGridIgnoreInputFocusCapturedPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(AnnotatedBeatmapLevelCollectionsGridView), "HandleVRPlatformHelperInputFocusCaptured")!;

        private static bool Prefix() => false;
    }
}
