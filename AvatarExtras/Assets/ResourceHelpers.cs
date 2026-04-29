using System.Reflection;

namespace MultiplayerChat.AvatarExtras.Assets;

public static class ResourceHelpers
{
    public static byte[]? GetResource(Assembly asm, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName);

        if (stream is null)
            return null;

        var data = new byte[stream.Length];
        stream.Read(data, 0, (int)stream.Length);
        return data;
    }
}
