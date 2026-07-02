namespace MultiplayerChat.Contracts;

public interface IMpChatLobbyAvatarHook
{
    string AddonId { get; }

    void DecorateLobbyAvatar(object lobbyAvatarController);

    void DecorateLobbyAvatarPlace(object lobbyAvatarPlace);
}
