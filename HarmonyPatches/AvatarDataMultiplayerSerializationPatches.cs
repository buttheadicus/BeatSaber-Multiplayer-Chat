using System;
using System.Reflection;
using BeatSaber.BeatAvatarSDK;
using HarmonyLib;

namespace MultiplayerChat.HarmonyPatches;

internal static class AvatarDataMultiplayerSerializationPatches
{
    internal static void Apply(Harmony harmony)
    {
        try
        {
            harmony.CreateClassProcessor(typeof(AvatarDataCreateMultiplayerAvatarsDataPrefix)).Patch();
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"[MPChat] AvatarData multiplayer serialization guard failed: {ex.Message}");
        }
    }

    internal static void EnsureStringsSafeForMpBinaryWriter(AvatarData? avatarData)
    {
        if (avatarData == null)
            return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = typeof(AvatarData);

        foreach (var field in t.GetFields(flags))
        {
            if (field.FieldType != typeof(string))
                continue;
            if (field.GetValue(avatarData) != null)
                continue;
            field.SetValue(avatarData, ReplacementForNullString(field.Name));
        }

        foreach (var prop in t.GetProperties(flags))
        {
            if (prop.PropertyType != typeof(string) || !prop.CanRead || !prop.CanWrite)
                continue;
            if (prop.GetIndexParameters().Length != 0)
                continue;
            if (prop.GetValue(avatarData) != null)
                continue;
            prop.SetValue(avatarData, ReplacementForNullString(prop.Name));
        }
    }

    private static string ReplacementForNullString(string memberName)
    {
        var logical = LogicalMemberName(memberName);
        return LooksLikeAccessoryOrMeshId(logical) ? "None" : "";
    }

    private static string LogicalMemberName(string reflectionName)
    {
        // C# auto-property backing field: <glassesId>k__BackingField
        if (reflectionName.StartsWith("<", StringComparison.Ordinal)
            && reflectionName.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            var end = reflectionName.IndexOf('>', 1);
            if (end > 1)
                return reflectionName.Substring(1, end - 1);
        }

        return reflectionName;
    }

    private static bool LooksLikeAccessoryOrMeshId(string logicalName) =>
        logicalName.EndsWith("Id", StringComparison.OrdinalIgnoreCase);

    [HarmonyPatch]
    private static class AvatarDataCreateMultiplayerAvatarsDataPrefix
    {
        private static MethodBase? TargetMethod()
        {
            var t = AccessTools.TypeByName("BeatSaber.BeatAvatarAdapter.AvatarDataMultiplayerAvatarsDataConverter");
            if (t == null)
                return null;

            foreach (var m in AccessTools.GetDeclaredMethods(t))
            {
                if (m.Name != "CreateMultiplayerAvatarsData")
                    continue;
                var ps = m.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(AvatarData))
                    continue;
                return m;
            }

            return null;
        }

        private static void Prefix(AvatarData avatarData) =>
            EnsureStringsSafeForMpBinaryWriter(avatarData);
    }
}
