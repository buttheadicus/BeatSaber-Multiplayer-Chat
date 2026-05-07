namespace MultiplayerChat.Core;

// Thin facade over LearnedChatIdsStore for Zenject injection into ChatManager and validators.
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
