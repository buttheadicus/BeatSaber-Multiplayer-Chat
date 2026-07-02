using System;
using System.Collections.Generic;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;

namespace MultiplayerChat.Core.Addons;

internal static class AddonPacketSerializerBridge
{
    private static readonly Dictionary<Type, Delegate> Active = new();

    internal static void Attach(MpPacketSerializer serializer)
    {
        Detach(serializer);
        AddonPacketBridge.RegistrationAdded += OnRegistrationAdded;
        AddonPacketBridge.RegistrationRemoved += OnRegistrationRemoved;

        foreach (var entry in AddonPacketBridge.Snapshot())
            Register(serializer, entry.PacketType, entry.Handler);
    }

    internal static void Detach(MpPacketSerializer serializer)
    {
        AddonPacketBridge.RegistrationAdded -= OnRegistrationAdded;
        AddonPacketBridge.RegistrationRemoved -= OnRegistrationRemoved;

        foreach (var type in new List<Type>(Active.Keys))
            Unregister(serializer, type);
        Active.Clear();
    }

    private static void OnRegistrationAdded(Type packetType, Delegate handler) =>
        Register(ChatManager.Instance?.PacketSerializer, packetType, handler);

    private static void OnRegistrationRemoved(Type packetType, Delegate handler) =>
        Unregister(ChatManager.Instance?.PacketSerializer, packetType);

    private static void Register(MpPacketSerializer? serializer, Type packetType, Delegate handler)
    {
        if (serializer == null)
            return;

        try
        {
            var method = typeof(AddonPacketSerializerBridge).GetMethod(
                nameof(RegisterGeneric),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var generic = method!.MakeGenericMethod(packetType);
            generic.Invoke(null, new object[] { serializer, handler });
            Active[packetType] = handler;
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Packet register failed for {packetType.Name}: {ex.Message}");
        }
    }

    private static void RegisterGeneric<TPacket>(MpPacketSerializer serializer, Delegate handler)
        where TPacket : MultiplayerCore.Networking.Abstractions.MpPacket, new()
    {
        if (handler is not Action<TPacket, object> typed)
            return;

        serializer.RegisterCallback<TPacket>((packet, sender) => typed(packet, sender!));
    }

    private static void Unregister(MpPacketSerializer? serializer, Type packetType)
    {
        if (serializer == null || !Active.ContainsKey(packetType))
            return;

        try
        {
            var method = typeof(MpPacketSerializer).GetMethod(nameof(MpPacketSerializer.UnregisterCallback));
            if (method == null)
                return;
            var generic = method.MakeGenericMethod(packetType);
            generic.Invoke(serializer, null);
            Active.Remove(packetType);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Packet unregister failed for {packetType.Name}: {ex.Message}");
        }
    }
}
