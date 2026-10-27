namespace MultiplayerChat.Core;

// basucally makes screwy charecters not screwy.
internal static class ChatRichTextEscape
{
    private const char LessThanDisplay = '\uFF1C';
    private const char GreaterThanDisplay = '\uFF1E';

    internal static string ForDisplay(string? text)
    {
        if (text is not { Length: > 0 })
            return "";

        if (text.IndexOf('<') < 0 && text.IndexOf('>') < 0)
            return text;

        return text.Replace('<', LessThanDisplay).Replace('>', GreaterThanDisplay);
    }
}
