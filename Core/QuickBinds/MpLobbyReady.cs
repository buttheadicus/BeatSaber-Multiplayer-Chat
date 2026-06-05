using System;

namespace MultiplayerChat.Core.QuickBinds;

internal static class MpLobbyReady
{
    private static readonly string[] ReadyMethodNames =
    {
        "HandleLobbySetupViewControllerStartGameOrReady"
    };

    private static Type? _lobbyFcType;
    private static bool _reflectionReady;

    internal static bool TryReadyUp()
    {
        try
        {
            EnsureReflection();
            if (_lobbyFcType == null)
                return false;

            var fc = MpUiReflection.FindBestActiveObject(_lobbyFcType);
            if (fc == null)
                return false;

            return TryInvokeNamedMethods(fc, _lobbyFcType, ReadyMethodNames);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureReflection()
    {
        if (_reflectionReady)
            return;

        _lobbyFcType = MpUiReflection.ResolveType("GameServerLobbyFlowCoordinator");
        _reflectionReady = true;
    }

    private static bool TryInvokeNamedMethods(object target, Type type, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            var method = MpUiReflection.GetParameterlessInstanceMethod(type, names[i]);
            if (method == null)
                continue;

            try
            {
                method.Invoke(target, null);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }
}
