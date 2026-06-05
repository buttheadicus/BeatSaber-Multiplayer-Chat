using System;
using System.Reflection;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

// Reads the local player's effective Custom Avatars scale (calibrated height / resize result).
internal static class MpCustomAvatarScaleSource
{
    private const float MinScale = 0.25f;

    private const float MaxScale = 4f;

    private static Type? _playerAvatarManagerType;

    private static PropertyInfo? _spawnedAvatarProperty;

    private static PropertyInfo? _spawnedAvatarScaleProperty;

    private static FieldInfo? _spawnedAvatarField;

    private static PropertyInfo? _managerScaleProperty;

    private static bool _reflectionReady;

    private static MonoBehaviour? _cachedManager;

    private static float _managerCacheTime;

    private const float ManagerCacheSeconds = 2f;

    public static bool TryGetLocalAvatarScale(out float scale)
    {
        scale = 1f;
        MpCustomAvatarHeightCalibration.ApplySavedPresetIfAny();

        if (!EnsureReflection())
            return false;

        var pam = ResolvePlayerAvatarManager();
        if (pam == null)
            return TryGetScaleFromSavedEyeHeight(out scale);

        if (_managerScaleProperty != null)
        {
            var managerScale = _managerScaleProperty.GetValue(pam, null);
            if (managerScale is float ms && ms > 0.001f)
            {
                scale = ClampScale(ms);
                return true;
            }
        }

        object? spawned = null;
        if (_spawnedAvatarProperty != null)
            spawned = _spawnedAvatarProperty.GetValue(pam, null);
        else if (_spawnedAvatarField != null)
            spawned = _spawnedAvatarField.GetValue(pam);

        if (spawned == null)
            return false;

        if (_spawnedAvatarScaleProperty != null)
        {
            var val = _spawnedAvatarScaleProperty.GetValue(spawned, null);
            if (val is float f)
            {
                scale = ClampScale(f);
                return true;
            }
        }

        if (spawned is Component component)
        {
            var ls = component.transform.localScale;
            var uniform = (ls.x + ls.y + ls.z) / 3f;
            if (uniform > 0.001f)
            {
                scale = ClampScale(uniform);
                return true;
            }
        }

        return TryGetScaleFromSavedEyeHeight(out scale);
    }

    private static bool TryGetScaleFromSavedEyeHeight(out float scale)
    {
        scale = 1f;
        if (!ModSettings.TryGetLobbyCustomAvatarSavedEyeHeight(out var eyeHeight))
            return false;

        if (!EnsureReflection())
            return false;

        var pam = ResolvePlayerAvatarManager();
        if (pam == null)
            return false;

        var calculate = _playerAvatarManagerType!.GetMethod(
            "CalculateAvatarScale",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(float) },
            null);
        if (calculate == null)
            return false;

        var scaleObj = calculate.Invoke(pam, new object[] { eyeHeight });
        if (scaleObj is not float f || f <= 0.001f)
            return false;

        scale = ClampScale(f);
        return true;
    }

    private static MonoBehaviour? ResolvePlayerAvatarManager()
    {
        var now = Time.realtimeSinceStartup;
        if (_cachedManager != null && now - _managerCacheTime < ManagerCacheSeconds)
            return _cachedManager;

        _cachedManager = UnityEngine.Object.FindObjectOfType(_playerAvatarManagerType!) as MonoBehaviour;
        _managerCacheTime = now;
        return _cachedManager;
    }

    private static bool EnsureReflection()
    {
        if (_reflectionReady)
            return _playerAvatarManagerType != null;

        _reflectionReady = true;
        _playerAvatarManagerType = Type.GetType("CustomAvatar.Player.PlayerAvatarManager, CustomAvatar");
        if (_playerAvatarManagerType == null)
            return false;

        _spawnedAvatarProperty = _playerAvatarManagerType.GetProperty("spawnedAvatar",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (_spawnedAvatarProperty == null)
            _spawnedAvatarProperty = _playerAvatarManagerType.GetProperty("SpawnedAvatar",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (_spawnedAvatarProperty == null)
        {
            _spawnedAvatarField = _playerAvatarManagerType.GetField("spawnedAvatar",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_spawnedAvatarField == null)
                _spawnedAvatarField = _playerAvatarManagerType.GetField("_spawnedAvatar",
                    BindingFlags.Instance | BindingFlags.NonPublic);
        }

        _managerScaleProperty = _playerAvatarManagerType.GetProperty("scale",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var spawnedType = Type.GetType("CustomAvatar.Avatar.SpawnedAvatar, CustomAvatar");
        if (spawnedType != null)
        {
            _spawnedAvatarScaleProperty = spawnedType.GetProperty("scale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_spawnedAvatarScaleProperty == null)
                _spawnedAvatarScaleProperty = spawnedType.GetProperty("Scale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        return true;
    }

    private static float ClampScale(float value) =>
        Mathf.Clamp(value, MinScale, MaxScale);
}
