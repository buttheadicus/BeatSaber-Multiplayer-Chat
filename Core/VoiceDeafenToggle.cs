using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

internal static class VoiceDeafenToggle
{
    internal static void ToggleFromUserInput()
    {
        VoiceChatRuntimeState.SetDeaf(!VoiceChatRuntimeState.IsDeaf);
        ModSettings.SaveVoiceSelfStateFromRuntime();
        var chat = ChatManager.Instance;
        if (chat == null)
            return;

        chat.SendVoiceDeafenStateNotify(VoiceChatRuntimeState.IsDeaf);
        chat.SendVoiceHotMicMuteStateNotify(VoiceChatRuntimeState.IsHotMicMuted);
        if (VoiceChatRuntimeState.IsDeaf)
            chat.PostSystemMessageRich(
                "<color=#CCCCCC>You have went deaf, you cannot hear anybody, not even voice messages.</color>");
        else
            chat.PostSystemMessageRich("<color=#CCCCCC>You have went undeaf, you can hear all voices.</color>");
    }
}
