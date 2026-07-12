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
        TryPatch(harmony, typeof(IgnoreUserPresenceCanceledPatch));
        TryPatch(harmony, typeof(IgnoreUnityXrApplicationPausePatch));
        TryPatch(harmony, typeof(IgnoreVrFocusLossPatch));
        TryPatch(harmony, typeof(AlwaysHasInputFocusPatch));
        TryPatch(harmony, typeof(AlwaysHasVrFocusPatch));
        TryPatch(harmony, typeof(SongPreviewIgnoreInputFocusCapturedPatch));
        TryPatch(harmony, typeof(LevelGridIgnoreInputFocusCapturedPatch));
        TryPatch(harmony, typeof(IgnoreAnimatorFocusCapturePatch));
        TryPatch(harmony, typeof(CustomAvatarAlwaysHasFocusPatch));
        TryPatch(harmony, typeof(CustomAvatarTrackingRigKeepEnabledPatch));
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

    [HarmonyPatch]
    private static class KeepControllersActiveOnFocusCapturePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(DeactivateVRControllersOnFocusCapture), "UpdateVRControllerActiveState")!;

        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    private static class IgnoreUserPresenceLossPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(UnityXRHelper), "set_userPresence")!;

        private static bool Prefix(bool value) => value;
    }

    [HarmonyPatch]
    private static class IgnoreUserPresenceCanceledPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(UnityXRHelper), "OnUserPresenceCanceled")!;

        private static bool Prefix() => false;
    }

    // SteamVR overlay triggers Unity pause on the XR helper; that fires vrFocusWasCapturedEvent
    [HarmonyPatch]
    private static class IgnoreUnityXrApplicationPausePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(UnityXRHelper), "OnApplicationPause")!;

        private static bool Prefix(bool pauseStatus) => !pauseStatus;
    }

    [HarmonyPatch]
    private static class IgnoreVrFocusLossPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(UnityXRHelper), "set_hasVrFocus")!;

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

    [HarmonyPatch]
    private static class IgnoreAnimatorFocusCapturePatch
    {
        private static MethodBase? TargetMethod() =>
            AccessTools.Method(typeof(DeactivateAnimatorOnInputFocusCapture), "HandleInputFocusCaptured");

        private static bool Prefix() => false;
    }

    // Custom Avatars disables VRController behaviours when it thinks focus is lost
    [HarmonyPatch]
    private static class CustomAvatarAlwaysHasFocusPatch
    {
        private static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("CustomAvatar.Utilities.BeatSaberUtilities");
            return type == null ? null : AccessTools.DeclaredMethod(type, "get_hasFocus");
        }

        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    private static class CustomAvatarTrackingRigKeepEnabledPatch
    {
        private static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("CustomAvatar.Tracking.TrackingRig");
            return type == null ? null : AccessTools.DeclaredMethod(type, "UpdateBehaviourEnabled");
        }

        private static bool Prefix(object __instance)
        {
            try
            {
                var behaviour = __instance as UnityEngine.Behaviour;
                if (behaviour != null)
                    behaviour.enabled = true;

                var left = AccessTools.PropertyGetter(__instance.GetType(), "leftHand")?.Invoke(__instance, null);
                var right = AccessTools.PropertyGetter(__instance.GetType(), "rightHand")?.Invoke(__instance, null);
                EnableController(left);
                EnableController(right);
            }
            catch
            {
                // keep trying to skip the original disable path
            }

            return false;
        }

        private static void EnableController(object? node)
        {
            if (node == null)
                return;
            var controller = AccessTools.PropertyGetter(node.GetType(), "controller")?.Invoke(node, null)
                as UnityEngine.Behaviour;
            if (controller != null)
                controller.enabled = true;
        }
    }
}
