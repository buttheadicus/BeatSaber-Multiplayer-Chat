namespace MultiplayerChat.Core;

/// <summary>
/// Beat Saber platform <c>userId</c> → other players' persistent 8-digit chat IDs.
/// Backed by <see cref="LearnedChatIdsStore"/> (DPAPI-encrypted <c>LearnedIDs.dat</c> under the Beat Saber AppData folder).
/// </summary>
public class ChatPlayerIdRegistry
{
    private readonly LearnedChatIdsStore _store;

    public ChatPlayerIdRegistry(LearnedChatIdsStore store)
    {
        _store = store;
    }

    public void SetMapping(string platformUserId, string chatId) => _store.SetMapping(platformUserId, chatId);

    public bool TryGetChatId(string platformUserId, out string chatId) => _store.TryGetChatId(platformUserId, out chatId);
}
