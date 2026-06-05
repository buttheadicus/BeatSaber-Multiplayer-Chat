using HarmonyLib;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;

namespace MultiplayerChat.HarmonyPatches;

[HarmonyPatch(typeof(MultiplayerConnectedPlayerFacade), nameof(MultiplayerConnectedPlayerFacade.HideBigAvatar))]
internal static class MpChatArenaFacadeHideBigAvatarPatch
{
    private static void Postfix(MultiplayerConnectedPlayerFacade __instance)
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return;
        if (!MpChatFeatures.LobbyCustomAvatarsInArena)
            return;

        MpChatArenaAvatarAttach.RefreshAttachForGameplay(__instance);

        foreach (var driver in __instance.GetComponentsInChildren<MpChatLobbyCustomAvatarDriver>(true))
            driver.PromoteArenaAfterIntro();
    }
}
