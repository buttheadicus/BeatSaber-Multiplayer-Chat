using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HMUI;
using MultiplayerChat.Settings;
using TMPro;
using UnityEngine;

namespace MultiplayerChat.Core;

// floating score/combo HUD above remote players while playing or spectating
internal sealed class ExperimentalExtraStatsHost : MonoBehaviour
{
    private static ExperimentalExtraStatsHost? _live;

    private const float RescanInterval = 0.35f;
    private const float ScorePollInterval = 0.12f;

    private static readonly BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly Dictionary<int, Billboard> _bills = new();
    private float _nextRescan;
    private float _nextScorePoll;
    private TMP_FontAsset? _font;
    private Material? _fontMat;

    private Type? _rankedPlayerType;
    private FieldInfo? _playersField;
    private FieldInfo? _syncField;
    private FieldInfo? _sharedTimeField;
    private MethodInfo? _getState;
    private object? _comboEnum;
    private object? _multEnum;
    private object? _maxEnum;
    private object? _scoreEnum;
    private bool _scoreTypesTried;

    private void OnEnable() => _live = this;

    private void OnDisable()
    {
        if (_live == this)
            _live = null;
        ClearAll();
    }

    private void OnDestroy()
    {
        if (_live == this)
            _live = null;
        ClearAll();
    }

    private void Update()
    {
        if (_live == null)
            _live = this;
        if (_live != this)
        {
            if (_bills.Count > 0)
                ClearAll();
            return;
        }

        if (!ModSettings.EnableExtraStats)
        {
            if (_bills.Count > 0)
                ClearAll();
            return;
        }

        var now = Time.unscaledTime;
        if (now >= _nextRescan)
        {
            _nextRescan = now + RescanInterval;
            Rescan();
        }

        if (now >= _nextScorePoll)
        {
            _nextScorePoll = now + ScorePollInterval;
            RefreshScores();
        }

        var height = ModSettings.ExtraStatsHeightMeters;
        var cam = Camera.main;
        foreach (var bill in _bills.Values)
            bill.Tick(height, cam);
    }

    private void ClearAll()
    {
        foreach (var bill in _bills.Values)
            bill.Destroy();
        _bills.Clear();
    }

    private void Rescan()
    {
        var seen = new HashSet<int>();

        foreach (var facade in Resources.FindObjectsOfTypeAll<MultiplayerConnectedPlayerFacade>())
        {
            if (facade == null || IsRedecoratorShadow(facade.transform))
                continue;
            if (facade.GetComponent<MultiplayerLocalActivePlayerFacade>() != null)
                continue;
            var player = GetPlayerFromObject(facade);
            if (player == null || player.isMe)
                continue;
            Track(facade.transform, player, seen);
        }

        foreach (var ctrl in Resources.FindObjectsOfTypeAll<MultiplayerLobbyAvatarController>())
        {
            if (ctrl == null)
                continue;
            var player = GetPlayerFromObject(ctrl);
            if (player == null || player.isMe)
                continue;
            Track(ctrl.transform, player, seen);
        }

        var dead = new List<int>();
        foreach (var kv in _bills)
        {
            if (!seen.Contains(kv.Key) || kv.Value.Anchor == null)
                dead.Add(kv.Key);
        }

        for (var i = 0; i < dead.Count; i++)
        {
            if (_bills.TryGetValue(dead[i], out var bill))
                bill.Destroy();
            _bills.Remove(dead[i]);
        }
    }

    private void Track(Transform anchor, IConnectedPlayer player, HashSet<int> seen)
    {
        var id = anchor.GetInstanceID();
        seen.Add(id);
        if (_bills.ContainsKey(id))
        {
            _bills[id].Player = player;
            return;
        }

        _bills[id] = Billboard.Create(anchor, player, StealFont(), _fontMat);
    }

    private void RefreshScores()
    {
        if (_bills.Count == 0)
            return;

        var snapshot = TryReadScores();
        foreach (var bill in _bills.Values)
        {
            if (bill.Player == null)
                continue;
            if (snapshot != null && snapshot.TryGetValue(bill.Player.userId, out var line))
                bill.SetStats(line);
            else
                bill.SetStats(null);
        }
    }

