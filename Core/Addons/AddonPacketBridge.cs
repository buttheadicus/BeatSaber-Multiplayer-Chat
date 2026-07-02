using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonPacketRegistration : IDisposable
{
    private readonly Action _dispose;

    internal AddonPacketRegistration(Action dispose) => _dispose = dispose;

    public void Dispose() => _dispose();
}

internal static class AddonPacketBridge
{
    private static readonly List<(Type PacketType, Delegate Handler)> Registrations = new();

    internal static event Action<Type, Delegate>? RegistrationAdded;

    internal static event Action<Type, Delegate>? RegistrationRemoved;

    internal static IReadOnlyList<(Type PacketType, Delegate Handler)> Snapshot() => Registrations;

    internal static AddonPacketRegistration Register<TPacket>(Action<TPacket, object> handler) where TPacket : class
    {
        var del = (Delegate)handler;
        Registrations.Add((typeof(TPacket), del));
        RegistrationAdded?.Invoke(typeof(TPacket), del);
        return new AddonPacketRegistration(() =>
        {
            Registrations.Remove((typeof(TPacket), del));
            RegistrationRemoved?.Invoke(typeof(TPacket), del);
        });
    }

    internal static void Unregister<TPacket>() where TPacket : class
    {
        var packetType = typeof(TPacket);
        foreach (var entry in Registrations.Where(e => e.PacketType == packetType).ToArray())
            RegistrationRemoved?.Invoke(entry.PacketType, entry.Handler);
        Registrations.RemoveAll(e => e.PacketType == packetType);
    }

    internal static void Clear()
    {
        foreach (var entry in Registrations.ToArray())
            RegistrationRemoved?.Invoke(entry.PacketType, entry.Handler);
        Registrations.Clear();
    }
}
