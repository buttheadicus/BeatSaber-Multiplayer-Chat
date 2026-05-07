using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.AvatarColoring;
using MultiplayerChat.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.AvatarLoadListView.bsml")]
public sealed class AvatarLoadListViewController : BSMLAutomaticViewController
{
    private const int SlotsPerRow = 8;

    public event Action<string>? PresetSelected;
    public event Action? Closed;

    [UIComponent("PresetListRoot")] private RectTransform? _presetRoot;

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ResolvePresetRoot();
        RebuildList();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ResolvePresetRoot();
        RebuildList();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    private void ResolvePresetRoot()
    {
        if (_presetRoot != null)
            return;
        var found = transform.Find("PresetListRoot");
        if (found is RectTransform rt)
            _presetRoot = rt;
    }

    private void RebuildList()
    {
        ResolvePresetRoot();
        if (_presetRoot == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat][AvatarLoad] PresetListRoot not resolved; load list is empty in UI.");
            return;
        }

        for (var i = _presetRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_presetRoot.GetChild(i).gameObject);

        var names = new List<string>(AvatarDatOperations.ListPresetNames());
        MultiplayerChat.Plugin.Log?.Info(
            $"[MPChat][AvatarLoad] {names.Count} presets under {ChatIdFilePaths.AvatarStorageDirectoryPath}");

        for (var i = 0; i < names.Count; i += SlotsPerRow)
        {
            var row = new GameObject("PresetRow", typeof(RectTransform));
            row.transform.SetParent(_presetRoot, false);
            var rowRt = (RectTransform)row.transform;
            rowRt.localScale = Vector3.one;

            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 10f;
            rowLe.preferredHeight = 10f;
            rowLe.flexibleHeight = 0f;

            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 0.5f;
            h.padding = new RectOffset(0, 0, 0, 0);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            h.childControlHeight = true;
            h.childControlWidth = true;

            var end = Math.Min(i + SlotsPerRow, names.Count);
            for (var j = i; j < end; j++)
                AddPresetSlot(row.transform, names[j], s => PresetSelected?.Invoke(s));
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_presetRoot);
        var p = _presetRoot.parent as RectTransform;
        while (p != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(p);
            p = p.parent as RectTransform;
        }
    }

    private static void AddPresetSlot(Transform row, string presetFileName, Action<string> onPick)
    {
        var go = new GameObject($"Preset_{presetFileName}", typeof(RectTransform));
        go.transform.SetParent(row, false);

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.minWidth = 14f;
        le.preferredWidth = 22f;
        le.minHeight = 9f;
        le.preferredHeight = 9f;

        var img = go.AddComponent<Image>();
        img.sprite = BeatSaberMarkupLanguage.Utilities.ImageResources.BlankSprite;
        img.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var capture = presetFileName;
        btn.onClick.AddListener(() =>
        {
            MultiplayerChat.Plugin.Log?.Debug($"[MPChat][AvatarLoad] Selected preset: {capture}");
            onPick(capture);
        });

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(3f, 1f);
        txtRt.offsetMax = new Vector2(-3f, -1f);
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 2.8f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.text = presetFileName;
        tmp.raycastTarget = false;
    }

    [UIAction("CloseClicked")]
    private void CloseClicked() => Closed?.Invoke();
}
