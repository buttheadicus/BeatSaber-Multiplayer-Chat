using System;

namespace MultiplayerChat.Core;

public static class ChatErrorReporter
{
    public static void Report(Exception ex, string context)
    {
        Report(ex, context, ex?.Message ?? "");
    }

    public static void Report(Exception? ex, string context, string detail)
    {
        try
        {
            MultiplayerChat.Plugin.Log?.Error($"[MPChat][ERROR] context={context}");
            if (!string.IsNullOrEmpty(detail))
                MultiplayerChat.Plugin.Log?.Error($"[MPChat][ERROR] detail={detail}");
            if (ex != null)
            {
                MultiplayerChat.Plugin.Log?.Error($"[MPChat][ERROR] type={ex.GetType().FullName} message={ex.Message}");
                MultiplayerChat.Plugin.Log?.Error(ex.StackTrace ?? "");
                if (ex.InnerException != null)
                    MultiplayerChat.Plugin.Log?.Error($"[MPChat][ERROR] inner={ex.InnerException}");
            }

            ChatSoundEffects.PlayError();
            ChatManager.Instance?.PostSystemMessageRich(
                "<color=#FF4444>An error occured, please file a bug report and provide your logs (if you can)!</color>");
        }
        catch (Exception logEx)
        {
            MultiplayerChat.Plugin.Log?.Error($"[MPChat][ERROR] ChatErrorReporter failed: {logEx}");
        }
    }
}
