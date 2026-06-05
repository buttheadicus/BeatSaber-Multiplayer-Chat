using System.Reflection;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

// Match Custom Avatars "Measure player height": HMD / BS player height -> eye height in CA settings -> resize -> lobby sync.
internal static class MpCustomAvatarHeightCalibration
{
    private const float MinEyeHeightMeters = 0.8f;

    private const float MaxEyeHeightMeters = 2.6f;

    private static bool _reflectionReady;

    private static System.Type? _playerAvatarManagerType;

    private static System.Type? _trackingRigType;

    private static System.Type? _calibrationModeType;

    private static System.Type? _generalSettingsHostType;

    private static System.Type? _armSpanMeasurerType;

    private static System.Type? _settingsViewControllerType;

    private static PropertyInfo? _generalSettingsHostProperty;

    private static PropertyInfo? _trackingRigEyeHeightProperty;

    private static MethodInfo? _measureHeightMethod;

    private static MethodInfo? _onPlayerEyeHeightChangedHostMethod;

    private static MethodInfo? _trackingRigBeginCalibrationMethod;

    private static MethodInfo? _trackingRigEndCalibrationMethod;

    private static MethodInfo? _onPlayerHeightChangedMethod;

    private static MethodInfo? _resizeCurrentAvatarMethod;

    private static MethodInfo? _calculateAvatarScaleMethod;

    private static PropertyInfo? _managerScaleProperty;

    private static FieldInfo? _pamSettingsField;

    private static PropertyInfo? _settingsPlayerEyeHeightProperty;

    private static PropertyInfo? _observableValueProperty;

    private static MethodInfo? _beatSaberAutoSetHeightMethod;

    private static PropertyInfo? _beatSaberPlayerHeightValueProperty;

    private static PropertyInfo? _scaledEyeHeightProperty;

    private static PropertyInfo? _generalSettingsHostHeightProperty;

    private static PropertyInfo? _isMeasureButtonEnabledProperty;

    private static PropertyInfo? _currentlySpawnedAvatarProperty;

    private static object? _cachedMeasureHost;

    private static float _lastAppliedSavedEyeHeight = -1f;

    public static void Run() => MpCustomAvatarSyncManager.RunHeightCalibration();

    // Reapply eye height from settings (no measure). Call on lobby join before avatar sync.
    public static bool ApplySavedPresetIfAny(bool refreshLobbyAvatar = false)
    {
        if (!ModSettings.TryGetLobbyCustomAvatarSavedEyeHeight(out var eyeHeight))
            return false;

        if (!EnsureReflection())
            return false;

        var manager = ResolvePlayerAvatarManager();
        if (manager == null)
            return false;

        if (Mathf.Abs(_lastAppliedSavedEyeHeight - eyeHeight) < 0.001f)
        {
            if (refreshLobbyAvatar)
                RefreshLocalLobbyAvatar();
            return true;
        }

        try
        {
            ApplyEyeHeightToCustomAvatars(manager, eyeHeight);
            _lastAppliedSavedEyeHeight = eyeHeight;
            MultiplayerChat.Plugin.Log?.Debug(
                $"[MPChat][CustomAvatars] Applied saved lobby eye height {eyeHeight:F2} m.");
        }
        catch (System.Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn(
                $"[MPChat][CustomAvatars] Saved eye height apply failed: {ex.Message}");
            return false;
        }

        if (refreshLobbyAvatar)
            RefreshLocalLobbyAvatar();

        return true;
    }

