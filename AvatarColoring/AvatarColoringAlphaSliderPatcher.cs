using System;
using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Tags.Settings;
using HMUI;
using IPA.Utilities;
using MultiplayerChat.Settings;
using SiraUtil.Affinity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.AvatarColoring;

// RGBA sliders for the stock color editor (built in code, not BSML).
public sealed class AvatarColoringAlphaSliderPatcher : IAffinity, IInitializable, IDisposable
{
    private const float SliderMin = -600f;
    private const float SliderMax = 600f;

    private const float ColumnPreferredWidth = 64f;

    private const float ColumnMinWidth = 56f;

    private const float SliderVisualScale = 1.1f;

    // Whole r/g/b/a column position in anchored pixels after layout parents this rect.
    public static Vector2 RgbaSliderStackAnchoredPixelOffset = new Vector2(24f, 48f);

    // -1 keeps auto order; otherwise set sibling index on the column under the parent row.
    public static int RgbaStackSiblingIndex = -1;

    // Nudge r/g/b/a labels relative to each slider row.
    public static Vector2 RgbaChannelLetterAnchoredPixelNudge = new Vector2(10f, 0f);

    internal static AvatarColoringAlphaSliderPatcher? Instance { get; private set; }

    [Inject] private readonly BeatAvatarEditorViewController _beatAvatarEditorViewController = null!;

    [Inject] private readonly EditAvatarColorViewController _editColorViewController = null!;

    private GameObject? _alphaColumnRoot;
    private RangeValuesTextSlider? _rHmSlider;
    private RangeValuesTextSlider? _gHmSlider;
    private RangeValuesTextSlider? _bHmSlider;
    private RangeValuesTextSlider? _alphaHmSlider;

    private bool _applyingSlidersFromCode;

    public void Initialize()
    {
        Instance = this;
        _beatAvatarEditorViewController.didRequestColorChangeEvent += HandleDidRequestColorChange;
        _editColorViewController.didChangeColorEvent += HandleExternalColorChanged;
    }

    public void Dispose()
    {
        if (Instance == this)
            Instance = null;
        _beatAvatarEditorViewController.didRequestColorChangeEvent -= HandleDidRequestColorChange;
        _editColorViewController.didChangeColorEvent -= HandleExternalColorChanged;
        TeardownAlphaUi();
    }

    private void HandleDidRequestColorChange(Action<Color> colorCallback, Color currentColor, AvatarPart editPart,
        int uvSegment)
    {
        if (!ModSettings.EnableAvatarColoringExtensions)
            return;
        AvatarColorEditContext.OnColorEditRequested(currentColor, editPart, uvSegment);
    }

    internal static void NotifyAvatarDataReloadedWhileColorUiOpen()
    {
        Instance?.SyncSlidersFromReloadedAvatarData();
    }

    private void SyncSlidersFromReloadedAvatarData()
    {
        if (!ModSettings.EnableAvatarColoringExtensions
            || !_editColorViewController.gameObject.activeInHierarchy
            || _alphaHmSlider == null)
            return;

        var data = AvatarColoringEditorSession.DataModel?.avatarData;
        if (data == null)
            return;

        if (!AvatarDataColorResolver.TryGetColor(data, AvatarColorEditContext.LastPart, out var c))
            c = _editColorViewController.color;

        _applyingSlidersFromCode = true;
        try
        {
            _editColorViewController.SetColor(c);
            _editColorViewController.InvokeMethod<object, EditAvatarColorViewController>("ChangeColor", c);
            ApplySliderValues(c);
        }
        finally
        {
            _applyingSlidersFromCode = false;
        }
    }

    private void HandleExternalColorChanged(Color value)
    {
        if (!ModSettings.EnableAvatarColoringExtensions || _applyingSlidersFromCode
                                                        || _alphaHmSlider == null)
            return;

        _applyingSlidersFromCode = true;
        try
        {
            ApplySliderValues(value);
        }
        finally
        {
            _applyingSlidersFromCode = false;
        }
    }

    private void ApplySliderValues(Color value)
    {
        if (_rHmSlider != null)
            _rHmSlider.value = value.r;
        if (_gHmSlider != null)
            _gHmSlider.value = value.g;
        if (_bHmSlider != null)
            _bHmSlider.value = value.b;
        if (_alphaHmSlider != null)
            _alphaHmSlider.value = value.a;
    }

