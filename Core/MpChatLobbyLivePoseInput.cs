using BeatSaber.AvatarCore;
using CustomAvatar.Tracking;
using System;
using System.Reflection;
using UnityEngine;

namespace MultiplayerChat.Core;

internal sealed class MpChatLobbyLivePoseInput : IAvatarInput
{
    private Action? _inputChangedHandlers;

    event Action IAvatarInput.inputChanged
    {
        add => _inputChangedHandlers += value;
        remove => _inputChangedHandlers -= value;
    }

    public bool allowMaintainPelvisPosition => true;

    private readonly MultiplayerAvatarPoseController _poseController;

    private readonly Transform _headTransform;
    private readonly Transform _rightHandTransform;
    private readonly Transform _leftHandTransform;
    private readonly Transform _bodyTransform;

    private readonly Transform _proxyHead;
    private readonly Transform _proxyRight;
    private readonly Transform _proxyLeft;

    private UnityEngine.Pose _head = new();
    private UnityEngine.Pose _rightHand = new();
    private UnityEngine.Pose _leftHand = new();

    internal MpChatLobbyLivePoseInput(MultiplayerAvatarPoseController poseController)
    {
        _poseController = poseController;

        _poseController.didUpdatePoseEvent += OnInputChanged;
        _headTransform = ReqTransformField(poseController, "_headTransform");
        _rightHandTransform = ReqTransformField(poseController, "_rightSaberTransform");
        _leftHandTransform = ReqTransformField(poseController, "_leftSaberTransform");
        _bodyTransform = poseController.transform.Find("Body") ?? poseController.transform;

        var root = poseController.transform;
        _proxyHead = new GameObject("MpChatLobbyPoseProxyHead").transform;
        _proxyHead.SetParent(root, false);
        _proxyRight = new GameObject("MpChatLobbyPoseProxyRH").transform;
        _proxyRight.SetParent(root, false);
        _proxyLeft = new GameObject("MpChatLobbyPoseProxyLH").transform;
        _proxyLeft.SetParent(root, false);

        SetEnabled(true);
    }

    private static Transform ReqTransformField(MultiplayerAvatarPoseController owner, string fieldName)
    {
        var f = typeof(MultiplayerAvatarPoseController).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (f?.GetValue(owner) is Transform t)
            return t;

        throw new MissingFieldException(nameof(MultiplayerAvatarPoseController), fieldName);
    }

    public void SetEnabled(bool enabled)
    {
        _headTransform.gameObject.SetActive(!enabled);
        _bodyTransform.gameObject.SetActive(!enabled);
        var rh = _rightHandTransform.Find("Hand");
        var lh = _leftHandTransform.Find("Hand");
        if (rh != null)
            rh.gameObject.SetActive(!enabled);
        if (lh != null)
            lh.gameObject.SetActive(!enabled);
    }

    private void OnInputChanged(Vector3 newHeadPosition)
    {
        _head.position = newHeadPosition;
        _head.rotation = _headTransform.localRotation;
        _rightHand.position = _rightHandTransform.localPosition;
        _rightHand.rotation = _rightHandTransform.localRotation;
        _leftHand.position = _leftHandTransform.localPosition;
        _leftHand.rotation = _leftHandTransform.localRotation;

        if (_rightHand.position == _head.position)
            _rightHand.position += Vector3.one * 0.1f;
        if (_rightHand.rotation == _head.rotation)
            _rightHand.rotation *= Quaternion.identity;
        if (_leftHand.position == _head.position)
            _leftHand.position += Vector3.one * -0.1f;
        if (_leftHand.rotation == _head.rotation)
            _leftHand.rotation *= Quaternion.identity;

        _proxyHead.localPosition = _head.position;
        _proxyHead.localRotation = _head.rotation;
        _proxyRight.localPosition = _rightHand.position;
        _proxyRight.localRotation = _rightHand.rotation;
        _proxyLeft.localPosition = _leftHand.position;
        _proxyLeft.localRotation = _leftHand.rotation;

        _inputChangedHandlers?.Invoke();
    }

    public bool TryGetFingerCurl(DeviceUse use, out FingerCurl curl)
    {
        curl = new FingerCurl(0f, 0f, 0f, 0f, 0f);
        return false;
    }

    public bool TryGetTransform(DeviceUse use, out Transform transform)
    {
        switch (use)
        {
            case DeviceUse.Head:
                transform = _proxyHead;
                return true;
            case DeviceUse.RightHand:
                transform = _proxyRight;
                return true;
            case DeviceUse.LeftHand:
                transform = _proxyLeft;
                return true;
            default:
                transform = null!;
                return false;
        }
    }
}