    public static bool TryRunCalibration()
    {
        if (!EnsureReflection())
        {
            MultiplayerChat.Plugin.Log?.Warn(
                "[MPChat][CustomAvatars] Calibrate Height: Custom Avatars is not installed.");
            return false;
        }

        var manager = ResolvePlayerAvatarManager();
        if (manager == null)
        {
            MultiplayerChat.Plugin.Log?.Warn(
                "[MPChat][CustomAvatars] Calibrate Height: Custom Avatars player rig is not active in this scene.");
            return false;
        }

        var rig = ResolveTrackingRig(manager);

        if (TryMeasureViaBootstrappedHost(manager, rig, out var hostEye, out var hostSource))
            return TryApplyMeasuredHeight(manager, hostEye, hostSource);

        if (rig != null && TryMeasureViaTrackingRig(rig, out var rigEye, out var rigSource))
            return TryApplyMeasuredHeight(manager, rigEye, rigSource);

        if (!TryMeasureEyeHeightMeters(manager, out var eyeHeight, out var source))
        {
            MultiplayerChat.Plugin.Log?.Warn(
                "[MPChat][CustomAvatars] Calibrate Height: could not read eye height. In VR, stand normally and try again. In FPFC, set height in Beat Saber Settings > Player Height (Auto), or test in VR.");
            return false;
        }

        return TryApplyMeasuredHeight(manager, eyeHeight, source);
    }

    internal static void RefreshLocalLobbyAvatar()
    {
        MpCustomAvatarSyncManager.InvalidateOutboundDedupe();
        MpCustomAvatarSyncManager.BroadcastScaleThenMetadata();
        MpChatLobbyCustomAvatarDriver.NotifyLocalAvatarSettingsChanged();
    }

    private static bool TryApplyMeasuredHeight(object manager, float eyeHeight, string source)
    {
        try
        {
            ApplyEyeHeightToCustomAvatars(manager, eyeHeight);
            ModSettings.SetLobbyCustomAvatarSavedEyeHeight(eyeHeight);
            _lastAppliedSavedEyeHeight = eyeHeight;
            MultiplayerChat.Plugin.Log?.Info(
                $"[MPChat][CustomAvatars] Calibrate Height: {eyeHeight:F2} m from {source} (saved for later lobbies).");
            return true;
        }
        catch (System.Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn(
                $"[MPChat][CustomAvatars] Calibrate Height failed: {ex.Message}");
            return false;
        }
    }

    private static object? ResolveTrackingRig(object manager)
    {
        if (_trackingRigType != null)
        {
            var rig = FindUnityObject(_trackingRigType);
            if (rig != null)
                return rig;
        }

        return null;
    }

    private static bool TryMeasureViaTrackingRig(object rig, out float eyeHeightMeters, out string source)
    {
        eyeHeightMeters = 0f;
        source = "";

        if (_trackingRigBeginCalibrationMethod == null || _calibrationModeType == null)
            return false;

        try
        {
            var automatic = System.Enum.Parse(_calibrationModeType, "Automatic");
            _trackingRigBeginCalibrationMethod.Invoke(rig, new[] { automatic });
            _trackingRigEndCalibrationMethod?.Invoke(rig, null);
        }
        catch (System.Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Debug(
                $"[MPChat][CustomAvatars] TrackingRig calibration skipped: {ex.Message}");
            return false;
        }

        if (_trackingRigEyeHeightProperty?.GetValue(rig, null) is float rigEye && IsValidEyeHeight(rigEye))
        {
            eyeHeightMeters = rigEye;
            source = "Custom Avatars TrackingRig (automatic calibration)";
            return true;
        }

        return false;
    }

    private static bool TryMeasureViaBootstrappedHost(object manager, object? rig, out float eyeHeightMeters, out string source)
    {
        eyeHeightMeters = 0f;
        source = "";

        if (_measureHeightMethod == null)
            return false;

        var host = ResolveGeneralSettingsHost(manager, rig);
        if (host == null)
            return false;

        try
        {
            _measureHeightMethod.Invoke(host, null);
        }
        catch (System.Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Debug(
                $"[MPChat][CustomAvatars] OnMeasureHeightButtonClicked failed: {ex.Message}");
            return false;
        }

        if (_generalSettingsHostHeightProperty?.GetValue(host, null) is float hostHeight && IsValidEyeHeight(hostHeight))
        {
            eyeHeightMeters = hostHeight;
            source = "Custom Avatars GeneralSettingsHost";
            return true;
        }

        return TryReadMeasuredEyeHeight(manager, out eyeHeightMeters, out source);
    }

    private static object? ResolveGeneralSettingsHost(object manager, object? rig)
    {
        if (_cachedMeasureHost != null)
            return _cachedMeasureHost;