    [AffinityPatch(typeof(EditAvatarColorViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (!ModSettings.EnableAvatarColoringExtensions || !addedToHierarchy)
            return;

        TeardownAlphaUi();
        BuildAlphaUi();
    }

    [AffinityPatch(typeof(EditAvatarColorViewController), "DidDeactivate")]
    [AffinityPostfix]
    public void PostfixEditColorDidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (!removedFromHierarchy)
            return;
        TeardownAlphaUi();
    }

    private void BuildAlphaUi()
    {
        var bottom = _editColorViewController.transform.Find("BottomPanel") ?? _editColorViewController.transform;

        Transform? wideRow = null;
        var bestKids = 0;
        for (var i = 0; i < bottom.childCount; i++)
        {
            var row = bottom.GetChild(i);
            if (row.GetComponent<HorizontalLayoutGroup>() == null)
                continue;
            if (row.childCount > bestKids)
            {
                bestKids = row.childCount;
                wideRow = row;
            }
        }

        var parentRow = wideRow ?? bottom;

        _alphaColumnRoot = new GameObject("MPChatAlphaColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        _alphaColumnRoot.transform.SetParent(parentRow, false);
        var colRt = (RectTransform)_alphaColumnRoot.transform;
        colRt.localScale = Vector3.one;
        colRt.anchoredPosition = RgbaSliderStackAnchoredPixelOffset;

        if (RgbaStackSiblingIndex >= 0 && RgbaStackSiblingIndex < parentRow.childCount)
            _alphaColumnRoot.transform.SetSiblingIndex(RgbaStackSiblingIndex);

        var colLe = _alphaColumnRoot.AddComponent<LayoutElement>();
        colLe.preferredWidth = ColumnPreferredWidth;
        colLe.minWidth = ColumnMinWidth;
        colLe.flexibleWidth = 0f;

        var vlg = _alphaColumnRoot.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 1f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        var c = AvatarColorEditContext.TryConsumePendingInitialColor(out var pending)
            ? pending
            : _editColorViewController.color;
        _rHmSlider = AddChannelSlider("r", c.r,
            v =>
            {
                var x = _editColorViewController.color;
                x.r = v;
                PushColor(x);
            });
        _gHmSlider = AddChannelSlider("g", c.g,
            v =>
            {
                var x = _editColorViewController.color;
                x.g = v;
                PushColor(x);
            });
        _bHmSlider = AddChannelSlider("b", c.b,
            v =>
            {
                var x = _editColorViewController.color;
                x.b = v;
                PushColor(x);
            });
        _alphaHmSlider = AddChannelSlider("a", c.a,
            v =>
            {
                var x = _editColorViewController.color;
                x.a = v;
                PushColor(x);
            });

        if (parentRow is RectTransform prt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(colRt);
        colRt.anchoredPosition = RgbaSliderStackAnchoredPixelOffset;
    }

    private void PushColor(Color c)
    {
        _editColorViewController.SetColor(c);
        _editColorViewController.InvokeMethod<object, EditAvatarColorViewController>("ChangeColor", c);
    }

    // BSML SliderSetting rows ship with "Default Text"; we reuse that label as r/g/b/a.
    private RangeValuesTextSlider AddChannelSlider(string channelLabel, float initial, Action<float> apply)
    {
        var sliderHost = new SliderSettingTag().CreateObject(_alphaColumnRoot!.transform);
        var sliderRt = sliderHost.GetComponent<RectTransform>();
        if (sliderRt != null)
            sliderRt.localScale = new Vector3(SliderVisualScale, SliderVisualScale, 1f);

        var ss = sliderHost.GetComponent<SliderSetting>();
        ss.Increments = 1f;
        ss.IsInt = false;

        var hmSlider = ss.Slider;
        hmSlider.minValue = SliderMin;
        hmSlider.maxValue = SliderMax;

        ss.Setup();
        StyleSliderRowLabelAndTighten(sliderHost, channelLabel);

        hmSlider.value = initial;
        StyleSliderRowLabelAndTighten(sliderHost, channelLabel);

        hmSlider.valueDidChangeEvent += (_, val) =>
        {
            if (!ModSettings.EnableAvatarColoringExtensions || _applyingSlidersFromCode)
                return;
            try
            {
                _applyingSlidersFromCode = true;
                apply(val);
            }
            finally
            {
                _applyingSlidersFromCode = false;
            }
        };

        return hmSlider;
    }

    private static void StyleSliderRowLabelAndTighten(GameObject sliderHost, string letter)
    {
        var h = sliderHost.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (h != null)
            h.spacing = 0.5f;

        foreach (var tmp in sliderHost.GetComponentsInChildren<TMP_Text>(true))
        {
            var s = tmp.text?.Trim() ?? "";
            if (!string.Equals(s, "Default Text", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s, "Default String", StringComparison.OrdinalIgnoreCase))
                continue;

            tmp.text = letter;
            tmp.fontSize = 3.2f;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            var le = tmp.GetComponent<LayoutElement>();
            if (le == null)
                le = tmp.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 5.5f;
            le.minWidth = 4f;
            le.flexibleWidth = 0f;

            var rct = tmp.rectTransform;
            if (rct != null)
                rct.anchoredPosition += RgbaChannelLetterAnchoredPixelNudge;
        }
    }

    private void TeardownAlphaUi()
    {
        _rHmSlider = null;
        _gHmSlider = null;
        _bHmSlider = null;
        _alphaHmSlider = null;

        if (_alphaColumnRoot != null)
        {
            UnityEngine.Object.Destroy(_alphaColumnRoot);
            _alphaColumnRoot = null;
        }
    }
}
