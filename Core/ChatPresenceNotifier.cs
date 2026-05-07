using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

public class ChatPresenceNotifier : IInitializable, IDisposable
{
    private static readonly HashSet<string> _alreadyAnnouncedUserIds = new();
    private static readonly object _announcedLock = new();

    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly ModPresenceManager _modPresence = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;

    private readonly List<(string Name, string? NameColorHex, bool IsSlzCompanion)> _pendingEntries = new();
    private Coroutine? _batchCoroutine;

    public void Initialize()
    {
        _modPresence.PlayerWithModAdded += OnPlayerWithModAdded;
    }

    public void Dispose()
    {
        _modPresence.PlayerWithModAdded -= OnPlayerWithModAdded;
        if (_batchCoroutine != null && _coroutineHost != null)
            _coroutineHost.StopCoroutine(_batchCoroutine);
    }

    private void OnPlayerWithModAdded(object? sender, PlayerWithModEventArgs e)
    {
        if (string.IsNullOrEmpty(e.UserName) || string.IsNullOrEmpty(e.UserId)) return;
        lock (_announcedLock)
        {
            if (_alreadyAnnouncedUserIds.Contains(e.UserId)) return;
            _alreadyAnnouncedUserIds.Add(e.UserId);
        }
        var trimmed = TrimName(e.UserName, 30);
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] ChatPresenceNotifier: adding {trimmed} to batch");
        _pendingEntries.Add((trimmed, e.NameColorHex, e.IsSlzCompanionClient));
        ScheduleBatch();
    }

    private static string TrimName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen) + "...";
    }

    private void ScheduleBatch()
    {
        if (_batchCoroutine != null)
            _coroutineHost.StopCoroutine(_batchCoroutine);
        _batchCoroutine = _coroutineHost.StartCoroutine(BatchAfterDelay());
    }

    private IEnumerator BatchAfterDelay()
    {
        yield return new WaitForSeconds(0.8f);
        _batchCoroutine = null;
        FlushBatch();
    }

    private const string SlzCompanionPresenceLine =
        "Oh hey! You're in a server with an SLZ AI player! SLZ will have commands in the future; for now it will just do its normal thing (play maps).";

    private void FlushBatch()
    {
        if (_pendingEntries.Count == 0) return;

        var entries = _pendingEntries.ToList();
        _pendingEntries.Clear();

        var anySlzForNonSlzViewer = entries.Any(e => e.IsSlzCompanion) && !SlzMode.IsEnabled;
        if (anySlzForNonSlzViewer)
        {
            foreach (var e in entries)
            {
                var one = $"{RichPresenceName(e.Name, e.NameColorHex)} has chat! They can see your messages!";
                MultiplayerChat.Plugin.Log?.Info($"[MPChat] ChatPresenceNotifier: posting presence line (split batch, SLZ mix)");
                _chatManager.PostSystemMessageRich(one);
                if (e.IsSlzCompanion)
                    _chatManager.PostSystemMessageRich(SlzCompanionPresenceLine);
            }

            return;
        }

        var coloredNames = entries.Count == 1
            ? RichPresenceName(entries[0].Name, entries[0].NameColorHex)
            : string.Join(", ", entries.Select(e => RichPresenceName(e.Name, e.NameColorHex)));

        var msg = $"{coloredNames} has chat! They can see your messages!";

        MultiplayerChat.Plugin.Log?.Info($"[MPChat] ChatPresenceNotifier: posting presence line ({entries.Count} name(s))");
        _chatManager.PostSystemMessageRich(msg);
    }

    private static string RichPresenceName(string displayName, string? hex6)
    {
        var raw = hex6?.Trim();
        var h = string.IsNullOrEmpty(raw) ? "87CEEB" : raw!;
        if (h.StartsWith("#")) h = h.Substring(1);
        if (h.Length > 6) h = h.Substring(0, 6);
        if (h.Length != 6) h = "87CEEB";
        var safe = (displayName ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
        return $"<color=#{h}>{safe}</color>";
    }
}
