using System;

namespace MultiplayerChat.Core.Addons;

internal static class AddonAvatarColoringBridge
{
    private static Action? _ensureAvatarStorageExists;
    private static Func<object?>? _getBeatAvatarEditorViewController;

    internal static void SetHandlers(
        Action? ensureAvatarStorageExists,
        Func<object?>? getBeatAvatarEditorViewController = null)
    {
        _ensureAvatarStorageExists = ensureAvatarStorageExists;
        _getBeatAvatarEditorViewController = getBeatAvatarEditorViewController;
    }

    internal static void ClearHandlers()
    {
        _ensureAvatarStorageExists = null;
        _getBeatAvatarEditorViewController = null;
    }

    internal static void EnsureAvatarStorageExists() => _ensureAvatarStorageExists?.Invoke();

    internal static object? TryGetBeatAvatarEditorViewController() =>
        _getBeatAvatarEditorViewController?.Invoke();
}
