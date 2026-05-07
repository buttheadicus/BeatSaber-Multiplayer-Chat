using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.CustomAvatarsSettingsView.bsml")]
public class CustomAvatarsSettingsViewController : BSMLAutomaticViewController
{
    public event Action? CustomAvatarsSettingsApplied;

    [UIComponent("EnableLobbyAvatarsToggle")] private ToggleSetting? _enableToggle;

    [UIComponent("AvatarDropdown")] private DropDownListSetting? _avatarDropdown;

    private readonly List<object> _avatarOptionObjects = new();

    private bool _enableDraft;

    private const string LabelEnableToggle =
        "Enable Custom Avatars (requires game restart)";

    [UIValue("AvatarOptions")]
    public IList AvatarOptions => _avatarOptionObjects;

    [UIValue("EnableLobbyAvatarsDraft")]
    public bool EnableLobbyAvatarsDraft
    {
        get => _enableDraft;
        set => _enableDraft = value;
    }

    private void ReloadDraftFromDisk()
    {
        _enableDraft = ModSettings.EnableLobbyCustomAvatars;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ReloadDraftFromDisk();
        BuildAvatarDropdown(selectSaved: true);
        if (_enableToggle != null)
            _enableToggle.Text = LabelEnableToggle;
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ReloadDraftFromDisk();
        BuildAvatarDropdown(selectSaved: true);
        _enableToggle?.ReceiveValue();
        if (_enableToggle != null)
            _enableToggle.Text = LabelEnableToggle;
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    private void BuildAvatarDropdown(bool selectSaved)
    {
        if (_avatarDropdown == null)
            return;

        _avatarOptionObjects.Clear();
        _avatarOptionObjects.Add(CustomAvatarInstallListing.NoneLabel);

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
        object pick = CustomAvatarInstallListing.NoneLabel;
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

        _avatarDropdown.Value = pick;
        _avatarDropdown.ReceiveValue();
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_enableToggle?.Toggle != null)
            _enableDraft = _enableToggle.Toggle.isOn;

        ModSettings.EnableLobbyCustomAvatars = _enableDraft;

        var sel = _avatarDropdown?.Value?.ToString() ?? CustomAvatarInstallListing.NoneLabel;
        if (string.IsNullOrEmpty(sel) ||
            string.Equals(sel, CustomAvatarInstallListing.NoneLabel, StringComparison.Ordinal))
        {
            ModSettings.LobbyCustomAvatarRelativePath = "";
            ModSettings.LobbyCustomAvatarContentHash = "";
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

        CustomAvatarsSettingsApplied?.Invoke();
    }
}
