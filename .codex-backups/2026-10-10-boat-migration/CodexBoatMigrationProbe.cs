using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CodexBoatMigrationProbe
{
    private const string Output = "D:/课程/大三上/虚拟现实/craft/CraftFight/.codex-backups/2026-10-10-boat-migration";
    static CodexBoatMigrationProbe()
    {
        if (!Application.isBatchMode) EditorApplication.update += Tick;
    }
    private static bool running;
    private static void Tick() { if (!running && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(Output + "/probe-request")) { running=true; try { Run(); } finally { running=false; } } }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var probeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(probeScene);
            var env = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Levels/Island/Environment Objects.prefab"));
            var terrain = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Levels/Island/TerrainGroup.prefab"));
            var report = new StringBuilder();
            foreach (Transform child in env.transform)
            {
                var rs = child.GetComponentsInChildren<Renderer>(true);
                var b = rs.Length > 0 ? rs[0].bounds : new Bounds(child.position, Vector3.zero);
                foreach (var r in rs) b.Encapsulate(r.bounds);
                report.AppendLine(child.name + " | pos=" + child.position.ToString("F2") + " | bounds=" + b.ToString("F2") + " | renderers=" + rs.Length);
                foreach (Transform c in child) { var crs=c.GetComponentsInChildren<Renderer>(true); var cb=crs.Length>0?crs[0].bounds:new Bounds(c.position,Vector3.zero); foreach(var cr in crs) cb.Encapsulate(cr.bounds); report.AppendLine("  " + c.name + " | " + c.position.ToString("F2") + " | bounds=" + cb.ToString("F2")); }
            }
            foreach (var t in terrain.GetComponentsInChildren<Terrain>()) report.AppendLine("TERRAIN " + t.name + " | " + t.transform.position + " | size=" + t.terrainData.size);
            File.WriteAllText(Output + "/source-layout.txt", report.ToString());
            var lighting = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Levels/Island/Lighting.prefab");
            if (lighting != null) PrefabUtility.InstantiatePrefab(lighting);
            foreach (var root in probeScene.GetRootGameObjects()) foreach (var m in root.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            if (UnityEngine.Object.FindObjectsOfType<Light>().Length == 0) { var l = new GameObject("Sun").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(50,-35,0); l.intensity=1.2f; }
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.7f,.75f); RenderSettings.fog=false;
            var cam = new GameObject("Probe Camera").AddComponent<Camera>(); cam.orthographic=true; cam.orthographicSize=270; cam.transform.position=new Vector3(0,600,0); cam.transform.rotation=Quaternion.Euler(90,0,0); cam.farClipPlane=1500; cam.backgroundColor=new Color(.08f,.17f,.2f); cam.clearFlags=CameraClearFlags.SolidColor;
            var rt = new RenderTexture(1600,1000,24); cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
            var tex = new Texture2D(1600,1000,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply(); File.WriteAllBytes(Output+"/source-overview.png",tex.EncodeToPNG());
            cam.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            EditorSceneManager.CloseScene(probeScene,true); UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
            File.Delete(Output+"/probe-request"); File.WriteAllText(Output+"/probe-complete","OK");
            Debug.Log("CODEX_BOAT_PROBE_OK"); if(Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText(Output+"/probe-error.txt",e.ToString()); Debug.LogException(e); if(Application.isBatchMode) EditorApplication.Exit(2); }
    }
}
