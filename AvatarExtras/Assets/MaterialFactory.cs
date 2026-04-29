using System;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.AvatarExtras.Assets;

public static class MaterialFactory
{
    public const string MaterialNamePrefix = "MpChatAvatarExtras_";

    /// <summary>
    /// Slightly above default geometry (~2000) so glasses/facial hair draw after face/eye meshes and reduce z-order bleed-through.
    /// </summary>
    public const int AccessoryFlatRenderQueue = 2460;

    private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdTintColor = Shader.PropertyToID("_TintColor");
    private static readonly int IdMainColor = Shader.PropertyToID("_MainColor");
    private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
    private static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");

    private static Material? _flatColorMaterial;

    private static Shader? ResolveFlatShader()
    {
        // Prefer legacy Unlit/Color: single _Color * _MainTex, works reliably for flat tint in URP projects.
        string[] candidates =
        {
            "Unlit/Color",
            "Unlit/Transparent",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Custom/SimpleLit",
            "Sprites/Default",
            "UI/Default",
            "Standard",
            "Diffuse",
        };

        foreach (var name in candidates)
        {
            var s = Shader.Find(name);
            if (s != null)
                return s;
        }

        return null;
    }

    public static Material FlatColorMaterial
    {
        get
        {
            if (_flatColorMaterial != null)
                return _flatColorMaterial;

            var shader = ResolveFlatShader();
            if (shader == null)
            {
                var canvasMat = Canvas.GetDefaultCanvasMaterial();
                shader = canvasMat != null ? canvasMat.shader : null;
            }

            if (shader == null)
            {
                Plugin.Log.Error("[AvatarExtras] No shader for flat avatar color material.");
                var fallback = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                if (fallback == null)
                    throw new InvalidOperationException("AvatarExtras: could not resolve any flat shader.");
                _flatColorMaterial = new Material(fallback)
                {
                    name = $"{MaterialNamePrefix}FlatColorMaterial",
                    color = Color.white
                };
                PrimeWhiteAlbedo(_flatColorMaterial);
                return _flatColorMaterial;
            }

            _flatColorMaterial = new Material(shader)
            {
                name = $"{MaterialNamePrefix}FlatColorMaterial",
                mainTexture = Texture2D.whiteTexture,
                color = Color.white
            };
            PrimeWhiteAlbedo(_flatColorMaterial);
            return _flatColorMaterial;
        }
    }

    /// <summary>URP/Built-in need a white albedo or some accessory meshes read as black.</summary>
    public static void PrimeWhiteAlbedo(Material mat)
    {
        if (mat.HasProperty(IdBaseMap))
            mat.SetTexture(IdBaseMap, Texture2D.whiteTexture);
        if (mat.HasProperty(IdMainTex))
            mat.SetTexture(IdMainTex, Texture2D.whiteTexture);
    }

    public static void ApplyTint(Material mat, Color color)
    {
        if (mat.HasProperty(IdBaseColor))
            mat.SetColor(IdBaseColor, color);
        if (mat.HasProperty(IdColor))
            mat.SetColor(IdColor, color);
        if (mat.HasProperty(IdTintColor))
            mat.SetColor(IdTintColor, color);
        if (mat.HasProperty(IdMainColor))
            mat.SetColor(IdMainColor, color);
        if (mat.HasProperty(IdEmissionColor))
            mat.SetColor(IdEmissionColor, color);
    }

    public static void ApplyAccessoryFlatTint(Material mat, Color color) => ApplyTint(mat, color);

    private static Material? _rainbowMaterial;

    public static Material RainbowMaterial
    {
        get
        {
            if (_rainbowMaterial != null)
                return _rainbowMaterial;

            _rainbowMaterial = BundleLoader.GetMaterial("RainbowSource");

            if (_rainbowMaterial != null)
            {
                _rainbowMaterial.name = $"{MaterialNamePrefix}RainbowMaterial";
                return _rainbowMaterial;
            }

            return FlatColorMaterial;
        }
    }
}
