using System.Reflection;
using BeatSaber.AvatarCore;
using HarmonyLib;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;

namespace MultiplayerChat.HarmonyPatches;

[HarmonyPatch]
internal static class MpChatArenaAvatarPoseConnectedPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(MultiplayerAvatarPoseController), "set_connectedPlayer")!;

    private static void Postfix(MultiplayerAvatarPoseController __instance)
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return;
        if (!MpChatFeatures.LobbyCustomAvatarsInArena)
            return;
        if (!string.Equals(__instance.gameObject.scene.name, "GameCore", System.StringComparison.Ordinal))
            return;

        MpChatArenaAvatarAttach.TryAttachToPose(__instance);
    }
}
