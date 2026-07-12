using System;
using Zenject;

namespace MultiplayerChat.Core;

// kept for Zenject wiring. SLZ companion lobby announcement was removed.
public class ChatPresenceNotifier : IInitializable, IDisposable
{
    public void Initialize()
    {
    }

    public void Dispose()
    {
    }
}
