using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using MultiplayerChat.Core.Addons;
using SiraUtil.Affinity;

namespace MultiplayerChat.Core.AvatarColoring;

// SiraUtil affinity only applies to types in the core plugin assembly.
internal sealed class AvatarColoringEditorAffinityShim : IAffinity
{
    private const string AddonId = "avatarColoring";
    private const string PatcherTypeName = "MultiplayerChat.AvatarColoring.AvatarColoringEditorPatcher";

    [AffinityPatch(typeof(BeatAvatarEditorViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixBeatAvatarEditorDidActivate(
        BeatAvatarEditorViewController __instance,
        bool firstActivation,
        bool addedToHierarchy,
        bool screenSystemEnabling)
    {
        var patcher = AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, __instance);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixBeatAvatarEditorDidActivate",
            firstActivation,
            addedToHierarchy,
            screenSystemEnabling);
    }

    [AffinityPatch(typeof(BeatAvatarEditorViewController), "DidDeactivate")]
    [AffinityPostfix]
    public void PostfixBeatAvatarEditorDidDeactivate(
        BeatAvatarEditorViewController __instance,
        bool removedFromHierarchy,
        bool screenSystemDisabling)
    {
        var patcher = AddonAffinityShimBridge.CreatePatcherOrNull(AddonId, PatcherTypeName, __instance);
        AddonAffinityShimBridge.InvokeVoidOrNoop(
            patcher,
            "PostfixBeatAvatarEditorDidDeactivate",
            removedFromHierarchy,
            screenSystemDisabling);
    }
}