        if (_settingsViewControllerType != null &&
            _generalSettingsHostProperty != null &&
            _measureHeightMethod != null)
        {
            var settingsView = FindUnityObject(_settingsViewControllerType, includeInactive: true);
            if (settingsView != null)
            {
                var existing = _generalSettingsHostProperty.GetValue(settingsView, null);
                if (existing != null)
                {
                    _cachedMeasureHost = existing;
                    return existing;
                }
            }
        }

        if (_generalSettingsHostType == null ||
            _pamSettingsField == null ||
            _armSpanMeasurerType == null ||
            _trackingRigType == null ||
            _measureHeightMethod == null)
            return null;

        rig ??= ResolveTrackingRig(manager);
        if (rig == null)
            return null;

        var settings = _pamSettingsField.GetValue(manager);
        if (settings == null)
            return null;

        object? host;
        try
        {
            var armSpanMeasurer = System.Activator.CreateInstance(_armSpanMeasurerType);
            host = System.Activator.CreateInstance(_generalSettingsHostType, settings, rig, armSpanMeasurer);
        }
        catch (System.Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Debug(
                $"[MPChat][CustomAvatars] Could not bootstrap GeneralSettingsHost: {ex.Message}");
            return null;
        }

        if (host == null)
            return null;

        TryAssignHostToSettingsView(host);
        _cachedMeasureHost = host;
        return host;
    }

    private static void TryAssignHostToSettingsView(object host)
    {
        if (_settingsViewControllerType == null || _generalSettingsHostProperty == null)
            return;

        var settingsView = FindUnityObject(_settingsViewControllerType, includeInactive: true);
        if (settingsView == null)
            return;

        try
        {
            if (_generalSettingsHostProperty.GetValue(settingsView, null) == null)
                _generalSettingsHostProperty.SetValue(settingsView, host, null);
        }
        catch
        {
            /* optional */
        }
    }

    private static bool TryReadMeasuredEyeHeight(object manager, out float eyeHeightMeters, out string source)
    {
        eyeHeightMeters = 0f;
        source = "";

        if (_trackingRigType != null && _trackingRigEyeHeightProperty != null)
        {
            var rig = FindUnityObject(_trackingRigType);
            if (rig != null &&
                _trackingRigEyeHeightProperty.GetValue(rig, null) is float rigEye &&
                IsValidEyeHeight(rigEye))
            {
                eyeHeightMeters = rigEye;
                source = "Custom Avatars measure (TrackingRig)";
                return true;
            }
        }

        if (_pamSettingsField != null && _settingsPlayerEyeHeightProperty != null && _observableValueProperty != null)
        {
            var settings = _pamSettingsField.GetValue(manager);
            if (settings != null)
            {
                var observable = _settingsPlayerEyeHeightProperty.GetValue(settings, null);
                if (observable != null &&
                    _observableValueProperty.GetValue(observable, null) is float settingsEye &&
                    IsValidEyeHeight(settingsEye))
                {
                    eyeHeightMeters = settingsEye;
                    source = "Custom Avatars measure (settings)";
                    return true;
                }
            }
        }

        return false;
    }

    private static void ApplyEyeHeightToCustomAvatars(object manager, float eyeHeight)
    {
        TrySetPlayerEyeHeightInPamSettings(manager, eyeHeight);
        TryInvokeOnPlayerEyeHeightChanged(eyeHeight);

        _onPlayerHeightChangedMethod?.Invoke(manager, new object[] { eyeHeight });

        if (_calculateAvatarScaleMethod != null)
        {
            var scaleObj = _calculateAvatarScaleMethod.Invoke(manager, new object[] { eyeHeight });
            if (scaleObj is float scale && _managerScaleProperty != null)
                _managerScaleProperty.SetValue(manager, scale, null);
        }

        _resizeCurrentAvatarMethod?.Invoke(manager, null);
    }

