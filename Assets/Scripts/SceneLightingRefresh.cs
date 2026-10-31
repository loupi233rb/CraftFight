using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Refreshes unbaked sky lighting once the new scene has initialized.</summary>
public sealed class SceneLightingRefresh : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // Avoid duplicate subscriptions when entering Play Mode without domain reload.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        var runner = new GameObject("Scene Lighting Refresh");
        SceneManager.MoveGameObjectToScene(runner, scene);
        runner.AddComponent<SceneLightingRefresh>();
    }

    private IEnumerator Start()
    {
        // Let scene Start methods and the previous scene's teardown finish first.
        yield return null;
        if (gameObject.scene == SceneManager.GetActiveScene() &&
            RenderSettings.ambientMode == AmbientMode.Skybox && RenderSettings.skybox != null &&
            LightmapSettings.lightmaps.Length == 0)
        {
            DynamicGI.UpdateEnvironment();
        }
        // This helper belongs to the loaded scene; nothing persists across scenes.
        Destroy(gameObject);
    }
}
