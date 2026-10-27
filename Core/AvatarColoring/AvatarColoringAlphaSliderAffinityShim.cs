using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using HMUI;
using MultiplayerChat.Core.Addons;
using SiraUtil.Affinity;
using UnityEngine;

namespace MultiplayerChat.Core.AvatarColoring;

internal sealed class AvatarColoringAlphaSliderAffinityShim : IAffinity
{
    private const string AddonId = "avatarColoring";
    private const string PatcherTypeName = "MultiplayerChat.AvatarColoring.AvatarColoringAlphaSliderPatcher";

    [AffinityPatch(typeof(ViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidActivate(
        ViewController __instance,
        bool firstActivation,
        bool addedToHierarchy,
        bool screenSystemEnabling)
    {
        if (__instance is not EditAvatarColorViewController colorEditor)
            return;

        var patcher = CreateAlphaSliderPatcher(colorEditor);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixEditColorDidActivate",
            firstActivation,
            addedToHierarchy,
            screenSystemEnabling);
    }

    [AffinityPatch(typeof(ViewController), "DidDeactivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidDeactivate(
        ViewController __instance,
        bool removedFromHierarchy,
        bool screenSystemDisabling)
    {
        if (__instance is not EditAvatarColorViewController colorEditor)
            return;

        var patcher = CreateAlphaSliderPatcher(colorEditor);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixEditColorDidDeactivate",
            removedFromHierarchy,
            screenSystemDisabling);
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(EditAvatarColorViewController), "ChangeColor")]
    public bool PrefixDeferChangeColorUntilApply(EditAvatarColorViewController __instance, Color color)
    {
        var patcher = CreateAlphaSliderPatcher(__instance);
        return AddonAffinityShimBridge.InvokePrefixOrTrue(patcher, "PrefixDeferChangeColorUntilApply", __instance, color);
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(BeatAvatarEditorViewController), "SaveColorChange")]
    public bool PrefixDeferSaveColorChangeWhileDraft(AvatarPart avatarEditPart)
    {
        return AddonAffinityShimBridge.InvokeStaticPrefixOrTrue(
            AddonId,
            PatcherTypeName,
            "TryPrefixDeferSaveColorChangeWhileDraft",
            avatarEditPart);
    }

    private static object? CreateAlphaSliderPatcher(EditAvatarColorViewController colorEditor)
    {
        var editor = AddonAvatarColoringBridge.TryGetBeatAvatarEditorViewController();
        return editor != null
            ? AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, colorEditor, editor)
            : AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, colorEditor);
    }
}
