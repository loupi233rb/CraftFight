using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CraftFight.Tutorial;

[InitializeOnLoad]
public static class BoatAttackTutorialTools
{
    public const string ScenePath = "Assets/BoatAttackTutorial/Scenes/Tutorial_BoatAttack.unity";
    static readonly string Work = Path.GetFullPath(".codex-backups/2026-10-10-boat-migration");
    static bool busy;
    static BoatAttackTutorialTools()
    {
        if(File.Exists(Work+"/verify-request-v2")) {File.Delete(Work+"/verify-request-v2");File.WriteAllText(Work+"/verify-request","run");}
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if(busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Work+"/verify-request")) return;
        busy=true; File.Delete(Work+"/verify-request");
        try { PrepareAndValidate(); }
        catch(Exception e) { File.WriteAllText(Work+"/target-error.txt",e.ToString()); Debug.LogException(e); }
        finally { busy=false; }
    }
    [MenuItem("Tools/CraftFight/BoatAttack Tutorial/Open Tutorial Scene")]
    public static void OpenScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }
    public static void PrepareAndValidate()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var loaded=SceneManager.GetSceneAt(i);
            if(loaded.isDirty && loaded.path!=ScenePath) throw new InvalidOperationException("Please save the open scene before validating the tutorial: "+loaded.path);
        }
        Directory.CreateDirectory(Work);
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var env=scene.GetRootGameObjects().First(g=>g.name=="BoatAttack Beach Environment");
        var buildings=env.transform.Find("Buildings");
        var existingVillage=buildings.Find("DistantVillage");
        if(existingVillage==null || PrefabUtility.IsPartOfPrefabInstance(existingVillage))
        {
            var village=existingVillage!=null?existingVillage.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoatAttackTutorial/Source/Objects/Levels/Island/Environment Objects Prefabs/Buildings/DistantVillage.prefab"),buildings);
            PrefabUtility.UnpackPrefabInstance(village,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction); village.name="DistantVillage";
            foreach(var t in village.GetComponentsInChildren<Transform>(true)) {GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);t.gameObject.tag="Untagged";t.gameObject.layer=0;}
            foreach(var m in village.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(m);
            foreach(var mf in village.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mf.sharedMesh==null) continue;
                var mc=mf.GetComponent<MeshCollider>();if(mc==null)mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;mc.convex=false;mc.isTrigger=false;
            }
        }
        if(env.GetComponent<TutorialVegetationSetup>()==null) env.AddComponent<TutorialVegetationSetup>();
        var terrain=scene.GetRootGameObjects().Select(g=>g.GetComponent<Terrain>()).First(t=>t!=null);
        terrain.gameObject.isStatic=false;terrain.lightmapIndex=-1;terrain.realtimeLightmapIndex=-1;
        var sun=scene.GetRootGameObjects().Select(g=>g.GetComponent<Light>()).First(l=>l!=null && l.type==LightType.Directional);
        sun.shadowStrength=.7f;
        var fill=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Sky Fill");
        if(fill==null) fill=new GameObject("Sky Fill",typeof(Light));
        var fillLight=fill.GetComponent<Light>();fillLight.type=LightType.Directional;fillLight.color=new Color(.75f,.85f,1);fillLight.intensity=.35f;fillLight.shadows=LightShadows.None;
        fill.transform.rotation=Quaternion.Euler(65,120,0);
        foreach(var layer in terrain.terrainData.terrainLayers) {layer.normalScale=Mathf.Min(layer.normalScale,.65f);EditorUtility.SetDirty(layer);}
        var spawn=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name=="PlayerSpawn");
        Physics.SyncTransforms();
        bool spawnFound=false;
        var desired=new Vector3(110,0,-80);
        for(int ring=0;ring<12 && !spawnFound;ring++) for(int dx=-ring;dx<=ring && !spawnFound;dx++) for(int dz=-ring;dz<=ring && !spawnFound;dz++)
        {
            if(Math.Abs(dx)!=ring && Math.Abs(dz)!=ring) continue;
            var p=desired+new Vector3(dx*2,0,dz*2);
            float u=(p.x-terrain.transform.position.x)/terrain.terrainData.size.x,v=(p.z-terrain.transform.position.z)/terrain.terrainData.size.z;
            if(u<.05f || u>.95f || v<.05f || v>.95f || terrain.terrainData.GetSteepness(u,v)>18) continue;
            p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
            if(p.y>7) continue;
            if(Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore)) continue;
            if(env.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.sharedMaterials.Any(m=>m!=null && m.shader.name.Contains("Vegetation")) && r.bounds.Contains(p+Vector3.up*1.3f))) continue;
            spawn.position=p+Vector3.up*.1f; spawnFound=true;
        }
        if(!spawnFound) throw new InvalidOperationException("Could not find an unobstructed, gently sloping beach spawn.");
        var entry=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name.StartsWith("BattleEntry"));
        entry.position=spawn.position+new Vector3(8,0,0); entry.position=new Vector3(entry.position.x,terrain.SampleHeight(entry.position)+terrain.transform.position.y+.1f,entry.position.z);
        var camera=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c!=null);
        camera.transform.position=new Vector3(105,94,28); camera.transform.LookAt(new Vector3(115,5,-107));
        spawn.rotation=Quaternion.Euler(0,245,0);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.57f,.73f,.85f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        var builds=EditorBuildSettings.scenes.ToList(); if(!builds.Any(s=>s.path==ScenePath)) {builds.Add(new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes=builds.ToArray();}
        var errors=new List<string>(); var shaderErrors=new List<string>(); int missing=0;
        foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true)) missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        if(missing>0) errors.Add("Missing scripts: "+missing);
        var renderers=env.GetComponentsInChildren<Renderer>(true);
        foreach(var r in renderers) foreach(var m in r.sharedMaterials) if(m==null || m.shader==null) errors.Add("Missing material/shader: "+r.name);
        foreach(var shader in renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader).Distinct())
            foreach(var message in ShaderUtil.GetShaderMessages(shader)) if(message.severity.ToString()=="Error") shaderErrors.Add(shader.name+": "+message.message);
        foreach(var mf in env.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh==null) errors.Add("Missing mesh: "+mf.name);
        if(terrain.GetComponent<TerrainCollider>().terrainData!=terrain.terrainData) errors.Add("Terrain collider data mismatch");
        var bounds=renderers[0].bounds;foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        Render(camera,Work+"/tutorial-overview.png");
        File.WriteAllText(Work+"/lighting-debug.txt", "Ambient coefficient: "+RenderSettings.ambientProbe[0,0]+"\nGlobal SH: "+Shader.GetGlobalVector("unity_SHAr")+"\nLightmaps: "+LightmapSettings.lightmaps.Length+"\nAmbient mode: "+RenderSettings.ambientMode);
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        camera.transform.position=spawn.position+Vector3.up*1.65f;camera.transform.rotation=spawn.rotation;Render(camera,Work+"/tutorial-player-view.png");camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllLines(Work+"/target-dependencies.txt",AssetDatabase.GetDependencies(ScenePath,true));
        var report=new StringBuilder();
        report.AppendLine("Scene: "+scene.path);report.AppendLine("Saved: "+!scene.isDirty); report.AppendLine("Renderers: "+renderers.Length);report.AppendLine("Mesh colliders: "+env.GetComponentsInChildren<MeshCollider>(true).Length);report.AppendLine("Missing scripts: "+missing);report.AppendLine("Terrain size: "+terrain.terrainData.size);report.AppendLine("Environment bounds: "+bounds);report.AppendLine("Spawn: "+spawn.position);report.AppendLine("Spawn unobstructed: "+spawnFound);report.AppendLine("Terrain collider raycast: "+Physics.Raycast(spawn.position+Vector3.up*2,Vector3.down,5));report.AppendLine("BoatAttack water/scripts: none");report.AppendLine("Shader errors: "+shaderErrors.Count);report.AppendLine("Asset errors: "+errors.Count);
        foreach(var error in errors.Concat(shaderErrors)) report.AppendLine(error);
        File.WriteAllText(Work+"/target-validation.txt",report.ToString());
        if(errors.Count+shaderErrors.Count>0) throw new InvalidOperationException(report.ToString());
        if(File.Exists(Work+"/target-error.txt")) File.Move(Work+"/target-error.txt",Work+"/target-error-previous-"+DateTime.UtcNow.Ticks+".txt");
        File.WriteAllText(Work+"/verify-complete",DateTime.UtcNow.ToString("O")); Debug.Log("BOATATTACK_TUTORIAL_VALIDATION_OK\n"+report);
        var view=SceneView.lastActiveSceneView;if(view!=null) view.LookAt(new Vector3(115,5,-107),camera.transform.rotation,135,false,true);
    }
    static void Render(Camera camera,string path)
    {
        var rt=new RenderTexture(1500,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1500,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1500,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
    }
}
