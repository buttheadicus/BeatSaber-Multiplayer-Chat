using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Posts "USERNAME has chat! They can see your messages!" when players with the mod join.
/// Batches names when we join a lobby; single message when someone joins us.
/// </summary>
public class ChatPresenceNotifier : IInitializable, IDisposable
{
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly ModPresenceManager _modPresence = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;

    private readonly List<string> _pendingNames = new();
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
        if (string.IsNullOrEmpty(e.UserName)) return;
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] ChatPresenceNotifier: adding {e.UserName} to batch");
        _pendingNames.Add(e.UserName);
        ScheduleBatch();
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

    private void FlushBatch()
    {
        if (_pendingNames.Count == 0) return;

        var names = _pendingNames.ToList();
        _pendingNames.Clear();

        var msg = names.Count == 1
            ? $"{names[0]} has chat! They can see your messages!"
            : $"{string.Join(", ", names)} has chat! They can see your messages!";

        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] ChatPresenceNotifier: posting '{msg}'");
        _chatManager.PostSystemMessage(msg);
    }
}
