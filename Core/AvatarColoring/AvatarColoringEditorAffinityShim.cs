using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using HMUI;
using MultiplayerChat.Core.Addons;
using SiraUtil.Affinity;

namespace MultiplayerChat.Core.AvatarColoring;

// SiraUtil affinity only applies to types in the core plugin assembly.
internal sealed class AvatarColoringEditorAffinityShim : IAffinity
{
    private const string AddonId = "avatarColoring";
    private const string PatcherTypeName = "MultiplayerChat.AvatarColoring.AvatarColoringEditorPatcher";

    [AffinityPatch(typeof(ViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixBeatAvatarEditorDidActivate(
        ViewController __instance,
        bool firstActivation,
        bool addedToHierarchy,
        bool screenSystemEnabling)
    {
        if (__instance is not BeatAvatarEditorViewController editor)
            return;

        var patcher = AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, editor);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixBeatAvatarEditorDidActivate",
            firstActivation,
            addedToHierarchy,
            screenSystemEnabling);
    }

    [AffinityPatch(typeof(ViewController), "DidDeactivate")]
    [AffinityPostfix]
    public void PostfixBeatAvatarEditorDidDeactivate(
        ViewController __instance,
        bool removedFromHierarchy,
        bool screenSystemDisabling)
    {
        if (__instance is not BeatAvatarEditorViewController editor)
            return;

        var patcher = AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, editor);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixBeatAvatarEditorDidDeactivate",
            removedFromHierarchy,
            screenSystemDisabling);
    }
}
