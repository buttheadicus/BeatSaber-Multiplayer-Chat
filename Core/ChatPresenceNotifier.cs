using System;
using System.Collections.Generic;
using MultiplayerChat;
using Zenject;

namespace MultiplayerChat.Core;

// SLZ companion detection only. Mod users are shown via nametag icons (ModPresenceManager + ChatBubbleAnchor).
public class ChatPresenceNotifier : IInitializable, IDisposable
{
    private static readonly HashSet<string> AnnouncedSlzUserIds = new();
    private static readonly object AnnouncedLock = new();

    private const string SlzCompanionPresenceLine =
        "Oh hey! You're in a server with an SLZ AI player! SLZ will have commands in the future; for now it will just do its normal thing (play maps).";

    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly ModPresenceManager _modPresence = null!;

    public void Initialize()
    {
        _modPresence.PlayerWithModAdded += OnPlayerWithModAdded;
    }

    public void Dispose()
    {
        _modPresence.PlayerWithModAdded -= OnPlayerWithModAdded;
    }

    private void OnPlayerWithModAdded(object? sender, PlayerWithModEventArgs e)
    {
        if (!e.IsSlzCompanionClient || SlzMode.IsEnabled)
            return;
        if (string.IsNullOrEmpty(e.UserId))
            return;

        lock (AnnouncedLock)
        {
            if (!AnnouncedSlzUserIds.Add(e.UserId))
                return;
        }

        MultiplayerChat.Plugin.Log?.Info("[MPChat] ChatPresenceNotifier: SLZ companion detected in lobby");
        _chatManager.PostSystemMessageRich(SlzCompanionPresenceLine);
    }
}
