using UnityEngine;

namespace MultiplayerChat.Core;

/// <summary>Distinguishes Zenject contexts: lobby/menu hosts live in non–GameCore scenes; song/arena uses GameCore.</summary>
internal static class MpChatSceneScope
{
    public static bool IsGameCoreHost(MonoBehaviour? host) =>
        host != null && host.gameObject != null &&
        string.Equals(host.gameObject.scene.name, "GameCore", System.StringComparison.Ordinal);
}
