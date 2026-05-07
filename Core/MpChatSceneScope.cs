using UnityEngine;

namespace MultiplayerChat.Core;

internal static class MpChatSceneScope
{
    public static bool IsGameCoreHost(MonoBehaviour? host) =>
        host != null && host.gameObject != null &&
        string.Equals(host.gameObject.scene.name, "GameCore", System.StringComparison.Ordinal);
}
