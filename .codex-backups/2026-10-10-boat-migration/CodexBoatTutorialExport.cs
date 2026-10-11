using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CodexBoatTutorialExport
{
    const string Work = "D:/课程/大三上/虚拟现实/craft/CraftFight/.codex-backups/2026-10-10-boat-migration";
    const string Root = "Assets/BoatAttackTutorial";
    static bool busy;
    [Serializable] public class Config { public float xMin=-115, xMax=130, zMin=-180, zMax=-35; public float spawnX=-50, spawnZ=-88; }
    static CodexBoatTutorialExport() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if(busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Work+"/export-request")) return;
        busy=true; File.Delete(Work+"/export-request");
        try { Run(); } catch(Exception e) { File.WriteAllText(Work+"/export-error.txt",e.ToString()); Debug.LogException(e); } finally {busy=false;}
    }
    static void Folder(string path) { Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
    public static void Run()
    {
        var config=File.Exists(Work+"/crop-config.json")?JsonUtility.FromJson<Config>(File.ReadAllText(Work+"/crop-config.json")):new Config();
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Folder(Root+"/Scenes"); Folder(Root+"/Generated/Materials"); Folder(Root+"/Generated/Terrain");
            UnityEngine.Random.InitState(20261010);
            var env=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Levels/Island/Environment Objects.prefab"));
            PrefabUtility.UnpackPrefabInstance(env,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            env.name="BoatAttack Beach Environment";
            var keepGroups=new HashSet<string>{"Buildings","MainIsland","Mountains","Pillar3","Pillar","Pillar2"};
            foreach(Transform t in env.transform.Cast<Transform>().ToArray()) if(!keepGroups.Contains(t.name)) UnityEngine.Object.DestroyImmediate(t.gameObject);
            var buildings=env.transform.Find("Buildings");
            if(buildings!=null) foreach(Transform t in buildings.Cast<Transform>().ToArray()) if(t.name!="Town01" && t.name!="FisherBay") UnityEngine.Object.DestroyImmediate(t.gameObject);
            var crop=new Bounds(new Vector3((config.xMin+config.xMax)/2,0,(config.zMin+config.zMax)/2),new Vector3(config.xMax-config.xMin,1000,config.zMax-config.zMin));
            foreach(var mf in env.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr=mf.GetComponent<MeshRenderer>(); if(mr==null) continue;
                var b=mr.bounds;
                if(!crop.Intersects(b) || b.center.z>config.zMax+8 || b.center.z<config.zMin-15 || b.center.x<config.xMin-12 || b.center.x>config.xMax+12)
                { UnityEngine.Object.DestroyImmediate(mr); UnityEngine.Object.DestroyImmediate(mf); foreach(var col in mf==null?new Collider[0]:mf.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(col); }
            }
            foreach(var component in env.GetComponentsInChildren<MonoBehaviour>(true)) if(component!=null) UnityEngine.Object.DestroyImmediate(component);
            foreach(var ps in env.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps.gameObject);
            foreach(var a in env.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);
            foreach(var t in env.GetComponentsInChildren<Transform>(true)) {t.gameObject.layer=0; t.gameObject.tag="Untagged";}
            var materials=new Dictionary<Material,Material>(); int mi=0;
            foreach(var r in env.GetComponentsInChildren<Renderer>(true))
            {
                var slots=r.sharedMaterials;
                for(int j=0;j<slots.Length;j++)
                {
                    var source=slots[j]; if(source==null) continue;
                    if(!materials.TryGetValue(source,out var baked))
                    {
                        baked=new Material(source); baked.name=source.name.Replace("(Instance)","").Trim();
                        var safe=new string(baked.name.Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch).ToArray());
                        AssetDatabase.CreateAsset(baked,Root+"/Generated/Materials/"+(mi++).ToString("D3")+"_"+safe+".mat"); materials[source]=baked;
                    }
                    slots[j]=baked;
                }
                r.sharedMaterials=slots; r.SetPropertyBlock(null); r.lightmapIndex=-1;
            }
            foreach(var col in env.GetComponentsInChildren<Collider>(true)) { if(col.GetComponent<MeshFilter>()==null) UnityEngine.Object.DestroyImmediate(col); }
            int colliders=0;
            foreach(var mf in env.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mf.sharedMesh==null || !mf.gameObject.activeInHierarchy) continue;
                string n=mf.name.ToLowerInvariant();
                if(n.Contains("bush") || n.Contains("leaf") || n.Contains("grass") || n.Contains("palm") || n.Contains("tree") || n.Contains("vegetation")) continue;
                var mc=mf.GetComponent<MeshCollider>(); if(mc==null) mc=mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh=mf.sharedMesh; mc.convex=false; mc.isTrigger=false; colliders++;
            }
            foreach(var t in env.GetComponentsInChildren<Transform>(true).Reverse()) if(t!=env.transform && t.childCount==0 && t.GetComponents<Component>().Length==1) UnityEngine.Object.DestroyImmediate(t.gameObject);
            var originalTerrain=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Levels/Island/TerrainGroup.prefab"));
            var sources=originalTerrain.GetComponentsInChildren<Terrain>();
            var data=new TerrainData {heightmapResolution=513,alphamapResolution=256,baseMapResolution=512,size=new Vector3(config.xMax-config.xMin,100,config.zMax-config.zMin)};
            var heights=new float[513,513];
            for(int z=0;z<513;z++) for(int x=0;x<513;x++)
            {
                float wx=Mathf.Lerp(config.xMin,config.xMax,x/512f), wz=Mathf.Lerp(config.zMin,config.zMax,z/512f);
                var src=sources.First(t=>wx>=t.transform.position.x-.01f && wx<=t.transform.position.x+t.terrainData.size.x+.01f);
                float u=(wx-src.transform.position.x)/src.terrainData.size.x,v=(wz-src.transform.position.z)/src.terrainData.size.z;
                heights[z,x]=(src.terrainData.GetInterpolatedHeight(u,v)+src.transform.position.y+50)/100;
            }
            data.SetHeights(0,0,heights);
            var layers=sources.SelectMany(t=>t.terrainData.terrainLayers).Distinct().ToArray(); data.terrainLayers=layers;
            var sourceMaps=sources.Select(t=>t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapWidth,t.terrainData.alphamapHeight)).ToArray();
            var alpha=new float[256,256,layers.Length];
            for(int z=0;z<256;z++) for(int x=0;x<256;x++)
            {
                float wx=Mathf.Lerp(config.xMin,config.xMax,x/255f),wz=Mathf.Lerp(config.zMin,config.zMax,z/255f);
                int si=Array.FindIndex(sources,t=>wx>=t.transform.position.x-.01f && wx<=t.transform.position.x+t.terrainData.size.x+.01f);
                var src=sources[si]; var map=sourceMaps[si];
                int ax=Mathf.Clamp(Mathf.RoundToInt((wx-src.transform.position.x)/src.terrainData.size.x*(map.GetLength(1)-1)),0,map.GetLength(1)-1);
                int az=Mathf.Clamp(Mathf.RoundToInt((wz-src.transform.position.z)/src.terrainData.size.z*(map.GetLength(0)-1)),0,map.GetLength(0)-1);
                for(int l=0;l<src.terrainData.terrainLayers.Length;l++) alpha[z,x,Array.IndexOf(layers,src.terrainData.terrainLayers[l])]=map[az,ax,l];
            }
            data.SetAlphamaps(0,0,alpha); data.name="BoatAttack_TutorialBeach_Terrain";
            AssetDatabase.CreateAsset(data,Root+"/Generated/Terrain/TutorialBeachTerrain.asset");
            var ground=Terrain.CreateTerrainGameObject(data); ground.name="Beach Terrain - Cropped"; ground.transform.position=new Vector3(config.xMin,-50,config.zMin);
            var terrain=ground.GetComponent<Terrain>(); terrain.materialTemplate=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            terrain.materialTemplate.name="TutorialBeach Terrain"; AssetDatabase.CreateAsset(terrain.materialTemplate,Root+"/Generated/Materials/Terrain.mat"); terrain.heightmapPixelError=4; terrain.basemapDistance=400;
            UnityEngine.Object.DestroyImmediate(originalTerrain);
            var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.15f; sun.color=new Color(1,.96f,.88f); sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(48,-35,0); RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.48f,.62f,.76f); RenderSettings.ambientEquatorColor=new Color(.4f,.44f,.43f); RenderSettings.ambientGroundColor=new Color(.22f,.2f,.17f); RenderSettings.fog=false;
            var sky=new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_AtmosphereThickness",.8f); sky.SetFloat("_Exposure",1.05f); sky.name="Tutorial Sky"; AssetDatabase.CreateAsset(sky,Root+"/Generated/Materials/Sky.mat"); RenderSettings.skybox=sky;
            var points=new GameObject("Tutorial Placement Points");
            var spawn=new GameObject("PlayerSpawn"); spawn.transform.SetParent(points.transform); spawn.transform.position=new Vector3(config.spawnX,terrain.SampleHeight(new Vector3(config.spawnX,0,config.spawnZ))-50+.1f,config.spawnZ); spawn.transform.rotation=Quaternion.Euler(0,100,0);
            var entry=new GameObject("BattleEntry - Placeholder"); entry.transform.SetParent(points.transform); entry.transform.position=spawn.transform.position+new Vector3(8,0,0);
            var camera=new GameObject("Preview Camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.transform.position=new Vector3(12,65,-5); camera.transform.LookAt(new Vector3(0,6,-100)); camera.nearClipPlane=.1f; camera.farClipPlane=700; camera.backgroundColor=new Color(.54f,.72f,.84f);
            camera.gameObject.AddComponent<AudioListener>();
            Physics.SyncTransforms();
            AssetDatabase.SaveAssets();
            var scenePath=Root+"/Scenes/Tutorial_BoatAttack.unity"; EditorSceneManager.SaveScene(scene,scenePath);
            var deps=AssetDatabase.GetDependencies(scenePath,true).Where(p=>p.StartsWith("Assets/") && !p.EndsWith(".cs")).OrderBy(p=>p).ToArray();
            File.WriteAllLines(Work+"/export-dependencies.txt",deps);
            File.WriteAllText(Work+"/export-summary.json","{\"scene\":\""+scenePath+"\",\"renderers\":"+env.GetComponentsInChildren<Renderer>(true).Length+",\"meshColliders\":"+colliders+",\"materials\":"+materials.Count+",\"dependencies\":"+deps.Length+",\"spawn\":\""+spawn.transform.position.ToString("F2")+"\",\"crop\":"+JsonUtility.ToJson(config)+"}");
            RenderPreview(camera,scene,Work+"/tutorial-source-preview.png");
            File.WriteAllText(Work+"/export-complete",DateTime.UtcNow.ToString("O")); Debug.Log("CODEX_BOAT_EXPORT_OK "+scenePath);
        }
        finally { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous); }
    }
    static void RenderPreview(Camera cam,Scene scene,string path)
    {
        cam.scene=scene;
        var rt=new RenderTexture(1500,900,24); cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
        var tex=new Texture2D(1500,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1500,900),0,0); tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG());
        cam.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
    }
}
