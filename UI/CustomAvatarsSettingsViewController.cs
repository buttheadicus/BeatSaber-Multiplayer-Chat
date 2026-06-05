using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.CustomAvatarsSettingsView.bsml")]
public class CustomAvatarsSettingsViewController : BSMLAutomaticViewController
{
    public event Action? CustomAvatarsSettingsApplied;

    [UIComponent("EnableLobbyAvatarsToggle")] private ToggleSetting? _enableToggle;

    [UIComponent("AvatarDropdown")] private DropDownListSetting? _avatarDropdown;

    [UIComponent("CalibrateHeightButton")] private Button? _calibrateHeightButton;

    [UIComponent("EnabledSection")] private RectTransform? _enabledSection;

    [UIComponent("DescriptionText")] private TextMeshProUGUI? _descriptionText;

    private readonly List<object> _avatarOptionObjects = new();

    private bool _enableDraft;

    private CanvasGroup? _enabledSectionCanvasGroup;

    private bool _suppressAvatarDraftUpdate;

    private const string LabelEnableToggle = "ENABLE ADDON (RESTART REQUIRED)";

    private const float DisabledSectionAlpha = 0.45f;

    [UIValue("AvatarOptions")]
    public IList AvatarOptions => _avatarOptionObjects;

    [UIValue("EnableLobbyAvatarsDraft")]
    public bool EnableLobbyAvatarsDraft
    {
        get => _enableDraft;
        set
        {
            if (_enableDraft == value)
                return;

            _enableDraft = value;
            RefreshEnabledSectionInteractable();
        }
    }

    [UIValue("SelectedAvatar")]
    public object? SelectedAvatar
    {
        get => _avatarDropdown?.Value;
        set
        {
            if (value == null || _suppressAvatarDraftUpdate)
                return;
        }
    }

