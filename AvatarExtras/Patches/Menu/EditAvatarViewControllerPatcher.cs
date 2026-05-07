using System;
using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.AvatarExtras.Networking;
using MultiplayerChat.AvatarExtras.UI;
using MultiplayerChat.AvatarExtras.Utils;
using IPA.Utilities;
using SiraUtil.Affinity;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

namespace MultiplayerChat.AvatarExtras.Patches.Menu;

public class EditAvatarViewControllerPatcher : IAffinity
{
    private const string EditPanelName = "EditPanel";
    private const string HandsRowName = "Hands";
    private const float ExtraVerticalListSpacing = 28f;

    [Inject] private readonly BeatAvatarEditorViewController _editAvatarViewController = null!;
    [Inject] private readonly AvatarPartsModel _avatarPartsModel = null!;
    [Inject] private readonly AvatarDataModel _avatarDataModel = null!;

    private PackedExtrasString _extras = new(null, null);
    private CustomAvatarOptionField? _glassesPicker;
    private CustomAvatarOptionField? _facialHairPicker;

    internal static Transform? FindChildRecursive(Transform root, string exactName)
    {
        if (root.name == exactName)
            return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), exactName);
            if (found != null)
                return found;
        }

        return null;
    }

    internal static Transform? ResolveEditPanel(BeatAvatarEditorViewController vc)
    {
        var root = vc.transform;
        var direct = root.Find(EditPanelName);
        if (direct != null)
            return direct;
        var deep = FindChildRecursive(root, EditPanelName);
        if (deep != null)
            return deep;

        var pick = vc.GetField<NamedIntListController, BeatAvatarEditorViewController>("_handsValuePicker")
                   ?? vc.GetField<NamedIntListController, BeatAvatarEditorViewController>("_headTopValuePicker");
        if (pick == null)
            return null;

        for (var t = pick.transform.parent; t != null; t = t.parent)
        {
            if (t.GetComponent<VerticalLayoutGroup>() != null)
                return t;
        }

        return null;
    }

    private static bool HandsPickerLiesUnder(Transform row, Transform handsPickerTransform)
    {
        for (var t = handsPickerTransform; t != null; t = t.parent)
        {
            if (ReferenceEquals(t, row))
                return true;
        }

        return false;
    }

    private static Transform? ResolveHandsTemplateRow(Transform editPanel,
        NamedIntListController handsPicker)
    {
        var byName = editPanel.Find(HandsRowName);
        if (byName != null)
            return byName;

        for (var i = 0; i < editPanel.childCount; i++)
        {
            var row = editPanel.GetChild(i);
            if (HandsPickerLiesUnder(row, handsPicker.transform))
                return row;
        }

        return null;
    }

    private static void PadExtraModelPickRows(Transform glassesRow, Transform facialHairRow)
    {
        const float extraMin = 14f;

        foreach (var row in new[] { glassesRow, facialHairRow })
        {
            var le = row.GetComponent<LayoutElement>();
            if (le == null)
                le = row.gameObject.AddComponent<LayoutElement>();

            var baseline = le.minHeight > 1f ? le.minHeight : 72f;
            le.minHeight = baseline + extraMin;
        }
    }

    private static void BumpVerticalListSpacing(Transform editPanelRoot, float extraSpacing)
    {
        if (extraSpacing <= 0f)
            return;

        var vlg = editPanelRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            for (var i = 0; i < editPanelRoot.childCount; i++)
            {
                vlg = editPanelRoot.GetChild(i).GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                    break;
            }
        }

        if (vlg != null)
            vlg.spacing += extraSpacing;
    }

    private CustomAvatarOptionField? CreateCustomField(Transform editPanel, Transform templateRow, string name,
        int layoutOffset, ref int siblingInsertIndex)
    {
        var field = CustomAvatarOptionField.Create(editPanel, templateRow, name, layoutOffset);
        if (field == null)
            return null;

        var idx = Mathf.Clamp(siblingInsertIndex, 0, editPanel.childCount - 1);
        field.transform.SetSiblingIndex(idx);
        siblingInsertIndex++;
        return field;
    }

    [AffinityPatch(typeof(BeatAvatarEditorViewController), "DidActivate")]
    [AffinityPostfix]
    public void PostfixDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (!firstActivation)
            return;

        var handsPicker =
            _editAvatarViewController.GetField<NamedIntListController, BeatAvatarEditorViewController>(
                "_handsValuePicker");
        var editPanel = ResolveEditPanel(_editAvatarViewController);
        var templateRow = handsPicker != null && editPanel != null
            ? ResolveHandsTemplateRow(editPanel, handsPicker)
            : null;

        if (editPanel == null || templateRow == null || handsPicker == null)
        {
            Plugin.Log.Error(
                "[AvatarExtras] Could not resolve EditPanel or hands template row (see BeatSaberAvatarExtras upstream layout).");
            return;
        }

        BumpVerticalListSpacing(editPanel, ExtraVerticalListSpacing);

        _extras = PackedExtrasString.FromEditorState(_avatarDataModel.avatarData);

        var insert = templateRow.GetSiblingIndex() + 1;
        _glassesPicker = CreateCustomField(editPanel, templateRow, "Glasses", 1, ref insert);
        _facialHairPicker = CreateCustomField(editPanel, templateRow, "FacialHair", 2, ref insert);

        if (_glassesPicker == null || _facialHairPicker == null)
        {
            Plugin.Log.Error("[AvatarExtras] Failed to instantiate glasses/facial hair rows.");
            return;
        }

        PadExtraModelPickRows(_glassesPicker.transform, _facialHairPicker.transform);

        if (_glassesPicker.Icon != null)
            _glassesPicker.Icon.sprite = Sprites.Glasses;
        if (_facialHairPicker.Icon != null)
            _facialHairPicker.Icon.sprite = Sprites.Mustache;

        if (editPanel is RectTransform edRt)
            edRt.position += new Vector3(0, .18f, 0);

        var randomPanel = _editAvatarViewController.transform.Find("RandomizePanel")
                          ?? FindChildRecursive(_editAvatarViewController.transform, "RandomizePanel");
        if (randomPanel != null)
            (randomPanel.transform as RectTransform)!.position += new Vector3(0, -.28f, 0);

        if (_glassesPicker.ValueController != null)
            InvokeSetupValuePicker<AvatarMeshPartSO>(
                _avatarPartsModel.glassesCollection,
                _glassesPicker.ValueController,
                delegate(string s)
                {
                    _extras.GlassesId = s;
                    SyncWireColorsFromAvatar();
                    _extras.ApplyTo(_avatarDataModel.avatarData);
                },
                AvatarPart.Unknown);

        if (_glassesPicker.PrimaryColorController != null)
            InvokeSetupColorButton(
                _glassesPicker.PrimaryColorController.button,
                delegate(Color color)
                {
                    _avatarDataModel.avatarData.glassesColor = color;
                    SyncWireColorsFromAvatar();
                    _extras.ApplyTo(_avatarDataModel.avatarData);
                },
                () => _avatarDataModel.avatarData.glassesColor,
                AvatarPart.GlassesColor);

        if (_facialHairPicker.ValueController != null)
            InvokeSetupValuePicker<AvatarMeshPartSO>(
                _avatarPartsModel.facialHairCollection,
                _facialHairPicker.ValueController,
                delegate(string s)
                {
                    _extras.FacialHairId = s;
                    SyncWireColorsFromAvatar();
                    _extras.ApplyTo(_avatarDataModel.avatarData);
                },
                AvatarPart.Unknown);

        if (_facialHairPicker.PrimaryColorController != null)
            InvokeSetupColorButton(
                _facialHairPicker.PrimaryColorController.button,
                delegate(Color color)
                {
                    _avatarDataModel.avatarData.facialHairColor = color;
                    SyncWireColorsFromAvatar();
                    _extras.ApplyTo(_avatarDataModel.avatarData);
                },
                () => _avatarDataModel.avatarData.facialHairColor,
                AvatarPart.FacialHairColor);

        InvokeRefreshUi();
        PackedExtrasString.SyncSeparateColorsFromPackedWire(_avatarDataModel.avatarData);
    }

    [AffinityPatch(typeof(AvatarRandomizer), nameof(AvatarRandomizer.RandomizeModels))]
    [AffinityPostfix]
    public void PostfixRandomizeModels(AvatarData avatarData, AvatarPartsModel avatarPartsModel)
    {
        _extras.GlassesId = CoinFlip() ? avatarPartsModel.glassesCollection.GetRandom().id : null;
        _extras.FacialHairId = CoinFlip() ? avatarPartsModel.facialHairCollection.GetRandom().id : null;
        _extras.WireGlassesColor = avatarData.glassesColor;
        _extras.WireFacialHairColor = avatarData.facialHairColor;
        _extras.ApplyTo(avatarData);
        PackedExtrasString.SyncSeparateColorsFromPackedWire(avatarData);
    }

    private static bool CoinFlip() => Random.Range(0, 2) == 0;

    [AffinityPatch(typeof(BeatAvatarEditorViewController), "RefreshUi")]
    [AffinityPostfix]
    public void PostfixRefreshUi()
    {
        _extras = PackedExtrasString.FromEditorState(_avatarDataModel.avatarData);

        if (_glassesPicker?.ValueController != null)
        {
            _glassesPicker.ValueController.SetValue(
                _avatarPartsModel.glassesCollection.GetIndexById(_extras.GlassesId));
            _glassesPicker.PrimaryColorController?.SetColor(_avatarDataModel.avatarData.glassesColor);
        }

        if (_facialHairPicker?.ValueController != null)
        {
            _facialHairPicker.ValueController.SetValue(
                _avatarPartsModel.facialHairCollection.GetIndexById(_extras.FacialHairId));
            _facialHairPicker.PrimaryColorController?.SetColor(_avatarDataModel.avatarData.facialHairColor);
        }

        PackedExtrasString.SyncSeparateColorsFromPackedWire(_avatarDataModel.avatarData);
    }

    private void SyncWireColorsFromAvatar()
    {
        _extras.WireGlassesColor = _avatarDataModel.avatarData.glassesColor;
        _extras.WireFacialHairColor = _avatarDataModel.avatarData.facialHairColor;
    }

    private void InvokeRefreshUi() =>
        _editAvatarViewController.InvokeMethod<object, BeatAvatarEditorViewController>("RefreshUi");

    private void InvokeSetupColorButton(Button button,
        Action<Color> colorSetter,
        Func<Color> currentColor,
        AvatarPart avatarPart,
        int uvSegment = 0) =>
        _editAvatarViewController.InvokeMethod<object, BeatAvatarEditorViewController>("SetupColorButton",
            button, colorSetter, currentColor, avatarPart, uvSegment);

    private void InvokeSetupValuePicker<T>(
        AvatarPartCollection<T> partCollection,
        NamedIntListController valuePicker,
        Action<string> setIdAction,
        AvatarPart avatarPart)
        where T : UnityEngine.Object, IAvatarPart =>
        _editAvatarViewController.InvokeGenericMethod<object, BeatAvatarEditorViewController, T>("SetupValuePicker",
            partCollection, valuePicker, setIdAction, avatarPart);
}
