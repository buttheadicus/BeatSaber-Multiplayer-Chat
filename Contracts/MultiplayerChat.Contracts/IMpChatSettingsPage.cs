namespace MultiplayerChat.Contracts;

public interface IMpChatSettingsPage
{
    string AddonId { get; }

    string PageTitle { get; }

    string SettingsCategory { get; }
}
