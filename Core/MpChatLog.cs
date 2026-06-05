using System;
using System.Reflection;
using IPALogger = IPA.Logging.Logger;

using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

// Debug off: BSIPA FilterLevel Error (errors only). Debug on: FilterLevel Debug (info, warn, debug, error).
internal static class MpChatLog
{
    private static IPALogger? _logger;
    private static PropertyInfo? _filterLevelProperty;
    private static object? _filterDebug;
    private static object? _filterError;

    internal static bool IsVerbose => ModSettings.DebugLogging;

    internal static void Init(IPALogger logger)
    {
        _logger = logger;
        try
        {
            var t = logger.GetType();
            _filterLevelProperty = t.GetProperty("FilterLevel", BindingFlags.Instance | BindingFlags.Public)
                                   ?? t.GetProperty("FilterLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_filterLevelProperty == null)
                return;
            var enumType = _filterLevelProperty.PropertyType;
            if (!enumType.IsEnum)
                return;
            _filterDebug = Enum.Parse(enumType, "Debug");
            _filterError = Enum.Parse(enumType, "Error");
        }
        catch
        {
            _filterLevelProperty = null;
            _filterDebug = null;
            _filterError = null;
        }
    }

    internal static void Apply(bool debugEnabled)
    {
        VoiceDynamicTransmitGate.NotifyPushToTalkHeld(false);

        if (_logger == null || _filterLevelProperty == null || _filterDebug == null || _filterError == null)
            return;

        try
        {
            _filterLevelProperty.SetValue(_logger, debugEnabled ? _filterDebug : _filterError);
            if (debugEnabled)
            {
                _logger.Debug("[MPChat] Verbose logging ON (FilterLevel Debug).");
                MpChatLobbyDiagnostics.LogVoipTransition("DebugLogging:enabled", "verbose diagnostics armed");
            }
        }
        catch
        {
        }
    }

    internal static void Error(string message) => _logger?.Error(message);

    internal static void Info(string message)
    {
        if (!IsVerbose)
            return;
        _logger?.Info(message);
    }

    internal static void Warn(string message)
    {
        if (!IsVerbose)
            return;
        _logger?.Warn(message);
    }

    internal static void DebugLine(string message)
    {
        if (!IsVerbose)
            return;
        _logger?.Debug(message);
    }
}
