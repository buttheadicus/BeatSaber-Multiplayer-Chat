using System;
using System.Reflection;
using BeatSaber.AvatarCore;
using HarmonyLib;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.HarmonyPatches;

// MPEX DisableAvatarConstraints skips RestrictPose then still clamps hands via
// LimitHandPositionRelativeToHead. HarmonyX also skips later prefixes when MPEX
// returns false, so we must run first and/or neuter LimitHand itself.
internal static class MpChatAvatarHandLimitPatch
{
    internal static void Apply(Harmony harmony)
    {
        try
        {
            harmony.CreateClassProcessor(typeof(UnlimitedRestrictPosePatch)).Patch();
            harmony.CreateClassProcessor(typeof(UnlimitedHandLimitPatch)).Patch();
            Plugin.Log?.Info("[MPChat] Avatar pose hand limit unlock patch ready (gated by settings).");
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"[MPChat] Avatar hand limit unlock failed: {ex.Message}");
        }
    }

    // beat MPEX under HarmonyX: first prefix to return false wins, later ones never run
    [HarmonyPatch(typeof(LimitAvatarPoseRestriction), nameof(LimitAvatarPoseRestriction.RestrictPose))]
    [HarmonyPriority(Priority.First)]
    private static class UnlimitedRestrictPosePatch
    {
        private static bool Prefix(
            Quaternion headRotation,
            Vector3 headPosition,
            Vector3 leftHandPosition,
            Vector3 rightHandPosition,
            out Vector3 newHeadPosition,
            out Vector3 newLeftHandPosition,
            out Vector3 newRightHandPosition)
        {
            if (!ModSettings.UnlockAvatarHandPositions)
            {
                newHeadPosition = default;
                newLeftHandPosition = default;
                newRightHandPosition = default;
                return true;
            }

            newHeadPosition = headPosition;
            newLeftHandPosition = leftHandPosition;
            newRightHandPosition = rightHandPosition;
            return false;
        }
    }

    // MPEX still calls this from its RestrictPose prefix; param is headCenter not headPosition
    [HarmonyPatch]
    private static class UnlimitedHandLimitPatch
    {
        private static MethodBase? TargetMethod() =>
            AccessTools.Method(typeof(LimitAvatarPoseRestriction), "LimitHandPositionRelativeToHead");

        private static bool Prefix(Vector3 handPosition, Vector3 headCenter, ref Vector3 __result)
        {
            if (!ModSettings.UnlockAvatarHandPositions)
                return true;

            __result = handPosition;
            return false;
        }
    }
}