    private static void TrySetPlayerEyeHeightInPamSettings(object manager, float eyeHeight)
    {
        if (_pamSettingsField == null || _settingsPlayerEyeHeightProperty == null || _observableValueProperty == null)
            return;

        var settings = _pamSettingsField.GetValue(manager);
        if (settings == null)
            return;

        var observable = _settingsPlayerEyeHeightProperty.GetValue(settings, null);
        if (observable == null)
            return;

        _observableValueProperty.SetValue(observable, eyeHeight, null);
    }

    private static void TryInvokeOnPlayerEyeHeightChanged(float eyeHeight)
    {
        if (_onPlayerEyeHeightChangedHostMethod == null)
            return;

        var host = _cachedMeasureHost;
        if (host == null &&
            _settingsViewControllerType != null &&
            _generalSettingsHostProperty != null)
        {
            var settingsView = FindUnityObject(_settingsViewControllerType, includeInactive: true);
            if (settingsView != null)
                host = _generalSettingsHostProperty.GetValue(settingsView, null);
        }

        if (host == null)
            return;

        try
        {
            _onPlayerEyeHeightChangedHostMethod.Invoke(host, new object[] { eyeHeight });
        }
        catch
        {
            /* optional when CA menu is not open */
        }
    }

    private static bool TryMeasureEyeHeightMeters(object manager, out float eyeHeightMeters, out string source)
    {
        eyeHeightMeters = 0f;
        source = "";

        TryBeatSaberAutoSetPlayerHeight();

        if (_scaledEyeHeightProperty?.GetValue(manager, null) is float scaledEye && IsValidEyeHeight(scaledEye))
        {
            eyeHeightMeters = scaledEye;
            source = "Custom Avatars scaledEyeHeight";
            return true;
        }

        if (_trackingRigType != null && _trackingRigEyeHeightProperty != null)
        {
            var rig = FindUnityObject(_trackingRigType);
            if (rig != null &&
                _trackingRigEyeHeightProperty.GetValue(rig, null) is float rigEye &&
                IsValidEyeHeight(rigEye))
            {
                eyeHeightMeters = rigEye;
                source = "Custom Avatars TrackingRig";
                return true;
            }
        }

        if (TryReadBeatSaberPlayerHeightSettings(out var settingsHeight))
        {
            eyeHeightMeters = settingsHeight;
            source = "Beat Saber player height settings";
            return true;
        }

        var detector = Object.FindObjectOfType<PlayerHeightDetector>();
        if (detector != null && IsValidEyeHeight(detector.playerHeight))
        {
            eyeHeightMeters = detector.playerHeight;
            source = "PlayerHeightDetector";
            return true;
        }

        return false;
    }

    private static void TryBeatSaberAutoSetPlayerHeight()
    {
        if (_beatSaberAutoSetHeightMethod == null)
            return;

        var controller = Object.FindObjectOfType<PlayerHeightSettingsController>();
        if (controller == null)
            return;

        try
        {
            _beatSaberAutoSetHeightMethod.Invoke(controller, null);
        }
        catch
        {
            /* menu object may not be ready */
        }
    }

    private static bool TryReadBeatSaberPlayerHeightSettings(out float heightMeters)
    {
        heightMeters = 0f;
        if (_beatSaberPlayerHeightValueProperty == null)
            return false;

        var controller = Object.FindObjectOfType<PlayerHeightSettingsController>();
        if (controller == null)
            return false;

        var val = _beatSaberPlayerHeightValueProperty.GetValue(controller, null);
        if (val is not float f)
            return false;

        heightMeters = f;
        return IsValidEyeHeight(heightMeters);
    }

    private static bool IsValidEyeHeight(float meters) =>
        meters >= MinEyeHeightMeters && meters <= MaxEyeHeightMeters;

    private static object? ResolvePlayerAvatarManager()
    {
        if (_playerAvatarManagerType == null)
            return null;

        return FindUnityObject(_playerAvatarManagerType);
    }

    private static Object? FindUnityObject(System.Type type, bool includeInactive = false)
    {
        if (!typeof(Object).IsAssignableFrom(type))
            return null;

        return Object.FindObjectOfType(type, includeInactive);
    }

