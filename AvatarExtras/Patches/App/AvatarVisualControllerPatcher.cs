using System;
using System.Collections.Generic;
using System.Reflection;
using BeatSaber.BeatAvatarSDK;
using IPA.Utilities;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.AvatarExtras.Models;
using MultiplayerChat.AvatarExtras.Networking;
using MultiplayerChat.AvatarExtras.Utils;
using SiraUtil.Affinity;
using UnityEngine;

namespace MultiplayerChat.AvatarExtras.Patches.App;

/// <summary>
/// Packed glasses/beard in <see cref="AvatarData.facialHairId"/>: vanilla <see cref="BeatAvatarVisualController.UpdateAvatarVisual"/>
/// assigns meshes before our postfix — facial <see cref="AvatarData.facialHairId"/> breaks <c>GetById</c>, so the stock
/// <c>UpdateAvatarColors</c> inside visual runs on the wrong mesh. Prefix syncs <c>glassesId</c>; postfix corrects meshes then
/// re-runs stock <c>UpdateAvatarColors</c> when meshes change (<see cref="AvatarPropertyBlockColorSetter"/> + MPB).
/// </summary>
public class AvatarVisualControllerPatcher : IAffinity
{
    private static readonly Dictionary<string, Material> OriginalMaterialsCache = new();

    private static readonly Dictionary<int, Mesh?> LastMeshByFilterId = new();

    private static readonly Dictionary<int, Material> NativePartSharedBaselineByRenderer = new();

    private static readonly Dictionary<int, Material> CustomFlatMaterialByRenderer = new();

    private static readonly Dictionary<int, Color> LastAccessoryFlatColorByRenderer = new();

    private static readonly List<Color> ScratchColors2 = new(2);

    private static readonly List<Color> ScratchColors3 = new(3);

