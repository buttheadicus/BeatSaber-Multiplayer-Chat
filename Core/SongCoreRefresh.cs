using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// written by grok

namespace MultiplayerChat.Core;

// SongCore Tick: R = RefreshSongs(false), Ctrl+R = RefreshSongs(true).
// true is a full refresh (rewrite existing maps + level packs). PlaylistManager
// listens to Loader.SongsLoadedEvent and refreshes playlists after that finishes.
internal static class SongCoreRefresh
{
    private static readonly BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly BindingFlags AllMethods =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static FieldInfo? _instanceField;
    private static PropertyInfo? _instanceProp;
    private static PropertyInfo? _areSongsLoading;
    private static MethodInfo? _refreshSongs;
    private static bool _resolved;
    private static bool _queued;
    private static RefreshWhenIdleHost? _host;

    internal static bool TryRefresh(out string detail)
    {
        detail = "";
        try
        {
            Resolve();
            if (_refreshSongs == null)
            {
                detail = "SongCore not found; press Ctrl+R in the menu";
                Plugin.Log?.Warn("[MPChat][Experimental] " + detail);
                return false;
            }

            var loader = GetInstance();
            if (loader == null)
            {
                detail = "SongCore loader not ready; press Ctrl+R in the menu";
                Plugin.Log?.Warn("[MPChat][Experimental] " + detail);
                return false;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, "GameCore", StringComparison.Ordinal))
            {
                detail = "SongCore will not refresh during a song; press Ctrl+R in the menu";
                Plugin.Log?.Warn("[MPChat][Experimental] " + detail);
                return false;
            }

            if (IsLoading())
            {
                QueueWhenIdle();
                detail = "SongCore is busy; Ctrl+R full refresh queued";
                Plugin.Log?.Info("[MPChat][Experimental] " + detail);
                return true;
            }

            InvokeCtrlR(loader);
            detail = "SongCore full refresh started (same as Ctrl+R)";
            return true;
        }
        catch (Exception ex)
        {
            detail = "SongCore refresh failed: " + ex.Message;
            Plugin.Log?.Warn("[MPChat][Experimental] " + detail);
            return false;
        }
    }

    private static void Resolve()
    {
        if (_resolved)
            return;
        _resolved = true;

        Type? loader = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (!string.Equals(name, "SongCore", StringComparison.Ordinal))
                continue;
            loader = asm.GetType("SongCore.Loader");
            break;
        }

        if (loader == null)
            return;

        _instanceField = loader.GetField("Instance", StaticFlags);
        _instanceProp = loader.GetProperty("Instance", StaticFlags);
        _areSongsLoading = loader.GetProperty("AreSongsLoading", StaticFlags);
        _refreshSongs = loader.GetMethod(
            "RefreshSongs",
            AllMethods,
            null,
            new[] { typeof(bool) },
            null) ?? loader.GetMethod("RefreshSongs", AllMethods);
    }

    private static object? GetInstance()
    {
        return _instanceProp?.GetValue(null) ?? _instanceField?.GetValue(null);
    }

    private static bool IsLoading()
    {
        if (_areSongsLoading == null)
            return false;
        return _areSongsLoading.GetValue(null) is true;
    }

    private static void InvokeCtrlR(object loader)
    {
        // true = Ctrl+R full refresh. false is R-only and skips songs already in the library.
        var args = _refreshSongs!.GetParameters().Length >= 1
            ? new object[] { true }
            : Array.Empty<object>();
        _refreshSongs.Invoke(loader, args);
        Plugin.Log?.Info("[MPChat][Experimental] SongCore.Loader.RefreshSongs(true) (Ctrl+R)");
    }

    private static void QueueWhenIdle()
    {
        if (_queued)
            return;
        _queued = true;

        if (_host == null)
        {
            var go = new GameObject("MPChatSongCoreRefresh");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<RefreshWhenIdleHost>();
        }

        _host.Begin();
    }

    private sealed class RefreshWhenIdleHost : MonoBehaviour
    {
        internal void Begin() => StartCoroutine(WaitThenRefresh());

        private IEnumerator WaitThenRefresh()
        {
            var waited = 0f;
            while (IsLoading() && waited < 45f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _queued = false;
            var loader = GetInstance();
            if (loader == null || _refreshSongs == null)
            {
                Plugin.Log?.Warn("[MPChat][Experimental] queued SongCore refresh skipped; loader gone");
                yield break;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, "GameCore", StringComparison.Ordinal))
            {
                Plugin.Log?.Warn("[MPChat][Experimental] queued SongCore refresh skipped; still in GameCore");
                yield break;
            }

            try
            {
                InvokeCtrlR(loader);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn("[MPChat][Experimental] queued SongCore refresh failed: " + ex.Message);
            }
        }
    }
}
