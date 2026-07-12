using System;
using HMUI;
using UnityEngine;
using VRUIControls;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonSettingsFlowInstantiator
{
    internal static FlowCoordinator? Create(DiContainer menu, AddonSettingsFlowBinding binding)
    {
        try
        {
            FlowCoordinator? nestedFlow = null;
            if (binding.NestedFlowType != null && binding.NestedViewType != null)
                nestedFlow = CreateSingle(menu, binding.AddonId, binding.NestedFlowType, binding.NestedViewType, null);

            return CreateSingle(menu, binding.AddonId, binding.FlowType, binding.ViewType, nestedFlow);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Settings flow instantiate failed for {binding.AddonId}: {ex.Message}");
            return null;
        }
    }

    private static FlowCoordinator CreateSingle(
        DiContainer menu,
        string addonId,
        Type flowType,
        Type viewType,
        FlowCoordinator? nestedFlow)
    {
        var view = InstantiateViewController(menu, addonId, viewType);

        var flowGo = new GameObject(flowType.Name);
        var fc = (FlowCoordinator)flowGo.AddComponent(flowType);

        if (nestedFlow == null)
            menu.Inject(fc, new object[] { view });
        else
            menu.Inject(fc, new object[] { view, nestedFlow });

        return fc;
    }

    private static ViewController InstantiateViewController(DiContainer menu, string addonId, Type viewType)
    {
        // mirrors BSML's BeatSaberUI.CreateViewController: UI layer, full-stretch rect,
        // canvas with the curved-UI shader channel, and a concrete VRGraphicRaycaster added
        // before the view controller so RequireComponent(BaseRaycaster) is satisfied.
        var go = new GameObject(viewType.Name)
        {
            layer = 5
        };

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var canvas = go.AddComponent<Canvas>();
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;

        menu.InstantiateComponent<VRGraphicRaycaster>(go);
        var view = (ViewController)menu.InstantiateComponent(viewType, go);

        go.SetActive(false);
        return view;
    }
}
