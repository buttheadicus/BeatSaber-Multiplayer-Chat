using System;

namespace MultiplayerChat.Core.Addons;

internal static class AddonMenuResolveBridge
{
    internal static Func<string, string, Type, object?>? Resolve { get; private set; }

    internal static void Register(Func<string, string, Type, object?> resolve) => Resolve = resolve;

    internal static T? TryResolveMenuSingleton<T>(string addonId, string typeFullName) where T : class =>
        Resolve?.Invoke(addonId, typeFullName, typeof(T)) as T;
}