    private static readonly MethodInfo? BeatAvatarUpdateAvatarColors = typeof(BeatAvatarVisualController).GetMethod(
        "UpdateAvatarColors",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Before vanilla assigns meshes: mirror packed ids so <c>GetById(avatarData.glassesId)</c> sees a real glasses id.
    /// </summary>
    [AffinityPatch(typeof(BeatAvatarVisualController), nameof(BeatAvatarVisualController.UpdateAvatarVisual))]
    [AffinityPrefix]
    public void PrefixUpdateAvatarVisual(AvatarData avatarData, BeatAvatarVisualController __instance)
    {
        var packed = PackedExtrasString.TryFromString(avatarData.facialHairId);
        if (packed != null)
            packed.Value.ApplyTo(avatarData);
    }

    [AffinityPatch(typeof(BeatAvatarVisualController), "UpdateAvatarColors")]
    [AffinityPrefix]
    public void PrefixMirrorPackedExtrasAndStockMaterials(BeatAvatarVisualController __instance)
    {
        var avatarData = __instance.GetField<AvatarData, BeatAvatarVisualController>("_avatarData");
        if (avatarData == null)
            return;

        var packed = PackedExtrasString.TryFromString(avatarData.facialHairId);
        if (packed != null)
            packed.Value.ApplyTo(avatarData);

        if (packed?.GlassesId is not null)
        {
            var g = GetPartMesh(__instance, "_glassesMeshFilter");
            EnsureNativeBaselineCached(g);
            EnsureStockSharedMaterial(g);
        }

        if (packed?.FacialHairId is not null)
        {
            var h = GetPartMesh(__instance, "_facialHairMeshFilter");
            EnsureNativeBaselineCached(h);
            EnsureStockSharedMaterial(h);
        }
    }

    [AffinityPatch(typeof(BeatAvatarVisualController), nameof(BeatAvatarVisualController.UpdateAvatarVisual))]
    [AffinityPostfix]
    public void PostfixUpdateAvatarVisual(AvatarData avatarData, BeatAvatarVisualController __instance)
    {
        var avatarExtras = PackedExtrasString.TryFromString(avatarData.facialHairId);

        var glassesMeshFilter = __instance.GetField<MeshFilter, BeatAvatarVisualController>("_glassesMeshFilter");
        var facialHairMeshFilter = __instance.GetField<MeshFilter, BeatAvatarVisualController>("_facialHairMeshFilter");
        var avatarPartsModel = __instance.GetField<AvatarPartsModel, BeatAvatarVisualController>("_avatarPartsModel");

        var repaintGlasses = false;
        var repaintFacial = false;

        if (avatarExtras?.GlassesId is not null)
        {
            var glassesMeshPart =
                avatarPartsModel.glassesCollection.GetById(avatarExtras.Value.GlassesId)
                ?? avatarPartsModel.glassesCollection.GetDefault();

            repaintGlasses = MaybeResetRendererStateAfterMeshSwap(glassesMeshFilter, glassesMeshPart.mesh);
            glassesMeshFilter.mesh = glassesMeshPart.mesh;
            glassesMeshFilter.gameObject.SetActive(true);
            CacheNativeSharedMaterialIfStock(glassesMeshFilter.GetComponent<MeshRenderer>());
        }
        else
        {
            LastMeshByFilterId.Remove(glassesMeshFilter.GetInstanceID());
            ForgetNativeBaselineForFilter(glassesMeshFilter);
            glassesMeshFilter.gameObject.SetActive(false);
        }

        if (avatarExtras?.FacialHairId is not null)
        {
            var facialHairMeshPart =
                avatarPartsModel.facialHairCollection.GetById(avatarExtras.Value.FacialHairId)
                ?? avatarPartsModel.facialHairCollection.GetDefault();

            repaintFacial = MaybeResetRendererStateAfterMeshSwap(facialHairMeshFilter, facialHairMeshPart.mesh);
            facialHairMeshFilter.mesh = facialHairMeshPart.mesh;
            facialHairMeshFilter.gameObject.SetActive(true);
            CacheNativeSharedMaterialIfStock(facialHairMeshFilter.GetComponent<MeshRenderer>());
        }
        else
        {
            LastMeshByFilterId.Remove(facialHairMeshFilter.GetInstanceID());
            ForgetNativeBaselineForFilter(facialHairMeshFilter);
            facialHairMeshFilter.gameObject.SetActive(false);
        }

        // Vanilla UpdateAvatarVisual ends with UpdateAvatarColors() before this postfix runs. Packed facialHairId made
        // vanilla pick the default beard mesh; we then swap meshes and cleared MPB. Re-run stock tinting with *light* applied.
        if ((repaintGlasses || repaintFacial) && avatarExtras != null)
            BeatAvatarUpdateAvatarColors?.Invoke(__instance, null);
    }

    [AffinityPatch(typeof(BeatAvatarVisualController), "UpdateAvatarColors")]
    [AffinityPostfix]
    public void PostfixUpdateAvatarColors(BeatAvatarVisualController __instance) =>
        ApplyColorsPatch(__instance, true, true);

    private void ApplyColorsPatch(BeatAvatarVisualController visualController, bool includeNativeParts,
        bool includeCustomParts)
    {
        var avatarData = visualController.GetField<AvatarData, BeatAvatarVisualController>("_avatarData");

        if (avatarData == null)
            return;

        if (includeNativeParts)
        {
            var headTopMesh = GetPartMesh(visualController, "_headTopMeshFilter");
            var leftHandMesh = GetPartMesh(visualController, "_leftHandsHairMeshFilter");
            var rightHandMesh = GetPartMesh(visualController, "_rightHandsHairMeshFilter");
            var bodyMesh = GetPartMesh(visualController, "_bodyMeshFilter");

            ScratchColors3.Clear();
            ScratchColors3.Add(avatarData.headTopPrimaryColor);
            ScratchColors3.Add(avatarData.headTopSecondaryColor);
            ApplyBasePartColor(headTopMesh, ScratchColors3);

            ScratchColors2.Clear();
            ScratchColors2.Add(avatarData.handsColor);
            ApplyBasePartColor(leftHandMesh, ScratchColors2);

            ScratchColors2.Clear();
            ScratchColors2.Add(avatarData.handsColor);
            ApplyBasePartColor(rightHandMesh, ScratchColors2);

            ScratchColors3.Clear();
            ScratchColors3.Add(avatarData.clothesPrimaryColor);
            ScratchColors3.Add(avatarData.clothesSecondaryColor);
            ScratchColors3.Add(avatarData.clothesDetailColor);
            ApplyBasePartColor(bodyMesh, ScratchColors3);
        }

        if (includeCustomParts)
        {
            var glassesSetter = visualController.GetField<AvatarPropertyBlockColorSetter, BeatAvatarVisualController>(
                "_glassesPropertyBlockColorSetter");
            var facialHairSetter = visualController.GetField<AvatarPropertyBlockColorSetter, BeatAvatarVisualController>(
                "_facialHairPropertyBlockColorSetter");

            var glassesMesh = GetPartMesh(visualController, "_glassesMeshFilter");
            var facialHairMesh = GetPartMesh(visualController, "_facialHairMeshFilter");

            var packedOptional = PackedExtrasString.TryFromString(avatarData.facialHairId);
            ApplyCustomPartColor(glassesMesh, glassesSetter, avatarData.glassesColor,
                forceFlatOverride: packedOptional?.GlassesId is not null);
            ApplyCustomPartColor(facialHairMesh, facialHairSetter, avatarData.facialHairColor,
                forceFlatOverride: packedOptional?.FacialHairId is not null);
        }
    }

    private static void CacheNativeSharedMaterialIfStock(MeshRenderer? r)
    {
        if (r == null)
            return;

        var sm = r.sharedMaterial;
        if (sm == null || sm.name.StartsWith(MaterialFactory.MaterialNamePrefix, StringComparison.Ordinal))
            return;

        NativePartSharedBaselineByRenderer[r.GetInstanceID()] = sm;
    }

    private static void EnsureNativeBaselineCached(MeshRenderer r)
    {
        var id = r.GetInstanceID();
        if (NativePartSharedBaselineByRenderer.ContainsKey(id))
            return;
        var sm = r.sharedMaterial;
        if (sm != null && !sm.name.StartsWith(MaterialFactory.MaterialNamePrefix, StringComparison.Ordinal))
            NativePartSharedBaselineByRenderer[id] = sm;
    }

    private static void ForgetNativeBaselineForFilter(MeshFilter filter)
    {
        var r = filter.GetComponent<MeshRenderer>();
        if (r != null)
            NativePartSharedBaselineByRenderer.Remove(r.GetInstanceID());
    }

    /// <returns><see langword="true"/> if MPB was cleared (real mesh swap).</returns>
    private static bool MaybeResetRendererStateAfterMeshSwap(MeshFilter filter, Mesh newMesh)
    {
        var id = filter.GetInstanceID();
        if (LastMeshByFilterId.TryGetValue(id, out var prev) && ReferenceEquals(prev, newMesh))
            return false;

        LastMeshByFilterId[id] = newMesh;
        var r = filter.GetComponent<MeshRenderer>();
        if (r == null)
            return false;

        r.SetPropertyBlock(null);
        var rid = r.GetInstanceID();
        if (CustomFlatMaterialByRenderer.TryGetValue(rid, out var m) && m != null)
            UnityEngine.Object.Destroy(m);
        CustomFlatMaterialByRenderer.Remove(rid);
        LastAccessoryFlatColorByRenderer.Remove(rid);
        return true;
    }

    private static void EnsureStockSharedMaterial(MeshRenderer mesh)
    {
        var rid = mesh.GetInstanceID();
        if (!NativePartSharedBaselineByRenderer.TryGetValue(rid, out var baseline) || baseline == null)
            return;

        var cur = mesh.sharedMaterial;
        if (cur == null || cur.name.StartsWith(MaterialFactory.MaterialNamePrefix, StringComparison.Ordinal))
            mesh.sharedMaterial = baseline;
    }

    private static void ApplyBasePartColor(MeshRenderer mesh, List<Color> applicableColors)
    {
        if (!OriginalMaterialsCache.ContainsKey(mesh.name))
            OriginalMaterialsCache[mesh.name] = mesh.material;

        if (ApplySpecialOption(mesh, SpecialColorOption.DetectNonDefaultOptionMagically(applicableColors)))
            return;

        RestoreNativeMaterial(mesh);
    }

    /// <summary>
    /// Rainbow / flat fallback. With packed optional parts, third-party avatar shader replacers (e.g. Naluluna) can
    /// leave stock <see cref="AvatarPropertyBlockColorSetter"/> tints black — <paramref name="forceFlatOverride"/>
    /// swaps to a dedicated unlit (or safe fallback) material so wheel colors always show.
    /// </summary>
    private static void ApplyCustomPartColor(MeshRenderer mesh, AvatarPropertyBlockColorSetter? setter,
        Color targetColor, bool forceFlatOverride = false)
    {
        if (!mesh.gameObject.activeInHierarchy)
            return;

        CacheNativeSharedMaterialIfStock(mesh);

        if (ApplySpecialOption(mesh, SpecialColorOption.DetectNonDefaultOptionMagically(targetColor)))
        {
            mesh.SetPropertyBlock(null);
            return;
        }

        if (forceFlatOverride)
        {
            EnsureStockSharedMaterial(mesh);
            mesh.SetPropertyBlock(null);
            ApplyFlatColor(mesh, targetColor);
            return;
        }

        EnsureStockSharedMaterial(mesh);

        if (setter != null)
            return;

        Plugin.Log.Warn(
            "[AvatarExtras] No AvatarPropertyBlockColorSetter for glasses/facial hair; using flat tint fallback.");
        mesh.SetPropertyBlock(null);
        ApplyFlatColor(mesh, targetColor);
    }

    private static void RestoreNativeMaterial(MeshRenderer mesh)
    {
        if (OriginalMaterialsCache.TryGetValue(mesh.name, out var originalMat))
            if (mesh.material.name != originalMat.name)
                mesh.material = originalMat;
    }

    private static void ApplyFlatColor(MeshRenderer mesh, Color color)
    {
        var template = MaterialFactory.FlatColorMaterial;
        var rid = mesh.GetInstanceID();
        var newlyCreated = false;
        if (!CustomFlatMaterialByRenderer.TryGetValue(rid, out var inst) || inst == null ||
            inst.shader != template.shader)
        {
            if (inst != null)
                UnityEngine.Object.Destroy(inst);
            inst = new Material(template);
            CustomFlatMaterialByRenderer[rid] = inst;
            LastAccessoryFlatColorByRenderer.Remove(rid);
            newlyCreated = true;
        }

        mesh.SetPropertyBlock(null);
        mesh.sharedMaterial = inst;
        inst.renderQueue = MaterialFactory.AccessoryFlatRenderQueue;

        var skipTint = !newlyCreated &&
                       LastAccessoryFlatColorByRenderer.TryGetValue(rid, out var prev) &&
                       prev.ApproximatelyEquals(color);

        if (skipTint)
            return;

        if (newlyCreated)
            MaterialFactory.PrimeWhiteAlbedo(inst);
        MaterialFactory.ApplyAccessoryFlatTint(inst, color);
        LastAccessoryFlatColorByRenderer[rid] = color;
    }

    /// <summary>
    /// <see cref="AvatarPropertyBlockColorSetter.UpdateRenderer"/> reapplies Beat Avatar MPB after our tint; for packed
    /// flat materials that leaves the wrong block on the renderer. Reassert material colors after MPB runs.
    /// </summary>
    [AffinityPatch(typeof(AvatarPropertyBlockColorSetter), "UpdateRenderer")]
    [AffinityPostfix]
    public void PostfixAvatarPropertyBlockColorSetterUpdateRenderer(AvatarPropertyBlockColorSetter __instance)
    {
        if (__instance.GetField<Renderer, AvatarPropertyBlockColorSetter>("_renderer") is not MeshRenderer meshRenderer)
            return;

        if (!meshRenderer.gameObject.activeInHierarchy)
            return;

        var vc = meshRenderer.GetComponentInParent<BeatAvatarVisualController>();
        if (vc == null)
            return;

        var avatarData = vc.GetField<AvatarData, BeatAvatarVisualController>("_avatarData");
        if (avatarData == null)
            return;

        var packedOptional = PackedExtrasString.TryFromString(avatarData.facialHairId);
        if (packedOptional == null)
            return;

        if (packedOptional.Value.GlassesId is not null &&
            meshRenderer == GetPartMesh(vc, "_glassesMeshFilter"))
        {
            if (avatarData.glassesColor.ApproximatelyEquals(Magic.MagicRainbowColor))
                return;
            ApplyFlatColor(meshRenderer, avatarData.glassesColor);
            return;
        }

        if (packedOptional.Value.FacialHairId is not null &&
            meshRenderer == GetPartMesh(vc, "_facialHairMeshFilter"))
        {
            if (avatarData.facialHairColor.ApproximatelyEquals(Magic.MagicRainbowColor))
                return;
            ApplyFlatColor(meshRenderer, avatarData.facialHairColor);
        }
    }

    private static bool ApplySpecialOption(MeshRenderer mesh, SpecialColorOption? option)
    {
        if (option != SpecialColorOption.Rainbow)
            return false;

        if (mesh.material.name != MaterialFactory.RainbowMaterial.name)
            mesh.material = MaterialFactory.RainbowMaterial;

        return true;
    }

    private static MeshRenderer GetPartMesh(BeatAvatarVisualController controller, string fieldName) =>
        controller.GetField<MeshFilter, BeatAvatarVisualController>(fieldName).GetComponent<MeshRenderer>();
}