    private static bool EnsureReflection()
    {
        if (_reflectionReady)
            return _playerAvatarManagerType != null;

        _reflectionReady = true;
        _playerAvatarManagerType = System.Type.GetType("CustomAvatar.Player.PlayerAvatarManager, CustomAvatar");
        if (_playerAvatarManagerType == null)
            return false;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _trackingRigType = System.Type.GetType("CustomAvatar.Tracking.TrackingRig, CustomAvatar");
        if (_trackingRigType != null)
        {
            _trackingRigEyeHeightProperty = _trackingRigType.GetProperty("eyeHeight", flags);
            _trackingRigBeginCalibrationMethod = _trackingRigType.GetMethod(
                "BeginCalibration",
                flags,
                null,
                new[] { System.Type.GetType("CustomAvatar.Configuration.CalibrationMode, CustomAvatar")! },
                null);
            _trackingRigEndCalibrationMethod = _trackingRigType.GetMethod(
                "EndCalibration",
                flags,
                null,
                System.Type.EmptyTypes,
                null);
        }

        _calibrationModeType = System.Type.GetType("CustomAvatar.Configuration.CalibrationMode, CustomAvatar");

        _onPlayerHeightChangedMethod = _playerAvatarManagerType.GetMethod(
            "OnPlayerHeightChanged",
            flags,
            null,
            new[] { typeof(float) },
            null);

        _resizeCurrentAvatarMethod = _playerAvatarManagerType.GetMethod(
            "ResizeCurrentAvatar",
            flags,
            null,
            System.Type.EmptyTypes,
            null);

        _calculateAvatarScaleMethod = _playerAvatarManagerType.GetMethod(
            "CalculateAvatarScale",
            flags,
            null,
            new[] { typeof(float) },
            null);

        _managerScaleProperty = _playerAvatarManagerType.GetProperty("scale", flags);
        _pamSettingsField = _playerAvatarManagerType.GetField("_settings", flags);

        var settingsType = System.Type.GetType("CustomAvatar.Configuration.Settings, CustomAvatar");
        if (settingsType != null)
        {
            _settingsPlayerEyeHeightProperty = settingsType.GetProperty("playerEyeHeight", flags);
            var observableType = System.Type.GetType("CustomAvatar.Configuration.ObservableValue`1, CustomAvatar");
            if (observableType != null)
            {
                var observableFloat = observableType.MakeGenericType(typeof(float));
                _observableValueProperty = observableFloat.GetProperty("value", flags);
            }
        }

        _settingsViewControllerType = System.Type.GetType("CustomAvatar.UI.SettingsViewController, CustomAvatar");
        _generalSettingsHostType = System.Type.GetType("CustomAvatar.UI.GeneralSettingsHost, CustomAvatar");
        _armSpanMeasurerType = System.Type.GetType("CustomAvatar.UI.ArmSpanMeasurer, CustomAvatar");
        if (_settingsViewControllerType != null)
        {
            _generalSettingsHostProperty = _settingsViewControllerType.GetProperty(
                "generalSettingsHost",
                flags);
        }

        if (_generalSettingsHostType != null)
        {
            _measureHeightMethod = _generalSettingsHostType.GetMethod(
                "OnMeasureHeightButtonClicked",
                flags);
            _onPlayerEyeHeightChangedHostMethod = _generalSettingsHostType.GetMethod(
                "OnPlayerEyeHeightChanged",
                flags,
                null,
                new[] { typeof(float) },
                null);
            _generalSettingsHostHeightProperty = _generalSettingsHostType.GetProperty("height", flags);
            _isMeasureButtonEnabledProperty = _generalSettingsHostType.GetProperty("isMeasureButtonEnabled", flags);
        }

        _scaledEyeHeightProperty = _playerAvatarManagerType.GetProperty("scaledEyeHeight", flags);
        _currentlySpawnedAvatarProperty = _playerAvatarManagerType.GetProperty("currentlySpawnedAvatar", flags);

        var phscType = typeof(PlayerHeightSettingsController);
        _beatSaberAutoSetHeightMethod = phscType.GetMethod("AutoSetHeight", flags);
        _beatSaberPlayerHeightValueProperty = phscType.GetProperty("value", flags);

        return true;
    }
}
