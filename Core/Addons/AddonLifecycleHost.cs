using MultiplayerChat.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonLifecycleHost : MonoBehaviour
{
    private static AddonLifecycleHost? _instance;

    internal static void EnsureRunning()
    {
        if (_instance != null)
            return;

        var go = new GameObject("MPChatAddonLifecycleHost");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AddonLifecycleHost>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        AddonHost.EnsureInstance();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void Update()
    {
        if (!MpChatDebugMode.IsEnabled || !Input.GetKeyDown(KeyCode.J))
            return;

        AddonReloadService.ReloadAddonsAndUiBindings("J key");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) =>
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene) =>
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
}
