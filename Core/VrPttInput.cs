using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MultiplayerChat.Core;

public static class VrPttInput
{
    private const float TriggerPressThreshold = 0.1f;

    private static readonly List<InputDevice> DeviceBuffer = new(4);

    private static InputDevice PrimaryDeviceAt(XRNode node)
    {
        DeviceBuffer.Clear();
        InputDevices.GetDevicesAtXRNode(node, DeviceBuffer);
        return DeviceBuffer.Count > 0 ? DeviceBuffer[0] : default;
    }

    public static bool HasAnyHandDeviceValid()
    {
        try
        {
            return PrimaryDeviceAt(XRNode.LeftHand).isValid || PrimaryDeviceAt(XRNode.RightHand).isValid;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsBindingHeld(int bindingIndex)
    {
        var idx = Mathf.Clamp(bindingIndex, 0, 3);
        try
        {
            var left = PrimaryDeviceAt(XRNode.LeftHand);
            var right = PrimaryDeviceAt(XRNode.RightHand);
            return idx switch
            {
                0 => Get(left, CommonUsages.primaryButton) || Get(right, CommonUsages.primaryButton),
                1 => Get(left, CommonUsages.secondaryButton) || Get(right, CommonUsages.secondaryButton),
                2 => TriggerHeld(left) || TriggerHeld(right),
                3 => Get(left, CommonUsages.gripButton) || Get(right, CommonUsages.gripButton),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool TriggerHeld(InputDevice d)
    {
        if (!d.isValid) return false;
        if (d.TryGetFeatureValue(CommonUsages.triggerButton, out var b) && b)
            return true;
        if (d.TryGetFeatureValue(CommonUsages.trigger, out var t) && t > TriggerPressThreshold)
            return true;
        return false;
    }

    private static bool Get(InputDevice d, InputFeatureUsage<bool> usage) =>
        d.isValid && d.TryGetFeatureValue(usage, out var v) && v;

    private static string FormatBoolToken(bool v) => v ? "y" : "n";

    public static string BuildRawControllerBindingsDiagnosticLine()
    {
        try
        {
            var left = PrimaryDeviceAt(XRNode.LeftHand);
            var right = PrimaryDeviceAt(XRNode.RightHand);
            return $"L[{FormatHandFaceButtons(left)}] R[{FormatHandFaceButtons(right)}]";
        }
        catch (Exception ex)
        {
            return $"xr-error {ex.GetType().Name}";
        }
    }

    private static string FormatHandFaceButtons(InputDevice d)
    {
        if (!d.isValid)
            return "off";

        d.TryGetFeatureValue(CommonUsages.primaryButton, out var pri);
        d.TryGetFeatureValue(CommonUsages.secondaryButton, out var sec);
        d.TryGetFeatureValue(CommonUsages.gripButton, out var grip);
        d.TryGetFeatureValue(CommonUsages.triggerButton, out var triBtn);
        d.TryGetFeatureValue(CommonUsages.trigger, out var triAx);

        return $"Pri={FormatBoolToken(pri)} Sec={FormatBoolToken(sec)} Grip={FormatBoolToken(grip)} TrigBtn={FormatBoolToken(triBtn)} TrigAx={triAx:0.###}";
    }

    public static string FormatDiagnosticsSuffix(int bindingIndex)
    {
        try
        {
            var left = PrimaryDeviceAt(XRNode.LeftHand);
            var right = PrimaryDeviceAt(XRNode.RightHand);
            left.TryGetFeatureValue(CommonUsages.trigger, out var lt);
            right.TryGetFeatureValue(CommonUsages.trigger, out var rt);
            return $" Lvalid={left.isValid} Rvalid={right.isValid} trigL={lt:0.###} trigR={rt:0.###}";
        }
        catch
        {
            return "";
        }
    }
}