    private Dictionary<string, string>? TryReadScores()
    {
        try
        {
            EnsureScoreTypes();
            var providerType = Type.GetType("MultiplayerScoreProvider, Main");
            if (providerType == null)
                return null;

            var providers = Resources.FindObjectsOfTypeAll(providerType);
            if (providers == null || providers.Length == 0)
                return null;

            UnityEngine.Object? provider = null;
            for (var i = 0; i < providers.Length; i++)
            {
                var o = providers[i];
                if (o is Behaviour b && b.isActiveAndEnabled)
                {
                    provider = o;
                    break;
                }

                provider ??= o;
            }

            if (provider == null || _playersField == null || _getState == null)
                return null;

            if (_playersField.GetValue(provider) is not IDictionary players)
                return null;

            var sharedTime = 0L;
            if (_sharedTimeField != null)
                sharedTime = (long)_sharedTimeField.GetValue(provider)!;

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in players)
            {
                var ranked = entry.Value;
                if (ranked == null)
                    continue;
                var userId = _rankedPlayerType?.GetProperty("userId")?.GetValue(ranked) as string;
                if (string.IsNullOrEmpty(userId))
                    continue;

                var sync = _syncField?.GetValue(ranked);
                if (sync == null)
                    continue;

                var score = InvokeState(sync, _scoreEnum, sharedTime);
                var max = InvokeState(sync, _maxEnum, sharedTime);
                var combo = InvokeState(sync, _comboEnum, sharedTime);
                var mult = InvokeState(sync, _multEnum, sharedTime);
                var failed = _rankedPlayerType?.GetProperty("isFailed")?.GetValue(ranked) as bool? == true;

                var pct = max > 0 ? (100.0 * score / max) : 0.0;
                var sb = new StringBuilder(96);
                sb.Append(pct.ToString("0.00", CultureInfo.InvariantCulture)).Append("%\n");
                sb.Append("score ").Append(score).Append('\n');
                sb.Append("combo ").Append(combo);
                if (mult > 0)
                    sb.Append("  x").Append(mult);
                if (failed)
                    sb.Append("\nFAILED");
                result[userId!] = sb.ToString();
            }

            return result;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Debug("[MPChat][Experimental] extra stats score read failed: " + ex.Message);
            return null;
        }
    }

    private int InvokeState(object sync, object? scoreEnum, long time)
    {
        if (_getState == null || scoreEnum == null)
            return 0;
        try
        {
            var v = _getState.Invoke(sync, new object[] { scoreEnum, time });
            return v is int i ? i : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void EnsureScoreTypes()
    {
        if (_scoreTypesTried)
            return;
        _scoreTypesTried = true;

        var providerType = Type.GetType("MultiplayerScoreProvider, Main");
        if (providerType == null)
            return;

        _rankedPlayerType = providerType.GetNestedType("RankedPlayer");
        _playersField = providerType.GetField("_players", Flags);
        _sharedTimeField = providerType.GetField("_sharedOffsetSyncTime", Flags);
        _syncField = _rankedPlayerType?.GetField("_multiplayerSyncState", Flags);

        var syncType = _syncField?.FieldType;
        if (syncType != null)
        {
            foreach (var m in syncType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m.Name != "GetState")
                    continue;
                var ps = m.GetParameters();
                if (ps.Length == 2)
                {
                    _getState = m;
                    break;
                }
            }
        }

        Type? scoreEnumType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(asm.GetName().Name, "BGNetCore", StringComparison.Ordinal))
                continue;
            var table = asm.GetType("StandardScoreSyncState");
            scoreEnumType = table?.GetNestedType("Score");
            break;
        }

        if (scoreEnumType == null)
            return;

        try
        {
            _scoreEnum = Enum.Parse(scoreEnumType, "MultipliedScore");
            _maxEnum = Enum.Parse(scoreEnumType, "ImmediateMaxPossibleMultipliedScore");
            _comboEnum = Enum.Parse(scoreEnumType, "Combo");
            _multEnum = Enum.Parse(scoreEnumType, "Multiplier");
        }
        catch
        {
            // leave nulls
        }
    }

    private TMP_FontAsset? StealFont()
    {
        if (_font != null)
            return _font;
        try
        {
            var donors = Resources.FindObjectsOfTypeAll<CurvedTextMeshPro>();
            for (var i = 0; i < donors.Length; i++)
            {
                var d = donors[i];
                if (d == null || d.font == null)
                    continue;
                if (d.gameObject.name.IndexOf("MpChatExtraStats", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                _font = d.font;
                _fontMat = d.fontSharedMaterial;
                return _font;
            }
        }
        catch
        {
            // ignore
        }

        return _font;
    }

    private static bool IsRedecoratorShadow(Transform? facadeRoot)
    {
        if (facadeRoot == null)
            return true;
        var root = facadeRoot.root;
        return root != null && string.Equals(root.name, "InternalRedecorator", StringComparison.Ordinal);
    }

    private static IConnectedPlayer? GetPlayerFromObject(object? obj)
    {
        if (obj == null)
            return null;
        var t = obj.GetType();
        foreach (var name in new[] { "_connectedPlayer", "_player", "connectedPlayer", "_playerData" })
        {
            var field = t.GetField(name, Flags);
            if (field?.GetValue(obj) is IConnectedPlayer p)
                return p;
        }

        foreach (var f in t.GetFields(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType) && f.GetValue(obj) is IConnectedPlayer p)
                return p;
        }

        foreach (var prop in t.GetProperties(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(prop.PropertyType)
                && prop.GetValue(obj) is IConnectedPlayer p)
                return p;
        }

        return null;
    }

    private sealed class Billboard
    {
        internal Transform? Anchor;
        internal IConnectedPlayer? Player;
        private GameObject? _root;
        private CurvedTextMeshPro? _tmp;
        private string _last = "";

        internal static Billboard Create(Transform anchor, IConnectedPlayer player, TMP_FontAsset? font, Material? fontMat)
        {
            var bill = new Billboard { Anchor = anchor, Player = player };
            var go = new GameObject("MpChatExtraStats");
            go.layer = 5;
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, ModSettings.ExtraStatsHeightMeters, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.04f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.TexCoord2
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250f, 110f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var textGo = new GameObject("Text");
            textGo.layer = 5;
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<CurvedTextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 16f;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.richText = false;
            tmp.raycastTarget = false;
            if (font != null)
                tmp.font = font;
            if (fontMat != null)
                tmp.fontSharedMaterial = fontMat;
            tmp.text = player.userName ?? "";

            bill._root = go;
            bill._tmp = tmp;
            return bill;
        }

        internal void SetStats(string? stats)
        {
            if (_tmp == null)
                return;
            var name = Player?.userName ?? "";
            var body = string.IsNullOrEmpty(stats) ? name + "\n--" : name + "\n" + stats;
            if (body == _last)
                return;
            _last = body;
            _tmp.text = body;
        }

        internal void Tick(float height, Camera? cam)
        {
            if (_root == null || Anchor == null)
                return;
            _root.transform.localPosition = new Vector3(0f, height, 0f);
            if (cam == null)
                return;
            var toCam = cam.transform.position - _root.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                _root.transform.rotation = Quaternion.LookRotation(toCam);
        }

        internal void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _tmp = null;
            Anchor = null;
        }
    }
}
