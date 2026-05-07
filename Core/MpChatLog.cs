using System;
using System.Reflection;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Core;

// Maps ModSettings DebugLogging to BSIPA FilterLevel on the Multiplayer Chat logger so Debug() lines appear in _latest.log.
internal static class MpChatLog
{
    private static IPALogger? _logger;
    private static PropertyInfo? _filterLevelProperty;
    private static object? _filterDebug;
    private static object? _filterInfo;

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
            _filterInfo = Enum.Parse(enumType, "Info");
        }
        catch
        {
            _filterLevelProperty = null;
            _filterDebug = null;
            _filterInfo = null;
        }
    }

    internal static void Apply(bool debugEnabled)
    {
        if (_logger == null || _filterLevelProperty == null || _filterDebug == null || _filterInfo == null)
            return;
        try
        {
            _filterLevelProperty.SetValue(_logger, debugEnabled ? _filterDebug : _filterInfo);
            if (debugEnabled)
                _logger.Info("[MPChat] Debug logging is ON for this logger (FilterLevel Debug).");
            else
                _logger.Info("[MPChat] Debug logging is OFF (FilterLevel Info).");
        }
        catch
        {
            // Older BSIPA layout without FilterLevel; Info/Debug still work at compile time but IPA.yml may control filtering.
        }
    }
}
