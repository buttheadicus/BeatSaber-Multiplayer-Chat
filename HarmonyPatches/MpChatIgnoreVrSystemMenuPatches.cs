using System;
using System.Reflection;
using HarmonyLib;

namespace MultiplayerChat.HarmonyPatches;

// SteamVR / system dashboard steals VR input focus; Beat Saber treats that as pause or multiplayer fail.
// These patches no-op the focus/HMD handlers so opening the system menu does not affect gameplay.
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

        // Block pause-on-background only; allow resume when focus returns.
        private static bool Prefix(bool pause) => !pause;
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

        private static bool Prefix(bool pause) => !pause;
    }
}
