using BeatSaberMarkupLanguage.Components.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

internal static class BsmlLayoutGroups
{
    internal static void ConfigureVertical(GameObject? go, float spacing, bool middleCenter = true)
    {
        if (go == null || !go.TryGetComponent<VerticalLayoutGroup>(out var vlg))
            return;

        vlg.spacing = spacing;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = middleCenter ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
    }

    internal static void ConfigureHorizontal(GameObject? go, float spacing, bool middleCenter = true)
    {
        if (go == null || !go.TryGetComponent<HorizontalLayoutGroup>(out var hlg))
            return;

        hlg.spacing = spacing;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        if (middleCenter)
            hlg.childAlignment = TextAnchor.MiddleCenter;
    }

    internal static void SetTextPreferredWidth(TMP_Text? tmp, float widthPx)
    {
        if (tmp == null)
            return;

        tmp.enableWordWrapping = true;
        var le = tmp.GetComponent<LayoutElement>();
        if (le == null)
            le = tmp.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = widthPx;
        le.flexibleWidth = 0f;
    }

    internal static void CompactSliderSetting(SliderSetting? slider, float preferredHeight = 10f)
    {
        if (slider == null)
            return;

        var rootLe = slider.GetComponent<LayoutElement>();
        if (rootLe == null)
            rootLe = slider.gameObject.AddComponent<LayoutElement>();
        rootLe.preferredHeight = preferredHeight;
        rootLe.minHeight = preferredHeight;
        rootLe.flexibleHeight = 0f;

        var row = slider.GetComponent<HorizontalLayoutGroup>()
                    ?? slider.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (row != null)
        {
            row.childForceExpandHeight = false;
            row.childControlHeight = true;
        }

        foreach (var childSlider in slider.GetComponentsInChildren<Slider>(true))
        {
            var rt = childSlider.transform as RectTransform;
            if (rt == null)
                continue;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, preferredHeight);
        }

        foreach (var le in slider.GetComponentsInChildren<LayoutElement>(true))
        {
            if (le.gameObject == slider.gameObject)
                continue;
            if (le.preferredHeight <= 0f || le.preferredHeight > preferredHeight + 1f)
                le.preferredHeight = preferredHeight;
            le.flexibleHeight = 0f;
        }

        var parentVertical = slider.GetComponentInParent<VerticalLayoutGroup>();
        if (parentVertical != null)
            parentVertical.childForceExpandHeight = false;
    }

    internal static void MirrorSettingRowLayoutFromReference(Component? reference, Component? target)
    {
        if (reference == null || target == null)
            return;

        var refRow = reference.GetComponent<HorizontalLayoutGroup>() ??
                     reference.GetComponentInChildren<HorizontalLayoutGroup>(true);
        var tgtRow = target.GetComponent<HorizontalLayoutGroup>() ??
                     target.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (refRow == null || tgtRow == null)
            return;

        tgtRow.spacing = refRow.spacing;
        tgtRow.padding = refRow.padding;
        tgtRow.childAlignment = refRow.childAlignment;
        tgtRow.childControlWidth = refRow.childControlWidth;
        tgtRow.childControlHeight = refRow.childControlHeight;
        tgtRow.childForceExpandWidth = false;
        tgtRow.childForceExpandHeight = false;

        foreach (var le in target.GetComponentsInChildren<LayoutElement>(true))
            le.flexibleWidth = 0f;

        var row = target.GetComponent<LayoutElement>();
        if (row != null)
        {
            row.preferredWidth = -1f;
            row.minWidth = -1f;
            row.flexibleWidth = 0f;
        }
    }
}
