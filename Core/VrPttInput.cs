using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MultiplayerChat.Core;

/// <summary>
/// VR push-to-talk cross-referenced with EasyOffset (<see href="https://github.com/Reezonate/EasyOffset"/>):
/// <list type="bullet">
/// <item><description><b>Devices:</b> EasyOffset <c>ReeInputDevice</c> uses <see cref="InputDevices.GetDevicesAtXRNode"/> and
/// <c>devices[0]</c> per hand  -  we do the same via <see cref="PrimaryDeviceAt"/> (not <see cref="InputDevices.GetDeviceAtXRNode"/>,
/// which can disagree when multiple devices are registered).</description></item>
/// <item><description><b>Buttons:</b> same Unity <see cref="CommonUsages"/> booleans EasyOffset maps from feature names
/// (<c>PrimaryButton</c>, <c>SecondaryButton</c>, <c>GripButton</c>).</description></item>
/// <item><description><b>Trigger:</b> EasyOffset <c>ReeInputManager</c> uses <c>IVRPlatformHelper.GetTriggerValue</c> with threshold
/// <c>0.1f</c>. We use that threshold on <see cref="CommonUsages.trigger"/> / <see cref="CommonUsages.triggerButton"/> on the
/// same primary devices (SteamVR/OpenXR still expose analog trigger on those).</description></item>
/// </list>
/// </summary>
public static class VrPttInput
{
    /// <summary>Matches EasyOffset <c>ReeInputManager.TriggerThreshold</c> (0.1f).</summary>
    private const float TriggerPressThreshold = 0.1f;

    private static readonly List<InputDevice> DeviceBuffer = new(4);

    private static InputDevice PrimaryDeviceAt(XRNode node)
    {
        DeviceBuffer.Clear();
        InputDevices.GetDevicesAtXRNode(node, DeviceBuffer);
        return DeviceBuffer.Count > 0 ? DeviceBuffer[0] : default;
    }

    /// <summary>True if either hand controller is currently tracked (PTT can be evaluated).</summary>
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

    /// <param name="bindingIndex">0 primary, 1 secondary, 2 trigger, 3 grip (same order as the voice settings PTT dropdown / PlayerPrefs).</param>
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

    /// <summary>One-line report of Primary / Secondary / Trigger / Grip on both hands (for temporary diagnostics).</summary>
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

    /// <summary>Extra detail for <see cref="VoiceHotMicManager"/> PTT logs (controllers + analog trigger).</summary>
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
