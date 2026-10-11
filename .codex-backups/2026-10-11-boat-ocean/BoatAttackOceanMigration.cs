using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CraftFight.Tutorial;
using WaterSystem;
using WaterSystem.Data;

[InitializeOnLoad]
public static class BoatAttackOceanMigration
{
    const string Root = "Assets/BoatAttackTutorial";
    const string Work = "Logs/BoatAttackTutorialOcean";
    static bool busy;
    static double entered;
    static bool firstFrame;
    static Camera probe;
    static BoatAttackOceanMigration() { EditorApplication.update += Update; EditorApplication.playModeStateChanged += StateChanged; }
    static void Update()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying)
        {
            if (entered <= 0) return;
            try
            {
                if (!firstFrame && Time.time > 1.5f)
                {
                    firstFrame = true;
                    var go = new GameObject("Ocean Animation Verification Camera");
                    probe = go.AddComponent<Camera>(); probe.enabled = false;
                    probe.transform.position = new Vector3(104,14,-42); probe.transform.LookAt(new Vector3(104,-.25f,-65));
                    probe.cullingMask = 1 << 4; probe.clearFlags = CameraClearFlags.SolidColor; probe.backgroundColor = new Color(.55f,.75f,.85f);
                    probe.GetUniversalAdditionalCameraData().requiresDepthOption = CameraOverrideOption.On;
                    probe.GetUniversalAdditionalCameraData().requiresColorOption = CameraOverrideOption.On;
                    BoatAttackTutorialTools.Render(probe,Work+"/ocean-animation-a.png");
                    File.WriteAllText(Work+"/runtime-start.txt","Time.time="+Time.time+"; Ocean="+UnityEngine.Object.FindObjectOfType<TutorialOcean>().isActiveAndEnabled);
                }
                if (firstFrame && Time.time > 3f)
                {
                    BoatAttackTutorialTools.Render(probe,Work+"/ocean-animation-b.png");
                    File.WriteAllText(Work+"/runtime-complete.txt","Time.time="+Time.time);
                    entered = -1; EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { File.WriteAllText(Work+"/runtime-error.txt",error.ToString()); entered=-1;EditorApplication.isPlaying=false; }
            return;
        }
        string request = Work+"/request";
        if (!File.Exists(request)) return;
        string action;
        try { action=File.ReadAllText(request).Trim();File.Delete(request); }
        catch (IOException) { return; }
        busy=true;
        try
        {
            var scene=SceneManager.GetActiveScene();
            if (scene.path != BoatAttackTutorialTools.ScenePath) throw new InvalidOperationException("Open the tutorial scene in Edit Mode first.");
            if (action == "apply") Apply(scene);
            else if (action == "polish") Polish(scene);
            else if (action == "finish") Finish(scene);
            else if (action == "validate") Validate(scene);
            else if (action == "play") { EditorSceneManager.SaveScene(scene);SessionState.SetBool("BoatOceanProbe",true);EditorApplication.isPlaying=true; }
            else throw new InvalidOperationException("Unknown ocean action: "+action);
        }
        catch (Exception error) { File.WriteAllText(Work+"/error-"+DateTime.UtcNow.Ticks+".txt",error.ToString());Debug.LogException(error); }
        finally { busy=false; }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("BoatOceanProbe",false)) {entered=EditorApplication.timeSinceStartup;firstFrame=false;}
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("BoatOceanProbe",false)) {SessionState.SetBool("BoatOceanProbe",false);File.WriteAllText(Work+"/editmode-restored.txt",DateTime.UtcNow.ToString("O"));}
    }
    static void Apply(Scene scene)
    {
        Directory.CreateDirectory(Work);
        EditorSceneManager.SaveScene(scene);
        File.Copy(scene.path,Work+"/Scene.live-before.unity",true);
        if(scene.GetRootGameObjects().Any(g=>g.GetComponent<TutorialOcean>()!=null)) throw new InvalidOperationException("Ocean already exists; use validation.");
        var terrain=scene.GetRootGameObjects().Select(g=>g.GetComponent<Terrain>()).First(t=>t!=null);
        Directory.CreateDirectory(Root+"/Generated/Water");
        var resources=AssetDatabase.LoadAssetAtPath<WaterResources>(Root+"/Water/Scripts/Data/WaterResources.asset");
        var surface=AssetDatabase.LoadAssetAtPath<WaterSurfaceData>(Root+"/Water/Scripts/Data/WaterSurfaceData.asset");
        if(resources==null || surface==null || resources.defaultWaterMeshes.Any(m=>m==null)) throw new InvalidOperationException("Missing BoatAttack water resources.");
        surface._basicWaveSettings.amplitude=.3f;EditorUtility.SetDirty(surface);
        var mat=resources.defaultSeaMaterial;
        mat.shaderKeywords=new[]{"_REFLECTION_CUBEMAP","_DEBUG_OFF"};EditorUtility.SetDirty(mat);
        var cube=new Cubemap(128,TextureFormat.RGBAHalf,true) { name="Tutorial Shore Reflection" };
        var capture=new GameObject("Temporary Shore Reflection Capture");
        var captureTarget=new RenderTexture(128,128,24) { dimension=TextureDimension.Cube };
        var previousTarget=RenderTexture.active;
        var facePixels=new Texture2D(128,128,TextureFormat.RGBAHalf,false,true);
        try
        {
            var camera=capture.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.1f;camera.farClipPlane=800;
            camera.transform.position=new Vector3(110,2,-60);camera.clearFlags=CameraClearFlags.Skybox;
            if(!camera.RenderToCubemap(captureTarget)) throw new InvalidOperationException("Could not capture water reflection cubemap.");
            for(int face=0;face<6;face++) {Graphics.SetRenderTarget(captureTarget,0,(CubemapFace)face);facePixels.ReadPixels(new Rect(0,0,128,128),0,0);facePixels.Apply();cube.SetPixels(facePixels.GetPixels(),(CubemapFace)face);}
            cube.Apply(true,false);AssetDatabase.CreateAsset(cube,Root+"/Generated/Water/Tutorial Shore Reflection.asset");
        }
        finally { RenderTexture.active=previousTarget;UnityEngine.Object.DestroyImmediate(capture);UnityEngine.Object.DestroyImmediate(captureTarget);UnityEngine.Object.DestroyImmediate(facePixels); }
        int resolution=1024;
        var map=new Texture2D(resolution,resolution,TextureFormat.RHalf,false,true) { name="Tutorial Seafloor Height",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
        var bounds=new Vector4(terrain.transform.position.x-10,terrain.transform.position.z-10,terrain.terrainData.size.x+20,terrain.terrainData.size.z+20);
        var pixels=new Color[resolution*resolution];
        for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
        {
            var point=new Vector3(bounds.x+(x+.5f)/resolution*bounds.z,0,bounds.y+(z+.5f)/resolution*bounds.w);
            float h=point.x>=terrain.transform.position.x && point.x<=terrain.transform.position.x+terrain.terrainData.size.x && point.z>=terrain.transform.position.z && point.z<=terrain.transform.position.z+terrain.terrainData.size.z ? terrain.SampleHeight(point)+terrain.transform.position.y : -20;
            pixels[z*resolution+x]=new Color(h,0,0,1);
        }
        map.SetPixels(pixels);map.Apply();AssetDatabase.CreateAsset(map,Root+"/Generated/Water/Tutorial Seafloor Height.asset");
        var go=new GameObject("BoatAttack Ocean");go.layer=4;go.transform.position=new Vector3(0,-.25f,0);
        var ocean=go.AddComponent<TutorialOcean>();ocean.resources=resources;ocean.surface=surface;ocean.seafloorHeight=map;ocean.skyReflection=cube;
        ocean.depthBounds=new Vector4(bounds.x,bounds.y,1/bounds.z,1/bounds.w);ocean.Rebuild();
        Physics.SyncTransforms();
        var points=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
        var spawn=points.First(t=>t.name=="PlayerSpawn");var entry=points.First(t=>t.name=="BattleEntry - Placeholder");
        var oldSpawn=spawn.position;
        spawn.position=FindDryGround(terrain,oldSpawn,Vector3.zero,0);
        entry.position=FindDryGround(terrain,spawn.position+Vector3.right*8,spawn.position,7);
        AddShoreBarrier(scene,terrain);
        var preview=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c!=null);
        preview.GetUniversalAdditionalCameraData().requiresDepthOption=CameraOverrideOption.On;
        preview.GetUniversalAdditionalCameraData().requiresColorOption=CameraOverrideOption.On;
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Work+"/placement.txt","Old spawn: "+oldSpawn+"\nDry spawn: "+spawn.position+"\nEntry: "+entry.position+"\nSea level: "+ocean.SeaLevel);
        Validate(scene);
        File.WriteAllText(Work+"/complete.txt",DateTime.UtcNow.ToString("O"));Debug.Log("BOATATTACK_OCEAN_MIGRATION_OK");
    }
    static Vector3 FindDryGround(Terrain terrain,Vector3 preferred,Vector3 excluded,float minimumDistance)
    {
        Vector3 best=default;float score=float.MaxValue;
        for(float z=terrain.transform.position.z+8;z<terrain.transform.position.z+terrain.terrainData.size.z-8;z+=1)
        for(float x=terrain.transform.position.x+8;x<terrain.transform.position.x+terrain.terrainData.size.x-8;x+=1)
        {
            var p=new Vector3(x,0,z);float h=terrain.SampleHeight(p)+terrain.transform.position.y;
            if(h<.9f || h>4 || Vector3.Distance(p,new Vector3(excluded.x,0,excluded.z))<minimumDistance) continue;
            if(Physics.Raycast(new Vector3(x,100,z),Vector3.down,out var overhead,200,~0,QueryTriggerInteraction.Ignore) && overhead.point.y>h+.35f) continue;
            float slope=terrain.terrainData.GetSteepness((x-terrain.transform.position.x)/terrain.terrainData.size.x,(z-terrain.transform.position.z)/terrain.terrainData.size.z);
            if(slope>12) continue;
            p.y=h+.12f;
            if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.7f,.4f,~0,QueryTriggerInteraction.Ignore)) continue;
            bool dry=true;
            for(int i=0;i<8;i++) {var q=p+new Vector3(Mathf.Cos(i*Mathf.PI/4)*2,0,Mathf.Sin(i*Mathf.PI/4)*2);if(terrain.SampleHeight(q)+terrain.transform.position.y<.65f) dry=false;}
            if(!dry) continue;
            float candidate=(new Vector2(x-preferred.x,z-preferred.z)).sqrMagnitude+(h-1)*(h-1)*3;
            if(candidate<score) {score=candidate;best=p;}
        }
        if(score==float.MaxValue) throw new InvalidOperationException("No clear, dry beach position found.");
        return best;
    }
    static float SeabedHeight(Terrain terrain,Vector3 point)
    {
        var origin=terrain.transform.position;var size=terrain.terrainData.size;
        var nearest=new Vector3(Mathf.Clamp(point.x,origin.x,origin.x+size.x),0,Mathf.Clamp(point.z,origin.z,origin.z+size.z));
        float edge=terrain.SampleHeight(nearest)+origin.y;
        float distance=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(nearest.x,nearest.z));
        if(distance<.001f) return edge;
        float spread=32+Mathf.PerlinNoise(point.x*.025f+11,point.z*.025f+19)*14;
        return Mathf.Lerp(edge,-20,Mathf.SmoothStep(0,1,distance/spread));
    }
    static void Polish(Scene scene)
    {
        var terrain=scene.GetRootGameObjects().Select(g=>g.GetComponent<Terrain>()).First(t=>t!=null);
        var ocean=scene.GetRootGameObjects().Select(g=>g.GetComponent<TutorialOcean>()).First(o=>o!=null);
        var points=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
        var spawn=points.First(t=>t.name=="PlayerSpawn");var entry=points.First(t=>t.name=="BattleEntry - Placeholder");
        spawn.position=FindDryGround(terrain,new Vector3(110,0,-80),Vector3.zero,0);
        entry.position=FindDryGround(terrain,spawn.position+Vector3.right*8,spawn.position,7);
        var visual=scene.GetRootGameObjects().First(g=>g.name=="BoatAttack Beach Environment").transform.Find("Boundary Scenery");
        int hidden=0;
        foreach(Transform child in visual)
        {
            if(child.name=="Weathered Timber Fence") continue;
            var renderer=child.GetComponent<Renderer>();if(renderer==null) continue;
            if(terrain.SampleHeight(renderer.bounds.center)+terrain.transform.position.y<.4f) {child.gameObject.SetActive(false);hidden++;}
        }
        var fence=visual.Find("Weathered Timber Fence").GetComponent<MeshFilter>();
        if(!AssetDatabase.GetAssetPath(fence.sharedMesh).Contains("Ocean Boundary Fence"))
        {
            var mesh=UnityEngine.Object.Instantiate(fence.sharedMesh);mesh.name="Ocean Boundary Fence";
            var vertices=mesh.vertices;var triangles=mesh.triangles;var kept=new List<int>();
            if(triangles.Length%36!=0) throw new InvalidOperationException("Unexpected combined fence topology.");
            for(int i=0;i<triangles.Length;i+=36)
            {
                var center=Vector3.zero;for(int j=0;j<36;j++) center+=fence.transform.TransformPoint(vertices[triangles[i+j]])/36;
                if(terrain.SampleHeight(center)+terrain.transform.position.y<.4f) continue;
                for(int j=0;j<36;j++) kept.Add(triangles[i+j]);
            }
            mesh.triangles=kept.ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Root+"/Generated/Water/Ocean Boundary Fence.asset");fence.sharedMesh=mesh;
        }
        var bounds=new Vector4(terrain.transform.position.x-50,terrain.transform.position.z-50,terrain.terrainData.size.x+100,terrain.terrainData.size.z+100);
        var texture=ocean.seafloorHeight;var pixels=new Color[texture.width*texture.height];
        for(int z=0;z<texture.height;z++) for(int x=0;x<texture.width;x++) pixels[z*texture.width+x]=new Color(SeabedHeight(terrain,new Vector3(bounds.x+(x+.5f)/texture.width*bounds.z,0,bounds.y+(z+.5f)/texture.height*bounds.w)),0,0,1);
        texture.SetPixels(pixels);texture.Apply();EditorUtility.SetDirty(texture);
        ocean.depthBounds=new Vector4(bounds.x,bounds.y,1/bounds.z,1/bounds.w);
        var verticesSkirt=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        var corners=new[]{new Vector2(0,0),new Vector2(terrain.terrainData.size.x,0),new Vector2(terrain.terrainData.size.x,terrain.terrainData.size.z),new Vector2(0,terrain.terrainData.size.z)};
        for(int edge=0;edge<4;edge++)
        {
            var a=corners[edge];var b=corners[(edge+1)%4];int n=Mathf.CeilToInt(Vector2.Distance(a,b));
            var tangent=(b-a).normalized;var outward=new Vector2(tangent.y,-tangent.x);
            for(int segment=0;segment<n;segment++)
            {
                var pa=Vector2.Lerp(a,b,segment/(float)n);var pb=Vector2.Lerp(a,b,(segment+1)/(float)n);
                var basePoint=terrain.transform.position+new Vector3((pa.x+pb.x)*.5f,0,(pa.y+pb.y)*.5f);
                if(terrain.SampleHeight(basePoint)+terrain.transform.position.y>-.4f) continue;
                for(int ring=0;ring<24;ring++)
                {
                    int start=verticesSkirt.Count;
                    foreach(var q in new[]{pa+outward*ring*2,pb+outward*ring*2,pa+outward*(ring+1)*2,pb+outward*(ring+1)*2})
                    {
                        var p=terrain.transform.position+new Vector3(q.x,0,q.y);p.y=SeabedHeight(terrain,p)-.04f;verticesSkirt.Add(p);uv.Add(new Vector2(p.x/14,p.z/14));
                    }
                    indices.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
                }
            }
        }
        var skirt=new Mesh {name="Natural Seafloor Extension",indexFormat=IndexFormat.UInt32};skirt.SetVertices(verticesSkirt);skirt.SetUVs(0,uv);skirt.SetTriangles(indices,0);skirt.RecalculateNormals();skirt.RecalculateBounds();AssetDatabase.CreateAsset(skirt,Root+"/Generated/Water/Natural Seafloor Extension.asset");
        var sand=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name="Submerged Sand"};sand.SetTexture("_BaseMap",terrain.terrainData.terrainLayers[0].diffuseTexture);sand.SetColor("_BaseColor",Color.white);sand.SetFloat("_Smoothness",0);sand.SetFloat("_Cull",0);AssetDatabase.CreateAsset(sand,Root+"/Generated/Water/Submerged Sand.mat");
        var bed=new GameObject("Submerged Seafloor Extension",typeof(MeshFilter),typeof(MeshRenderer));bed.GetComponent<MeshFilter>().sharedMesh=skirt;bed.GetComponent<MeshRenderer>().sharedMaterial=sand;
        bed.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        ocean.Rebuild();Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Work+"/polish.txt","Dry spawn="+spawn.position+"\nEntry="+entry.position+"\nSubmerged boundary decorations hidden="+hidden+"\nSeafloor triangles="+indices.Count/3);
        Validate(scene);
    }
    static void AddShoreBarrier(Scene scene,Terrain terrain)
    {
        var env=scene.GetRootGameObjects().First(g=>g.name=="BoatAttack Beach Environment");
        var root=new GameObject("Shoreline Activity Boundary");root.transform.SetParent(env.transform,true);
        float step=1.5f,level=.55f;
        int nx=Mathf.CeilToInt(terrain.terrainData.size.x/step),nz=Mathf.CeilToInt(terrain.terrainData.size.z/step);
        var heights=new float[nz+1,nx+1];
        for(int z=0;z<=nz;z++) for(int x=0;x<=nx;x++)
        {
            var p=terrain.transform.position+new Vector3(x*step,0,z*step);
            heights[z,x]=Physics.Raycast(new Vector3(p.x,100,p.z),Vector3.down,out var hit,200,~(1<<4),QueryTriggerInteraction.Ignore) ? hit.point.y : -20;
        }
        var segments=new List<Tuple<Vector3,Vector3>>();
        for(int z=0;z<nz;z++) for(int x=0;x<nx;x++)
        {
            var vertices=new[]{new Vector3(x*step,heights[z,x],z*step),new Vector3((x+1)*step,heights[z,x+1],z*step),new Vector3((x+1)*step,heights[z+1,x+1],(z+1)*step),new Vector3(x*step,heights[z+1,x],(z+1)*step)};
            var crossings=new List<Vector3>();
            for(int k=0;k<4;k++) {var a=vertices[k];var b=vertices[(k+1)%4];if((a.y>level)!=(b.y>level)) {var p=Vector3.Lerp(a,b,Mathf.InverseLerp(a.y,b.y,level));p.x+=terrain.transform.position.x;p.z+=terrain.transform.position.z;p.y=level;crossings.Add(p);}}
            for(int k=0;k+1<crossings.Count;k+=2)
            {
                var a=crossings[k];var b=crossings[k+1];var middle=(a+b)*.5f;
                if(middle.x<16 || middle.x>216 || middle.z< -173 || middle.z> -49 || Vector3.Distance(a,b)<.01f) continue;
                var wall=new GameObject("Shore Barrier "+segments.Count);wall.transform.SetParent(root.transform,true);
                wall.transform.position=middle+Vector3.up*1.3f;wall.transform.rotation=Quaternion.LookRotation(b-a,Vector3.up);
                wall.AddComponent<BoxCollider>().size=new Vector3(.65f,3.2f,Vector3.Distance(a,b)+.65f);segments.Add(Tuple.Create(a,b));
            }
        }
        Physics.SyncTransforms();int blocked=0;
        foreach(var segment in segments)
        {
            var midpoint=(segment.Item1+segment.Item2)*.5f+Vector3.up;
            var normal=Vector3.Cross((segment.Item2-segment.Item1).normalized,Vector3.up);
            if(Physics.RaycastAll(midpoint-normal*1.5f,normal,3,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(root.transform))) blocked++;
        }
        File.WriteAllText(Work+"/shore-boundary.txt","Segments="+segments.Count+"\nCrossing rays blocked="+blocked+"\nLevel="+level+"\nPier decking included in surface sampling.");
        if(blocked!=segments.Count) throw new InvalidOperationException("Shore boundary collision validation failed.");
    }
    static void Finish(Scene scene)
    {
        var terrain=scene.GetRootGameObjects().Select(g=>g.GetComponent<Terrain>()).First(t=>t!=null);
        var spawn=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name=="PlayerSpawn");
        spawn.LookAt(new Vector3(65,spawn.position.y,-80));
        var stored=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Generated/Water/Natural Seafloor Extension.asset");
        var vertices=stored.vertices.ToList();var uvs=stored.uv.ToList();var triangles=stored.triangles.ToList();
        Action<Vector2,Vector2,Vector2,Vector2> add=(a,b,c,d)=>
        {
            int offset=vertices.Count;
            foreach(var q in new[]{a,b,c,d}) {var p=terrain.transform.position+new Vector3(q.x,0,q.y);p.y=SeabedHeight(terrain,p)-.04f;vertices.Add(p);uvs.Add(new Vector2(p.x/14,p.z/14));}
            triangles.AddRange(new[]{offset,offset+1,offset+2,offset+1,offset+3,offset+2});
        };
        var corners=new[]{new Vector2(0,0),new Vector2(terrain.terrainData.size.x,0),new Vector2(terrain.terrainData.size.x,terrain.terrainData.size.z),new Vector2(0,terrain.terrainData.size.z)};
        for(int edge=0;edge<4;edge++)
        {
            var a=corners[edge];var b=corners[(edge+1)%4];int n=Mathf.CeilToInt(Vector2.Distance(a,b));var tangent=(b-a).normalized;var outward=new Vector2(tangent.y,-tangent.x);
            for(int segment=0;segment<n;segment++)
            {
                var pa=Vector2.Lerp(a,b,segment/(float)n);var pb=Vector2.Lerp(a,b,(segment+1)/(float)n);
                float h=terrain.SampleHeight(terrain.transform.position+new Vector3((pa.x+pb.x)*.5f,0,(pa.y+pb.y)*.5f))+terrain.transform.position.y;
                if(h<=-.4f || h>3) continue;
                for(int ring=0;ring<24;ring++) add(pa+outward*ring*2,pb+outward*ring*2,pa+outward*(ring+1)*2,pb+outward*(ring+1)*2);
            }
        }
        foreach(var corner in corners)
        {
            float h=terrain.SampleHeight(terrain.transform.position+new Vector3(corner.x,0,corner.y))+terrain.transform.position.y;
            if(h>3) continue;
            float sx=corner.x==0?-1:1,sz=corner.y==0?-1:1;
            for(int x=0;x<24;x++) for(int z=0;z<24;z++) add(corner+new Vector2(sx*x*2,sz*z*2),corner+new Vector2(sx*(x+1)*2,sz*z*2),corner+new Vector2(sx*x*2,sz*(z+1)*2),corner+new Vector2(sx*(x+1)*2,sz*(z+1)*2));
        }
        stored.Clear();stored.indexFormat=IndexFormat.UInt32;stored.SetVertices(vertices);stored.SetUVs(0,uvs);stored.SetTriangles(triangles,0);stored.RecalculateNormals();stored.RecalculateBounds();EditorUtility.SetDirty(stored);
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Validate(scene);
        var entry=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name=="BattleEntry - Placeholder");
        int dry=0,clear=0;
        for(int i=0;i<=20;i++)
        {
            var p=Vector3.Lerp(spawn.position,entry.position,i/20f);p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.12f;
            if(p.y>.75f) dry++;
            if(!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore)) clear++;
        }
        File.WriteAllText(Work+"/final-validation.txt","Spawn="+spawn.position+"\nEntry="+entry.position+"\nDry route samples="+dry+"/21\nClear capsule samples="+clear+"/21\nEdit Mode="+!EditorApplication.isPlaying+"\nScene saved="+!scene.isDirty);
        if(dry!=21 || clear!=21) throw new InvalidOperationException("Dry beach route check failed.");
    }
    static void Validate(Scene scene)
    {
        BoatAttackTutorialTools.ValidateCurrentScene();
        var ocean=scene.GetRootGameObjects().Select(g=>g.GetComponent<TutorialOcean>()).First(o=>o!=null);
        var shader=ocean.resources.defaultSeaMaterial.shader;
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();
        if(errors.Length>0) throw new InvalidOperationException(string.Join("\n",errors.Select(e=>e.message)));
        var camera=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c!=null);
        var p=camera.transform.position;var r=camera.transform.rotation;
        try
        {
            BoatAttackTutorialTools.Render(camera,Work+"/ocean-overview.png");
            camera.transform.position=new Vector3(110,11,-40);camera.transform.LookAt(new Vector3(110,1,-102));
            BoatAttackTutorialTools.Render(camera,Work+"/ocean-shore.png");
        }
        finally {camera.transform.SetPositionAndRotation(p,r);}
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Work+"/water-validation.txt","Ocean active="+ocean.isActiveAndEnabled+"\nWater meshes="+ocean.resources.defaultWaterMeshes.Length+"\nTriangles="+ocean.resources.defaultWaterMeshes.Sum(m=>m.triangles.Length/3)+"\nShader="+shader.name+"\nShader supported="+shader.isSupported+"\nShader errors="+errors.Length+"\nScene saved="+!scene.isDirty);
    }
}
