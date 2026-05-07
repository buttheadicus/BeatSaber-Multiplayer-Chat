using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using MultiplayerChat.AvatarExtras.Models;
using MultiplayerChat.AvatarExtras.Utils;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Tags.Settings;
using HMUI;
using IPA.Utilities;
using SiraUtil.Affinity;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.AvatarExtras.Patches.Menu;

public class EditAvatarColorViewControllerPatcher : IInitializable, IAffinity
{
    [Inject] private readonly BeatAvatarEditorViewController _editAvatarViewController = null!;
    [Inject] private readonly EditAvatarColorViewController _editColorViewController = null!;

    private ListSetting? _listSetting = null;
    private AvatarPart? _selectedEditPart = null;
    private SpecialColorOption? _currentOption = null;

    public void Initialize()
    {
        _editAvatarViewController.didRequestColorChangeEvent += HandleColorEditBegin;
        _editColorViewController.didChangeColorEvent += HandleColorEditChange;
        _editColorViewController.didFinishEvent += HandleColorEditFinish;

        _selectedEditPart = null;
        _currentOption = null;
    }

    private void HandleColorEditBegin(Action<Color> colorCallback, Color currentColor,
        AvatarPart editPart, int uvSegment)
    {
        _selectedEditPart = editPart;
    }

    private void HandleColorEditChange(Color value)
    {
        SyncListSettingValue(value);
    }

    private void HandleColorEditFinish(bool didChange)
    {
        _selectedEditPart = null;
    }

    [AffinityPatch(typeof(BeatAvatarEditorViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            var bottom = _editColorViewController.transform.Find("BottomPanel")
                         ?? _editColorViewController.transform;

            _listSetting = CreateListSetting(bottom, "Extra Options",
                SpecialColorOption.AllOptions.Cast<object>().ToList(), SpecialColorOption.Default, OnListSettingChange);

            _listSetting.transform.SetAsFirstSibling();

            TuneColorEditorLayout(_editColorViewController);
        }
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(EditAvatarColorViewController), nameof(EditAvatarColorViewController.SetColor))]
    public void PrefixSetColor(ref Color color)
    {
        SyncListSettingValue(color);
    }

    public Color GetSelectedColorValue() => _editColorViewController.color;

    private void SyncListSettingValue(Color color)
    {
        var associatedColorOption = SpecialColorOption.DetectOptionMagically(color);

        if (_listSetting is not null)
            _listSetting.Value = associatedColorOption;

        _currentOption = associatedColorOption;
    }

    private void OnListSettingChange(object newValue)
    {
        if (newValue is SpecialColorOption newOption)
        {
            var selectedColor = GetSelectedColorValue();
            var wasSpecial = _currentOption != SpecialColorOption.Default;
            var isSpecial = newOption != SpecialColorOption.Default;

            if (!wasSpecial && _selectedEditPart is not null)
                Plugin.AvatarExtrasConfig!.StoreBackupColor(selectedColor, _selectedEditPart.Value);

            var setColor = Color.black;

            if (!isSpecial && _selectedEditPart is not null)
            {
                var backupColor = Plugin.AvatarExtrasConfig!.GetBackupColor(_selectedEditPart.Value);

                if (backupColor is not null)
                    setColor = backupColor.Value;
            }

            if (newOption.MagicColor is not null)
                setColor = newOption.MagicColor.Value;

            _editColorViewController.SetColor(setColor);
            InvokeChangeColor(setColor);
            _currentOption = newOption;
        }
    }

    private ListSetting CreateListSetting(Transform parent, string label, List<object> options, object defaultValue,
        Action<object> onChangeAction)
    {
        var gameObject = (new ListSettingTag()).CreateObject(parent);

        var listSetting = gameObject.GetComponent<ListSetting>();
        listSetting.Values = options;
        listSetting.Value = defaultValue;
        listSetting.OnChange = new BSMLAction(this, onChangeAction.Method);

        var rectTransform = (gameObject.transform as RectTransform)!;
        rectTransform.offsetMin += new Vector2(25f, 0);
        rectTransform.offsetMax += new Vector2(-20f, 0);
        rectTransform.anchoredPosition += new Vector2(0f, 13f);

        gameObject.transform.Find("NameText")
            !.GetComponent<CurvedTextMeshPro>()
            .SetText(label);

        return listSetting;
    }

    private void TuneColorEditorLayout(EditAvatarColorViewController vc)
    {
        var bottom = vc.transform.Find("BottomPanel");
        if (bottom == null)
            return;

        for (var i = 0; i < bottom.childCount; i++)
        {
            var row = bottom.GetChild(i);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h == null)
                continue;
            if (row.GetComponentsInChildren<Button>(true).Length < 2)
                continue;
            h.spacing += 32f;
            var pad = h.padding;
            pad.left += 6;
            pad.right += 6;
            h.padding = pad;
        }
    }

    private void InvokeChangeColor(Color color) =>
        _editColorViewController.InvokeMethod<object, EditAvatarColorViewController>("ChangeColor",
            color);
}
