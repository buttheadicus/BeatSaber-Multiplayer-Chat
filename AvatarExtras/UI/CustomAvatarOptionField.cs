using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using BeatSaber.BeatAvatarAdapter.AvatarEditor;

namespace MultiplayerChat.AvatarExtras.UI;

/// <summary>
/// Clone the vanilla hands row under <c>EditPanel</c>.
/// </summary>
public class CustomAvatarOptionField : MonoBehaviour
{
    private const float HorizontalOffset = -.02f;
    private const float VerticalOffset = -.14f;

    public static CustomAvatarOptionField? Create(Transform editPanel, Transform templateRow, string name, int offsetPosition)
    {
        if (editPanel == null || templateRow == null)
            return null;

        var cloneGo = Object.Instantiate(templateRow.gameObject, editPanel, false);
        cloneGo.name = name;

        if (cloneGo.transform is RectTransform rt)
            rt.position += new Vector3(HorizontalOffset * offsetPosition, VerticalOffset * offsetPosition, 0);

        return cloneGo.AddComponent<CustomAvatarOptionField>();
    }

    public Image? Icon { get; private set; }
    public NamedIntListController? ValueController { get; private set; }
    public ColorPickerButtonController? PrimaryColorController { get; private set; }

    private void Awake()
    {
        ValueController = GetComponentInChildren<NamedIntListController>(true);
        PrimaryColorController = GetComponentInChildren<ColorPickerButtonController>(true);

        Transform? iconTf = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
            {
                iconTf = t;
                break;
            }
        }

        Icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
    }
}
