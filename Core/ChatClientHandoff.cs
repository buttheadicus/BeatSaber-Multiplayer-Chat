namespace MultiplayerChat.Core;

// exclusive chat-client ownership; when claimed, human UI/input is suppressed
public static class ChatClientHandoff
{
    private static readonly object Gate = new();
    private static string? _ownerAddonId;

    public static bool IsTakenOver
    {
        get
        {
            lock (Gate)
                return _ownerAddonId != null;
        }
    }

    public static bool IsHumanClientSuppressed => IsTakenOver;

    public static string? OwnerAddonId
    {
        get
        {
            lock (Gate)
                return _ownerAddonId;
        }
    }

    public static bool TryClaim(string addonId)
    {
        if (string.IsNullOrWhiteSpace(addonId))
            return false;

        lock (Gate)
        {
            if (_ownerAddonId != null &&
                !string.Equals(_ownerAddonId, addonId, System.StringComparison.Ordinal))
                return false;

            _ownerAddonId = addonId;
            return true;
        }
    }

    public static void Release(string addonId)
    {
        if (string.IsNullOrWhiteSpace(addonId))
            return;

        lock (Gate)
        {
            if (_ownerAddonId == null)
                return;
            if (!string.Equals(_ownerAddonId, addonId, System.StringComparison.Ordinal))
                return;
            _ownerAddonId = null;
        }
    }

    public static void ReleaseAll()
    {
        lock (Gate)
            _ownerAddonId = null;
    }

    public static bool IsOwner(string addonId)
    {
        if (string.IsNullOrWhiteSpace(addonId))
            return false;
        lock (Gate)
            return string.Equals(_ownerAddonId, addonId, System.StringComparison.Ordinal);
    }
}
