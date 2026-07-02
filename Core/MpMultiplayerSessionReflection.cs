using System;
using System.Reflection;
using UnityEngine;

namespace MultiplayerChat.Core;

internal static class MpMultiplayerSessionReflection
{
    private static Type? _sessionManagerType;
    private static bool _reflectionReady;

    internal static bool IsSessionConnected()
    {
        try
        {
            EnsureReflection();
            var session = FindActiveSessionManager();
            return session != null && SessionLooksConnected(session);
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

        _sessionManagerType = typeof(CutScoreBuffer).Assembly.GetType("MultiplayerSessionManager", throwOnError: false);
        if (_sessionManagerType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name ?? "";
                if (!string.Equals(name, "Main", StringComparison.Ordinal) &&
                    !string.Equals(name, "BGNetCore", StringComparison.Ordinal))
                    continue;

                _sessionManagerType = asm.GetType("MultiplayerSessionManager", throwOnError: false);
                if (_sessionManagerType != null)
                    break;
            }
        }

        _reflectionReady = true;
    }

    private static object? FindActiveSessionManager()
    {
        if (_sessionManagerType == null)
            return null;

        object? best = null;
        var bestDepth = int.MaxValue;

        foreach (var obj in Resources.FindObjectsOfTypeAll(_sessionManagerType))
        {
            if (obj == null)
                continue;

            var mb = obj as MonoBehaviour;
            if (mb == null || !mb.isActiveAndEnabled)
                continue;

            var depth = mb.transform != null ? mb.transform.hierarchyCount : 0;
            if (depth >= bestDepth)
                continue;

            best = obj;
            bestDepth = depth;
        }

        return best;
    }

    private static bool SessionLooksConnected(object session)
    {
        var t = session.GetType();
        var prop = t.GetProperty("connected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(session, null) is bool connected)
            return connected;

        prop = t.GetProperty("isConnected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(session, null) is bool isConnected)
            return isConnected;

        return true;
    }
}
