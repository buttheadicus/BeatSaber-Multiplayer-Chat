using BeatSaber.AvatarCore;
using MultiplayerChat.Core.Addons;
using System.Reflection;
using UnityEngine;

namespace MultiplayerChat.Core;

// FPFC / Deck: copy cached Custom Avatars TrackingRig into the local lobby pose bones (no scene scans).
internal static class MpChatLocalPlayerPoseBridge
{
    private static FieldInfo? _headField;

    private static FieldInfo? _rightSaberField;

    private static FieldInfo? _leftSaberField;

    private static MultiplayerAvatarPoseController? _localPose;

    internal static void SetLocalTarget(MultiplayerAvatarPoseController? pose) => _localPose = pose;

    internal static bool TargetIs(MultiplayerAvatarPoseController? pose) => _localPose == pose;

    internal static void ClearLocalTarget()
    {
        _localPose = null;
    }

    internal static void TickCached()
    {
        var pose = _localPose;
        if (pose == null || !pose.gameObject.activeInHierarchy)
            return;

        if (!AddonCustomAvatarsBridge.TryGetLocalCaWorldDevicePoses(
                out var headWorld,
                out var headWorldRot,
                out var rightWorld,
                out var rightWorldRot,
                out var leftWorld,
                out var leftWorldRot))
            return;

        if (!EnsurePoseFields())
            return;

        ApplyWorldPoseToBones(pose, headWorld, headWorldRot, rightWorld, rightWorldRot, leftWorld, leftWorldRot);
    }

    private static void ApplyWorldPoseToBones(
        MultiplayerAvatarPoseController pose,
        Vector3 headWorld,
        Quaternion headWorldRot,
        Vector3 rightWorld,
        Quaternion rightWorldRot,
        Vector3 leftWorld,
        Quaternion leftWorldRot)
    {
        if (_headField?.GetValue(pose) is Transform head)
        {
            head.position = headWorld;
            head.rotation = headWorldRot;
        }

        if (_rightSaberField?.GetValue(pose) is Transform rightSaber)
        {
            rightSaber.position = rightWorld;
            rightSaber.rotation = rightWorldRot;
        }

        if (_leftSaberField?.GetValue(pose) is Transform leftSaber)
        {
            leftSaber.position = leftWorld;
            leftSaber.rotation = leftWorldRot;
        }
    }

    private static bool EnsurePoseFields()
    {
        if (_headField != null)
            return true;

        var poseType = typeof(MultiplayerAvatarPoseController);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _headField = poseType.GetField("_headTransform", flags);
        _rightSaberField = poseType.GetField("_rightSaberTransform", flags);
        _leftSaberField = poseType.GetField("_leftSaberTransform", flags);
        return _headField != null && _rightSaberField != null && _leftSaberField != null;
    }
}
