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

public static class BoatAttackTutorialTools
{
    public const string ScenePath = "Assets/BoatAttackTutorial/Scenes/Tutorial_BoatAttack.unity";
    static readonly string Work = Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/BoatAttackTutorialValidation"));
    [MenuItem("Tools/CraftFight/BoatAttack Tutorial/Open Tutorial Scene")]
    public static void OpenScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }
    [MenuItem("Tools/CraftFight/BoatAttack Tutorial/Validate Current Tutorial Scene")]
    public static void ValidateCurrentScene()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath) {Debug.LogWarning("Open the BoatAttack tutorial scene before validating it.");return;}
        Directory.CreateDirectory(Work);
        var env=scene.GetRootGameObjects().First(g=>g.name=="BoatAttack Beach Environment");
        var terrain=scene.GetRootGameObjects().Select(g=>g.GetComponent<Terrain>()).First(t=>t!=null);
        var spawn=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name=="PlayerSpawn");
        Physics.SyncTransforms();
        bool spawnFound=!Physics.CheckCapsule(spawn.position+Vector3.up*.4f,spawn.position+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore);
        bool terrainHit=terrain.GetComponent<TerrainCollider>().Raycast(new Ray(spawn.position+Vector3.up*2,Vector3.down),out var hit,5);
        var camera=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c!=null);
        var errors=new List<string>(); var shaderErrors=new List<string>(); int missing=0;
        if(!spawnFound) errors.Add("Spawn capsule is obstructed");
        if(!terrainHit) errors.Add("No terrain beneath spawn");
        foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true)) missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        if(missing>0) errors.Add("Missing scripts: "+missing);
        var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
        var oceans=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TutorialOcean>(true)).ToArray();
        foreach(var ocean in oceans)
        {
            if(ocean.resources==null || ocean.surface==null || ocean.seafloorHeight==null || ocean.skyReflection==null) errors.Add("Missing ocean resources");
            if(ocean.resources!=null && (ocean.resources.defaultSeaMaterial==null || ocean.resources.defaultFoamMap==null || ocean.resources.defaultSurfaceMap==null || ocean.resources.defaultFoamRamp==null || ocean.resources.defaultWaterMeshes.Any(m=>m==null))) errors.Add("Incomplete BoatAttack water resources");
            if(spawn.position.y<ocean.SeaLevel+.75f) errors.Add("Spawn is too close to sea level");
        }
        foreach(var marker in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name=="PlayerSpawn" || t.name=="BattleEntry - Placeholder"))
        {
            if(Physics.CheckCapsule(marker.position+Vector3.up*.4f,marker.position+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore)) errors.Add("Placement obstructed: "+marker.name);
            if(Physics.Raycast(marker.position+Vector3.up*100,Vector3.down,out var overhead,102,~0,QueryTriggerInteraction.Ignore) && overhead.point.y>marker.position.y+.3f) errors.Add("Placement is inside or beneath scenery: "+marker.name);
        }
        foreach(var r in renderers) foreach(var m in r.sharedMaterials) if(m==null || m.shader==null) errors.Add("Missing material/shader: "+r.name);
        foreach(var shader in renderers.SelectMany(r=>r.sharedMaterials).Concat(new[]{terrain.materialTemplate}).Concat(oceans.Where(o=>o.resources!=null).Select(o=>o.resources.defaultSeaMaterial)).Where(m=>m!=null && m.shader!=null).Select(m=>m.shader).Distinct())
            foreach(var message in ShaderUtil.GetShaderMessages(shader)) if(message.severity.ToString()=="Error") shaderErrors.Add(shader.name+": "+message.message);
        foreach(var mf in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true))) if(mf.sharedMesh==null) errors.Add("Missing mesh: "+mf.name);
        if(terrain.GetComponent<TerrainCollider>().terrainData!=terrain.terrainData) errors.Add("Terrain collider data mismatch");
        var bounds=renderers[0].bounds;foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        Render(camera,Work+"/tutorial-overview.png");
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        try {camera.transform.position=spawn.position+Vector3.up*1.65f;camera.transform.rotation=spawn.rotation;Render(camera,Work+"/tutorial-player-view.png");}
        finally {camera.transform.SetPositionAndRotation(oldPosition,oldRotation);}
        File.WriteAllLines(Work+"/target-dependencies.txt",AssetDatabase.GetDependencies(ScenePath,true));
        var report=new StringBuilder();
        report.AppendLine("Scene: "+scene.path);report.AppendLine("Saved: "+!scene.isDirty); report.AppendLine("Renderers: "+renderers.Length);report.AppendLine("Mesh colliders: "+env.GetComponentsInChildren<MeshCollider>(true).Length);report.AppendLine("Missing scripts: "+missing);report.AppendLine("Terrain size: "+terrain.terrainData.size);report.AppendLine("Environment bounds: "+bounds);report.AppendLine("Spawn: "+spawn.position);report.AppendLine("Spawn unobstructed: "+spawnFound);report.AppendLine("Terrain collider raycast: "+terrainHit);report.AppendLine("Shader errors: "+shaderErrors.Count);report.AppendLine("Asset errors: "+errors.Count);
        foreach(var error in errors.Concat(shaderErrors)) report.AppendLine(error);
        File.WriteAllText(Work+"/target-validation.txt",report.ToString());
        if(errors.Count+shaderErrors.Count>0) throw new InvalidOperationException(report.ToString());
        if(File.Exists(Work+"/target-error.txt")) File.Move(Work+"/target-error.txt",Work+"/target-error-previous-"+DateTime.UtcNow.Ticks+".txt");
        File.WriteAllText(Work+"/verify-complete",DateTime.UtcNow.ToString("O")); Debug.Log("BOATATTACK_TUTORIAL_VALIDATION_OK\n"+report);
        var view=SceneView.lastActiveSceneView;if(view!=null) view.LookAt(new Vector3(115,5,-107),camera.transform.rotation,135,false,true);
    }
    public static void Render(Camera camera,string path)
    {
        var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        var rt=new RenderTexture(1500,900,24);var tex=new Texture2D(1500,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,1500,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
