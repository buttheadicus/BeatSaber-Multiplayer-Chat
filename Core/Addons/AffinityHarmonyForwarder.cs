using System;
using System.Reflection;

namespace MultiplayerChat.Core.Addons;

// applies Harmony.PatchAll for addon assemblies. SiraUtil IAffinity patchers are bound via AddonZenjectSettingsBinder.
internal static class AffinityHarmonyForwarder
{
    internal static void PatchAssembly(HarmonyLib.Harmony harmony, Assembly assembly)
    {
        try
        {
            harmony.PatchAll(assembly);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Harmony.PatchAll failed for {assembly.GetName().Name}: {ex.Message}");
        }
    }

    internal static void UnpatchAssembly(HarmonyLib.Harmony harmony)
    {
        try
        {
            harmony.UnpatchSelf();
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Harmony.UnpatchSelf failed: {ex.Message}");
        }
    }
}
