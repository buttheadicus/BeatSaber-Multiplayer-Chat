using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MultiplayerChat.Settings;
using UnityEngine;

// cool, groks name dropping link dlls. thanks grok, i only ASKED you to import that into this codebase, not talk about its origins.

namespace MultiplayerChat.Core;

// port of SLZLink RemoteAvatarDataExporter: BeatAvatar binary -> AvatarData.dat JSON.
// local only. never send MPC packets; snatch reads vanilla multiplayerAvatarsData already on the player.
internal static class SkinSnatcherExporter
{
    private static readonly BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type? _sessionManagerType;
    private static PropertyInfo? _connectedPlayersProp;
    private static Type? _lobbyFcType;
    private static FieldInfo? _lobbyFcStateField;
    private static Type? _lobbyAvatarManagerType;
    private static FieldInfo? _lobbyAvatarStateField;
    private static PropertyInfo? _userIdProp;
    private static PropertyInfo? _avatarsDataProp;
    private static FieldInfo? _avatarsListField;
    private static FieldInfo? _avatarTypeHashField;
    private static FieldInfo? _avatarBytesField;
    private static uint? _beatAvatarHash;
    private static bool _triedResolve;

    internal static bool TrySaveToAvatarStorage(string userId, string userName, out string error)
    {
        error = "";
        if (!TryExportAvatarDataJson(userId, out var json, out error) || string.IsNullOrEmpty(json))
            return false;

        var folderName = SanitizeFileName(userName);
        if (string.IsNullOrEmpty(folderName))
            folderName = SanitizeFileName(userId);
        if (string.IsNullOrEmpty(folderName))
        {
            error = "bad player name";
            return false;
        }

        try
        {
            var dir = Path.Combine(ChatIdFilePaths.AvatarStorageDirectoryPath, folderName);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "AvatarData.dat");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Plugin.Log?.Info("[MPChat][Experimental] skin snatch saved " + path);
            return true;
        }
        catch (Exception ex)
        {
            error = "failed writing Avatar Storage: " + ex.Message;
            return false;
        }
    }

    private static bool TryExportAvatarDataJson(string userId, out string json, out string error)
    {
        json = "";
        error = "";
        if (string.IsNullOrEmpty(userId))
        {
            error = "empty userId";
            return false;
        }

        try
        {
            EnsureResolved();
            var player = FindConnectedPlayer(userId);
            if (player == null)
            {
                error = "player not found in session";
                return false;
            }

            var avatarsData = ReadAvatarsData(player);
            if (avatarsData == null)
            {
                error = "player has no multiplayerAvatarsData";
                return false;
            }

            var listObj = _avatarsListField?.GetValue(avatarsData);
            if (listObj is not IEnumerable list)
            {
                error = "multiplayerAvatarsData list missing";
                return false;
            }

            byte[]? fallbackData = null;
            foreach (var entry in list)
            {
                if (entry == null)
                    continue;

                var data = _avatarBytesField?.GetValue(entry) as byte[];
                if (data == null || data.Length == 0)
                    continue;

                uint hash = 0;
                if (_avatarTypeHashField != null)
                    hash = (uint)_avatarTypeHashField.GetValue(entry)!;

                var hashOk = !_beatAvatarHash.HasValue || hash == 0 || hash == _beatAvatarHash.Value;
                if (!hashOk)
                {
                    fallbackData ??= data;
                    continue;
                }

                return TryParseBeatAvatarBinary(data, out json, out error);
            }

            if (fallbackData != null)
                return TryParseBeatAvatarBinary(fallbackData, out json, out error);

            error = "no BeatAvatar payload on player";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void EnsureResolved()
    {
        if (_triedResolve)
            return;
        _triedResolve = true;

        _sessionManagerType = ResolveType("BeatSaberMultiplayerSessionManager")
                              ?? ResolveType("IBeatSaberMultiplayerSessionManager")
                              ?? ResolveType("MultiplayerSessionManager");

        if (_sessionManagerType != null)
            _connectedPlayersProp = FindProperty(_sessionManagerType, "connectedPlayers");

        _lobbyFcType = ResolveType("GameServerLobbyFlowCoordinator");
        if (_lobbyFcType != null)
        {
            _lobbyFcStateField = FindField(_lobbyFcType, "_lobbyStateDataModel")
                                 ?? FindFieldByTypeName(_lobbyFcType, "ILobbyStateDataModel", "LobbyStateDataModel");
        }

        _lobbyAvatarManagerType = ResolveType("MultiplayerLobbyAvatarManager");
        if (_lobbyAvatarManagerType != null)
        {
            _lobbyAvatarStateField = FindField(_lobbyAvatarManagerType, "_lobbyStateDataModel")
                                     ?? FindFieldByTypeName(_lobbyAvatarManagerType, "ILobbyStateDataModel", "LobbyStateDataModel");
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (string.Equals(name, "BeatSaber.BeatAvatarAdapter", StringComparison.Ordinal))
            {
                var beatSystem = asm.GetType("BeatSaber.BeatAvatarAdapter.BeatAvatarSystem", false);
                var idField = beatSystem?.GetField(
                    "kAvatarSystemTypeIdentifier",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var identifier = idField?.GetValue(null);
                if (identifier != null)
                {
                    var hashField = identifier.GetType().GetField("hash", Flags);
                    if (hashField != null)
                        _beatAvatarHash = (uint)hashField.GetValue(identifier)!;
                }
            }

            if (_avatarsDataProp == null
                && (string.Equals(name, "BeatSaber.Multiplayer.Core", StringComparison.Ordinal)
                    || string.Equals(name, "Main", StringComparison.Ordinal)
                    || string.Equals(name, "GameplayCore", StringComparison.Ordinal)))
            {
                var playerType = asm.GetType("IBeatSaberConnectedPlayer", false)
                                 ?? FindTypeByName(asm, "IBeatSaberConnectedPlayer")
                                 ?? FindTypeByName(asm, "BeatSaberConnectedPlayer");
                _avatarsDataProp ??= playerType?.GetProperty("multiplayerAvatarsData", Flags)
                                    ?? FindProperty(playerType, "multiplayerAvatarsData");
            }

            if (_avatarsListField == null
                && (string.Equals(name, "GameplayCore", StringComparison.Ordinal)
                    || string.Equals(name, "Main", StringComparison.Ordinal)))
            {
                var avatarsType = asm.GetType("MultiplayerAvatarsData", false)
                                  ?? FindTypeByName(asm, "MultiplayerAvatarsData");
                var avatarType = asm.GetType("MultiplayerAvatarData", false)
                                 ?? FindTypeByName(asm, "MultiplayerAvatarData");
                _avatarsListField ??= avatarsType?.GetField("multiplayerAvatarsData", Flags);
                _avatarTypeHashField ??= avatarType?.GetField("avatarTypeIdentifierHash", Flags);
                _avatarBytesField ??= avatarType?.GetField("data", Flags);
            }
        }
    }

    private static object? FindConnectedPlayer(string userId)
    {
        var found = FindPlayerInPlayersObject(GetSessionPlayers(), userId);
        if (found != null)
            return found;
        return FindPlayerInPlayersObject(GetLobbyStatePlayers(), userId);
    }

    private static object? GetSessionPlayers()
    {
        if (_sessionManagerType == null || _connectedPlayersProp == null)
            return null;

        var session = FindAnySceneObject(_sessionManagerType);
        if (session == null)
            return null;

        return _connectedPlayersProp.GetValue(session, null);
    }

    private static object? GetLobbyStatePlayers()
    {
        object? model = null;

        if (_lobbyFcType != null && _lobbyFcStateField != null)
        {
            var fc = FindAnySceneObject(_lobbyFcType);
            if (fc != null)
                model = _lobbyFcStateField.GetValue(fc);
        }

        if (model == null && _lobbyAvatarManagerType != null && _lobbyAvatarStateField != null)
        {
            var manager = FindAnySceneObject(_lobbyAvatarManagerType);
            if (manager != null)
                model = _lobbyAvatarStateField.GetValue(manager);
        }

        if (model == null)
            return null;

        var playersProp = FindProperty(model.GetType(), "connectedPlayers")
                          ?? FindProperty(model.GetType(), "rawConnectedPlayers");
        return playersProp?.GetValue(model, null);
    }

    private static object? FindPlayerInPlayersObject(object? playersObj, string userId)
    {
        if (playersObj is not IEnumerable enumerable)
            return null;

        foreach (var player in enumerable)
        {
            if (player == null)
                continue;
            var id = ReadUserId(player);
            if (string.Equals(id, userId, StringComparison.Ordinal))
                return player;
        }

        return null;
    }

    private static string? ReadUserId(object player)
    {
        if (_userIdProp == null || _userIdProp.DeclaringType == null
            || !_userIdProp.DeclaringType.IsAssignableFrom(player.GetType()))
        {
            _userIdProp = FindProperty(player.GetType(), "userId")
                          ?? FindProperty(player.GetType(), "UserId");
        }

        return _userIdProp?.GetValue(player, null) as string;
    }

    private static object? ReadAvatarsData(object player)
    {
        var prop = _avatarsDataProp;
        if (prop == null || prop.DeclaringType == null
            || !prop.DeclaringType.IsAssignableFrom(player.GetType()))
        {
            prop = FindProperty(player.GetType(), "multiplayerAvatarsData");
            if (prop != null)
                _avatarsDataProp = prop;
        }

        return prop?.GetValue(player, null);
    }

    private static object? FindAnySceneObject(Type type)
    {
        if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
            return null;

        var all = Resources.FindObjectsOfTypeAll(type);
        if (all == null || all.Length == 0)
            return null;

        object? fallback = null;
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] is not Component c || !c.gameObject.scene.IsValid() || !c.gameObject.scene.isLoaded)
                continue;
            if (c is Behaviour b && b.isActiveAndEnabled)
                return c;
            fallback ??= c;
        }

        return fallback;
    }

    private static Type? ResolveType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (!string.Equals(name, "Main", StringComparison.Ordinal)
                && !string.Equals(name, "GameplayCore", StringComparison.Ordinal)
                && !string.Equals(name, "DataModels", StringComparison.Ordinal)
                && !string.Equals(name, "BeatSaber.Multiplayer.Core", StringComparison.Ordinal)
                && !string.Equals(name, "BGNetCore", StringComparison.Ordinal)
                && !string.Equals(name, "Networking", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var t = asm.GetType(typeName, false) ?? FindTypeByName(asm, typeName);
                if (t != null)
                    return t;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static PropertyInfo? FindProperty(Type? type, string propName)
    {
        if (type == null || string.IsNullOrEmpty(propName))
            return null;

        for (var t = type; t != null; t = t.BaseType)
        {
            var p = t.GetProperty(propName, Flags | BindingFlags.FlattenHierarchy);
            if (p != null)
                return p;

            foreach (var iface in t.GetInterfaces())
            {
                p = iface.GetProperty(propName, Flags);
                if (p != null)
                    return p;
            }
        }

        return null;
    }

    private static FieldInfo? FindField(Type type, string fieldName)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(fieldName, Flags);
            if (f != null)
                return f;
        }

        return null;
    }

    private static FieldInfo? FindFieldByTypeName(Type type, params string[] typeNames)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            foreach (var f in t.GetFields(Flags))
            {
                var n = f.FieldType.Name;
                for (var i = 0; i < typeNames.Length; i++)
                {
                    if (string.Equals(n, typeNames[i], StringComparison.Ordinal))
                        return f;
                }
            }
        }

        return null;
    }

    private static bool TryParseBeatAvatarBinary(byte[] data, out string json, out string error)
    {
        json = "";
        error = "";
        try
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms, Encoding.UTF8);
            var headTopId = br.ReadString();
            var headTopPrimary = ReadColor(br);
            var headTopSecondary = ReadColor(br);
            var glassesId = br.ReadString();
            var glassesColor = ReadColor(br);
            var facialHairId = br.ReadString();
            var facialHairColor = ReadColor(br);
            var handsId = br.ReadString();
            var handsColor = ReadColor(br);
            var clothesId = br.ReadString();
            var clothesPrimary = ReadColor(br);
            var clothesSecondary = ReadColor(br);
            var clothesDetail = ReadColor(br);
            var skinColorId = br.ReadString();
            var eyesId = br.ReadString();
            var mouthId = br.ReadString();

            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendString(sb, "headTopId", headTopId);
            sb.Append(',');
            AppendString(sb, "glassesId", glassesId);
            sb.Append(',');
            AppendString(sb, "facialHairId", facialHairId);
            sb.Append(',');
            AppendString(sb, "handsId", handsId);
            sb.Append(',');
            AppendString(sb, "clothesId", clothesId);
            sb.Append(',');
            AppendString(sb, "skinColorId", skinColorId);
            sb.Append(',');
            AppendString(sb, "mouthId", mouthId);
            sb.Append(',');
            AppendString(sb, "eyesId", eyesId);
            sb.Append(',');
            AppendColor(sb, "headTopPrimaryColor", headTopPrimary);
            sb.Append(',');
            AppendColor(sb, "headTopSecondaryColor", headTopSecondary);
            sb.Append(',');
            AppendColor(sb, "glassesColor", glassesColor);
            sb.Append(',');
            AppendColor(sb, "facialHairColor", facialHairColor);
            sb.Append(',');
            AppendColor(sb, "handsColor", handsColor);
            sb.Append(',');
            AppendColor(sb, "clothesPrimaryColor", clothesPrimary);
            sb.Append(',');
            AppendColor(sb, "clothesSecondaryColor", clothesSecondary);
            sb.Append(',');
            AppendColor(sb, "clothesDetailColor", clothesDetail);
            sb.Append('}');
            json = sb.ToString();
            return true;
        }
        catch (Exception ex)
        {
            error = "failed parsing BeatAvatar binary: " + ex.Message;
            return false;
        }
    }

    private static Color ReadColor(BinaryReader br) =>
        new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

    private static void AppendString(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":\"").Append(Escape(value ?? "")).Append('"');
    }

    private static void AppendColor(StringBuilder sb, string key, Color c)
    {
        sb.Append('"').Append(key).Append("\":{");
        sb.Append("\"r\":").Append(F(c.r)).Append(',');
        sb.Append("\"g\":").Append(F(c.g)).Append(',');
        sb.Append("\"b\":").Append(F(c.b)).Append(',');
        sb.Append("\"a\":").Append(F(c.a));
        sb.Append('}');
    }

    private static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture);

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string SanitizeFileName(string name)
    {
        var s = (name ?? "").Trim();
        if (string.IsNullOrEmpty(s))
            return "";
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c.ToString(), "");
        return s.Trim();
    }

    private static Type? FindTypeByName(Assembly asm, string typeName)
    {
        try
        {
            foreach (var t in asm.GetTypes())
            {
                if (string.Equals(t.Name, typeName, StringComparison.Ordinal))
                    return t;
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var t in ex.Types)
            {
                if (t != null && string.Equals(t.Name, typeName, StringComparison.Ordinal))
                    return t;
            }
        }

        return null;
    }
}
