using UnityEngine;

namespace MultiplayerChat.Core;

internal static class MpChatSceneScope
{
    public static bool IsGameCoreHost(MonoBehaviour? host)
    {
        // Unity destroyed objects are not C# null; use the overloaded ==
        if (host == null)
            return false;
        try
        {
            var go = host.gameObject;
            if (go == null)
                return false;
            return string.Equals(go.scene.name, "GameCore", System.StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
