using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using MultiplayerChat.Core.Addons;
using SiraUtil.Affinity;
using UnityEngine;

namespace MultiplayerChat.Core.AvatarColoring;

internal sealed class AvatarColoringAlphaSliderAffinityShim : IAffinity
{
    private const string AddonId = "avatarColoring";
    private const string PatcherTypeName = "MultiplayerChat.AvatarColoring.AvatarColoringAlphaSliderPatcher";

    [AffinityPatch(typeof(EditAvatarColorViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidActivate(
        EditAvatarColorViewController __instance,
        bool firstActivation,
        bool addedToHierarchy,
        bool screenSystemEnabling)
    {
        var patcher = CreateAlphaSliderPatcher(__instance);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixEditColorDidActivate",
            firstActivation,
            addedToHierarchy,
            screenSystemEnabling);
    }

    [AffinityPatch(typeof(EditAvatarColorViewController), "DidDeactivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidDeactivate(
        EditAvatarColorViewController __instance,
        bool removedFromHierarchy,
        bool screenSystemDisabling)
    {
        var patcher = CreateAlphaSliderPatcher(__instance);
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