    private void ReloadDraftFromDisk()
    {
        _enableDraft = ModSettings.EnableLobbyCustomAvatars;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ReloadDraftFromDisk();
        EnsureEnabledSectionCanvasGroup();
        BuildAvatarDropdown(selectSaved: true);
        ApplyToggleLabel();
        HookEnableToggleListener();
        RefreshEnabledSectionInteractable();
        StabilizeCustomAvatarsLayout();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ReloadDraftFromDisk();
        EnsureEnabledSectionCanvasGroup();
        BuildAvatarDropdown(selectSaved: true);
        _enableToggle?.ReceiveValue();
        ApplyToggleLabel();
        HookEnableToggleListener();
        RefreshEnabledSectionInteractable();
        StabilizeCustomAvatarsLayout();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    private const float CustomAvatarsDescriptionWidthPx = 400f;

    private void StabilizeCustomAvatarsLayout()
    {
        BsmlLayoutGroups.ConfigureVertical(BsmlUiRefs.FindChildGameObject(transform, "CustomAvatarsRoot"), 4f);
        BsmlLayoutGroups.ConfigureVertical(BsmlUiRefs.FindChildGameObject(transform, "EnableToggleGroup"), 2f);
        BsmlLayoutGroups.ConfigureVertical(BsmlUiRefs.FindChildGameObject(transform, "EnabledSection"), 6f);
        BsmlLayoutGroups.ConfigureVertical(BsmlUiRefs.FindChildGameObject(transform, "ControlsGroup"), 2f);
        BsmlLayoutGroups.ConfigureHorizontal(BsmlUiRefs.FindChildGameObject(transform, "ActionButtonsRow"), 3f);
        BsmlLayoutGroups.MirrorSettingRowLayoutFromReference(_avatarDropdown, _enableToggle);
        BsmlLayoutGroups.SetTextPreferredWidth(_descriptionText, CustomAvatarsDescriptionWidthPx);
    }

    private void EnsureEnabledSectionCanvasGroup()
    {
        if (_enabledSection == null || _enabledSectionCanvasGroup != null)
            return;

        _enabledSectionCanvasGroup = _enabledSection.gameObject.GetComponent<CanvasGroup>();
        if (_enabledSectionCanvasGroup == null)
            _enabledSectionCanvasGroup = _enabledSection.gameObject.AddComponent<CanvasGroup>();
    }

    private void HookEnableToggleListener()
    {
        if (_enableToggle?.Toggle == null)
            return;

        _enableToggle.Toggle.onValueChanged.RemoveListener(OnEnableToggleUnityChanged);
        _enableToggle.Toggle.onValueChanged.AddListener(OnEnableToggleUnityChanged);
    }

    private void OnEnableToggleUnityChanged(bool isOn) => EnableLobbyAvatarsDraft = isOn;

    private void ApplyToggleLabel()
    {
        if (_enableToggle != null)
            _enableToggle.Text = LabelEnableToggle;
    }

    private void RefreshEnabledSectionInteractable()
    {
        var enabled = _enableDraft;
        if (_enabledSectionCanvasGroup != null)
        {
            _enabledSectionCanvasGroup.alpha = enabled ? 1f : DisabledSectionAlpha;
            _enabledSectionCanvasGroup.interactable = enabled;
            _enabledSectionCanvasGroup.blocksRaycasts = enabled;
        }

        SetControlInteractable(_avatarDropdown?.gameObject, enabled);
        if (_calibrateHeightButton != null)
            _calibrateHeightButton.interactable = enabled;
    }

    private static void SetControlInteractable(GameObject? root, bool interactable)
    {
        if (root == null)
            return;

        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            selectable.interactable = interactable;
    }

    private void BuildAvatarDropdown(bool selectSaved)
    {
        if (_avatarDropdown == null)
            return;

        _suppressAvatarDraftUpdate = true;
        try
        {
            _avatarOptionObjects.Clear();
            _avatarOptionObjects.Add(CustomAvatarInstallListing.DefaultBeatSaberAvatarLabel);

            foreach (var fn in CustomAvatarInstallListing.ListRelativeAvatarFilenames())
                _avatarOptionObjects.Add(fn);

            _avatarDropdown.Values = _avatarOptionObjects;
            _avatarDropdown.UpdateChoices();

            if (!selectSaved)
            {
                _avatarDropdown.ReceiveValue();
                return;
            }

            var saved = ModSettings.LobbyCustomAvatarRelativePath.Trim().Replace('\\', '/');
            object pick = CustomAvatarInstallListing.DefaultBeatSaberAvatarLabel;
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var o in _avatarOptionObjects)
                {
                    var s = o?.ToString() ?? "";
                    if (o is not null && string.Equals(s, saved, StringComparison.OrdinalIgnoreCase))
                    {
                        pick = o;
                        break;
                    }
                }
            }
            else if (CustomAvatarInstallListing.IsVanillaDescriptorHash(ModSettings.LobbyCustomAvatarContentHash))
            {
                pick = CustomAvatarInstallListing.DefaultBeatSaberAvatarLabel;
            }

            _avatarDropdown.Value = pick;
            _avatarDropdown.ReceiveValue();
        }
        finally
        {
            _suppressAvatarDraftUpdate = false;
        }
    }

    [UIAction("CalibrateHeightClicked")]
    private void OnCalibrateHeightClicked()
    {
        if (!_enableDraft)
            return;

        MpCustomAvatarHeightCalibration.Run();
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_enableToggle?.Toggle != null)
            _enableDraft = _enableToggle.Toggle.isOn;

        ModSettings.EnableLobbyCustomAvatars = _enableDraft;

        var sel = _avatarDropdown?.Value?.ToString() ?? CustomAvatarInstallListing.DefaultBeatSaberAvatarLabel;
        ApplyAvatarSelection(sel);

        CustomAvatarsSettingsApplied?.Invoke();
    }

    private void ApplyAvatarSelection(string sel)
    {
        if (CustomAvatarInstallListing.IsDefaultBeatSaberAvatarLabel(sel))
        {
            ModSettings.LobbyCustomAvatarRelativePath = "";
            ModSettings.LobbyCustomAvatarContentHash = CustomAvatarInstallListing.VanillaDescriptorHash;
        }
        else
        {
            ModSettings.LobbyCustomAvatarRelativePath = sel.Replace('\\', '/');
            var full = Path.Combine(BeatSaberPaths.CustomAvatarsDirectory,
                ModSettings.LobbyCustomAvatarRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                ModSettings.LobbyCustomAvatarContentHash = CustomAvatarHashUtil.Md5HexFile(full);
            else
                ModSettings.LobbyCustomAvatarContentHash = "";
        }

        CustomAvatarLobbyHashCache.Invalidate();
        MpCustomAvatarSyncManager.InvalidateOutboundDedupe();
        MpCustomAvatarSyncManager.BroadcastMetadataNow();
        MpChatLobbyCustomAvatarDriver.NotifyLocalAvatarSettingsChanged();
    }
}
