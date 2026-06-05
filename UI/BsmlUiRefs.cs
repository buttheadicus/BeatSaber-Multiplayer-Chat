using UnityEngine;

namespace MultiplayerChat.UI;

internal static class BsmlUiRefs
{
    internal static GameObject? FindChildGameObject(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        var direct = root.Find(childName);
        if (direct != null)
            return direct.gameObject;

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindChildGameObject(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    internal static void SetActive(GameObject? go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }
}
