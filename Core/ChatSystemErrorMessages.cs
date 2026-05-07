namespace MultiplayerChat.Core;

public static class ChatSystemErrorMessages
{
    private const string ErrRed = "#FF4444";
    private const string HintGray = "#CCCCCC";

    private const string BugReportHint =
        "(maybe send a bug report ;3) . if you send a bug report, please send logs.";

    private static string Primary(string capsMessage) => $"<color={ErrRed}>{capsMessage}</color>";

    private static string PrimaryWithHint(string capsMessage, string grayParenHint) =>
        $"<color={ErrRed}>{capsMessage}</color> <color={HintGray}>{grayParenHint}</color>";

    public static void PostNoMicrophoneFound(ChatManager? chat)
    {
        if (chat == null) return;
        ChatSoundEffects.PlayError();
        chat.PostSystemMessageRich(Primary("ERROR: NO MICROPHONE FOUND."));
    }

    public static void PostMicrophoneFailedToStart(ChatManager? chat)
    {
        if (chat == null) return;
        ChatSoundEffects.PlayError();
        chat.PostSystemMessageRich(PrimaryWithHint("ERROR: MICROPHONE FAILED TO START.", BugReportHint));
    }

    public static void PostHotMicDidNotResumeAfterVoipReload(ChatManager? chat)
    {
        if (chat == null) return;
        ChatSoundEffects.PlayError();
        chat.PostSystemMessageRich(
            PrimaryWithHint("ERROR: HOT MIC DID NOT RESTART AFTER VOIP RELOAD.", BugReportHint));
    }

    public static void PostVoiceEncodeFailed(ChatManager? chat)
    {
        if (chat == null) return;
        ChatSoundEffects.PlayError();
        chat.PostSystemMessageRich(
            PrimaryWithHint("ERROR: VOICE MESSAGE FAILED TO ENCODE.", BugReportHint));
    }

    public static void PostNothingRecordedToSend(ChatManager? chat)
    {
        if (chat == null) return;
        ChatSoundEffects.PlayError();
        chat.PostSystemMessageRich(Primary("ERROR: NOTHING RECORDED TO SEND."));
    }
}
