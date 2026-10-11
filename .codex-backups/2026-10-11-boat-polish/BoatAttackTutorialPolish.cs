using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CraftFight.Tutorial;

public static class BoatAttackTutorialPolish
{
    const string Root = "Assets/BoatAttackTutorial";
    const string Output = "Logs/BoatAttackTutorialPolish";
    static readonly Vector2[] Perimeter = {
        new Vector2(13,-47),new Vector2(60,-43),new Vector2(112,-45),
        new Vector2(168,-43),new Vector2(220,-49),new Vector2(222,-95),
        new Vector2(219,-143),new Vector2(204,-177),new Vector2(150,-178),
        new Vector2(100,-177),new Vector2(45,-176),new Vector2(13,-159),new Vector2(12,-105)
    };

    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != BoatAttackTutorialTools.ScenePath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open the tutorial scene in Edit Mode before applying environment changes.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.SaveScene(scene);
        if (!File.Exists(Output + "/Scene.before.unity")) File.Copy(scene.path, Output + "/Scene.before.unity");
        var terrain = scene.GetRootGameObjects().Select(g => g.GetComponent<Terrain>()).First(t => t != null);
        if (!File.Exists(Output + "/Terrain.before.asset")) File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData), Output + "/Terrain.before.asset");
        PaintTerrain(terrain);
        var env = scene.GetRootGameObjects().First(g => g.name == "BoatAttack Beach Environment");
        BuildPerimeter(env, terrain);
        var setup = env.GetComponent<TutorialVegetationSetup>();
        setup.enabled = false;
        setup.enabled = true;
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        BoatAttackTutorialTools.ValidateCurrentScene();
        AuditBoundary(env, terrain);
        var camera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).First(c => c != null);
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        try
        {
            camera.transform.position = new Vector3(194, 89, -52);
            camera.transform.LookAt(new Vector3(140, 24, -145));
            BoatAttackTutorialTools.Render(camera, Output + "/mountain-textures.png");
            camera.transform.position = new Vector3(112, 8, -69);
            camera.transform.LookAt(new Vector3(116, 1, -43));
            BoatAttackTutorialTools.Render(camera, Output + "/boundary-view.png");
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); }
        File.WriteAllText(Output + "/complete.txt", DateTime.UtcNow.ToString("O"));
        Debug.Log("BOATATTACK_TUTORIAL_POLISH_OK");
    }

    static TerrainLayer Layer(string name, string source, Vector2 tiling, Color tint, float normal)
    {
        string path = Root + "/Generated/Terrain/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainLayer>(source));
            layer.name = name;
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.tileSize = tiling;
        layer.normalScale = normal;
        layer.smoothness = 0;
        layer.metallic = 0;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = tint;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static void PaintTerrain(Terrain terrain)
    {
        string original = Root + "/Source/Objects/Levels/Island/";
        var sand = Layer("Tutorial Sand", original + "BeachSand.terrainlayer", Vector2.one * 14, Color.white, .35f);
        var rock = Layer("Tutorial Mountain Rock", original + "SandRocks.terrainlayer", Vector2.one * 9, new Color(.70f,.77f,.86f,1), .7f);
        var plants = Layer("Tutorial Rocky Groundcover", original + "SandRocks.terrainlayer", Vector2.one * 10, Color.white, .45f);
        plants.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Source/Textures/Terrain/RockPlants_basecolor.tif");
        plants.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Source/Textures/Terrain/RockPlants_normal.tif");
        var bush = Layer("Tutorial Moss Ground", original + "Bush.terrainlayer", Vector2.one * 12, new Color(.9f,1,.85f,1), .4f);
        if (new[] {sand,rock,plants,bush}.Any(l => l.diffuseTexture == null || l.normalMapTexture == null))
            throw new InvalidOperationException("Terrain textures did not import correctly.");
        var data = terrain.terrainData;
        data.terrainLayers = new[] {sand,rock,plants,bush};
        var weights = new float[data.alphamapHeight, data.alphamapWidth, 4];
        var totals = new double[4];
        for (int z = 0; z < data.alphamapHeight; z++)
        for (int x = 0; x < data.alphamapWidth; x++)
        {
            float u = x / (float)(data.alphamapWidth-1), v = z / (float)(data.alphamapHeight-1);
            float wx = terrain.transform.position.x + u*data.size.x, wz = terrain.transform.position.z + v*data.size.z;
            float height = terrain.transform.position.y + data.GetInterpolatedHeight(u,v);
            float steep = Smooth(19,43,data.GetSteepness(u,v));
            float mountain = Smooth(2,12,height);
            float noise = Mathf.PerlinNoise(wx*.07f+11,wz*.07f+21);
            float rim = (1-Smooth(2,10,EdgeDistance(new Vector2(wx,wz)))) * (.3f + .5f*noise);
            float rockWeight = mountain*(.35f + .65f*steep);
            float plantWeight = (1-steep)*Mathf.Max(mountain*.50f, rim*.60f);
            float bushWeight = mountain*(1-steep)*(.10f+.25f*noise);
            float sandWeight = Mathf.Max(0,1-rockWeight-plantWeight-bushWeight);
            float sum = sandWeight+rockWeight+plantWeight+bushWeight;
            float[] cell = {sandWeight/sum,rockWeight/sum,plantWeight/sum,bushWeight/sum};
            for (int l = 0; l < 4; l++) { weights[z,x,l] = cell[l]; totals[l] += cell[l]; }
        }
        data.SetAlphamaps(0,0,weights);
        terrain.Flush();
        EditorUtility.SetDirty(data);
        File.WriteAllText(Output+"/terrain-coverage.txt", string.Join("\n", data.terrainLayers.Select((l,i) => l.name+": "+(100*totals[i]/(data.alphamapWidth*data.alphamapHeight)).ToString("F1")+"%")));
    }

    static float Smooth(float min, float max, float value) { return Mathf.SmoothStep(0,1,Mathf.InverseLerp(min,max,value)); }
    static float EdgeDistance(Vector2 point)
    {
        float result = float.MaxValue;
        for (int i=0;i<Perimeter.Length;i++)
        {
            var a=Perimeter[i];var b=Perimeter[(i+1)%Perimeter.Length];var ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/ab.sqrMagnitude);
            result=Mathf.Min(result,Vector2.Distance(point,a+ab*t));
        }
        return result;
    }
    static float Height(Terrain terrain, Vector3 point) { return terrain.SampleHeight(point)+terrain.transform.position.y; }

    static void BuildPerimeter(GameObject env, Terrain terrain)
    {
        foreach(string name in new[]{"Boundary Scenery","Tutorial Activity Boundary"})
        {
            var old=env.transform.Find(name);
            if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        var visual=new GameObject("Boundary Scenery"); visual.transform.SetParent(env.transform,false);
        var collision=new GameObject("Tutorial Activity Boundary");collision.transform.SetParent(env.transform,false);
        var filters=env.GetComponentsInChildren<MeshFilter>(true);
        var rocks=filters.Where(f=>f.sharedMesh!=null && f.GetComponent<MeshRenderer>()!=null && f.name.StartsWith("Rock_Medium") && (f.name.Contains("LOD1")||f.name.Contains("LOD2"))).GroupBy(f=>f.sharedMesh).Select(g=>g.First()).ToArray();
        var shrub=filters.First(f=>f.sharedMesh!=null && f.GetComponent<MeshRenderer>()!=null && f.name.StartsWith("Bush_Large") && !f.name.Contains("LOD"));
        var palm=filters.First(f=>f.sharedMesh!=null && f.GetComponent<MeshRenderer>()!=null && f.name.StartsWith("Palm_LOD0"));
        if(rocks.Length==0) throw new InvalidOperationException("No source rocks found.");
        var random=new System.Random(20261011);
        Func<float,float,float> next=(a,b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        var fenceParts=new List<CombineInstance>();
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh=cube.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(cube);
        int segment=0;
        for(int edge=0;edge<Perimeter.Length;edge++)
        {
            Vector2 a=Perimeter[edge],b=Perimeter[(edge+1)%Perimeter.Length];
            int count=Mathf.CeilToInt(Vector2.Distance(a,b)/5);
            for(int part=0;part<count;part++,segment++)
            {
                Vector2 aa=Vector2.Lerp(a,b,part/(float)count),bb=Vector2.Lerp(a,b,(part+1)/(float)count);
                Vector3 start=new Vector3(aa.x,0,aa.y),end=new Vector3(bb.x,0,bb.y);
                Vector3 center=(start+end)*.5f,tangent=(end-start).normalized;
                Vector3 inward=new Vector3(tangent.z,0,-tangent.x);
                float ha=Height(terrain,start),hb=Height(terrain,end),hc=Height(terrain,center);
                float bottom=Mathf.Min(ha,hb,hc)-3,top=Mathf.Max(ha,hb,hc)+9;
                var wall=new GameObject("Boundary collider "+segment.ToString("D3")); wall.transform.SetParent(collision.transform,false);
                wall.transform.SetPositionAndRotation(new Vector3(center.x,(bottom+top)*.5f,center.z),Quaternion.LookRotation(tangent));
                var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(1.2f,top-bottom,Vector3.Distance(start,end)+1.2f);
                bool fence=hc<10 && Mathf.Abs(ha-hb)<1.8f && segment%6>=3;
                if(fence)
                {
                    foreach(float t in new[]{0f,.5f,1f})
                    {
                        var p=Vector3.Lerp(start,end,t)+inward*.1f;p.y=Height(terrain,p)+1.22f;
                        fenceParts.Add(new CombineInstance {mesh=cubeMesh,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,next(-6,6),next(-3,3)),new Vector3(.25f,2.65f,.25f))});
                    }
                    foreach(float level in new[]{.8f,1.8f,2.35f})
                    {
                        var p=start+Vector3.up*(ha+level);var q=end+Vector3.up*(hb+level);
                        fenceParts.Add(new CombineInstance {mesh=cubeMesh,transform=Matrix4x4.TRS((p+q)*.5f,Quaternion.FromToRotation(Vector3.right,q-p),new Vector3(Vector3.Distance(p,q)+.15f,.19f,.14f))});
                    }
                }
                if(!fence || segment%3==0)
                {
                    var p=center+inward*next(.4f,1.8f)+tangent*next(-.7f,.7f);
                    Place(rocks[segment%rocks.Length],visual.transform,terrain,p,new Vector3(next(4.6f,6.8f),next(2.5f,4.4f),next(3.3f,5.3f)),next(0,360),.32f,"Boundary Rock "+segment);
                }
                if(segment%3==0 && hc<17 && Mathf.Abs(ha-hb)<4)
                {
                    var p=center+inward*next(2.8f,4.7f)+tangent*next(-1.5f,1.5f);
                    float size=next(2.3f,3.6f);
                    Place(shrub,visual.transform,terrain,p,new Vector3(size,next(1.6f,2.4f),size),next(0,360),.10f,"Boundary Shrub "+segment);
                }
                if(segment%11==0 && hc<8 && Mathf.Abs(ha-hb)<2)
                {
                    var p=center+inward*next(3.3f,5.6f);
                    float height=next(8,11),width=next(5.5f,7);
                    Place(palm,visual.transform,terrain,p,new Vector3(width,height,width),next(0,360),.08f,"Boundary Palm "+segment);
                }
            }
        }
        var combined=new Mesh {name="Tutorial Boundary Timber"};
        combined.CombineMeshes(fenceParts.ToArray(),true,true);
        string meshPath=Root+"/Generated/Terrain/TutorialBoundaryFence.asset";
        var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(stored==null) {AssetDatabase.CreateAsset(combined,meshPath);stored=combined;}
        else {EditorUtility.CopySerialized(combined,stored);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(stored);}
        string materialPath=Root+"/Generated/Materials/Boundary Timber.mat";
        var wood=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(wood==null) {wood=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(wood,materialPath);}
        wood.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Map/tex/spruce_planks.png"));
        wood.SetColor("_BaseColor",new Color(.66f,.63f,.54f));wood.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(wood);
        var timber=new GameObject("Weathered Timber Fence",typeof(MeshFilter),typeof(MeshRenderer));
        timber.transform.SetParent(visual.transform,true);timber.transform.position=Vector3.zero;
        timber.GetComponent<MeshFilter>().sharedMesh=stored;timber.GetComponent<MeshRenderer>().sharedMaterial=wood;
    }

    static void Place(MeshFilter source, Transform parent, Terrain terrain, Vector3 point, Vector3 size, float yaw, float embed, string name)
    {
        var mesh=source.sharedMesh;var bounds=mesh.bounds;
        var scale=new Vector3(size.x/Mathf.Max(.01f,bounds.size.x),size.y/Mathf.Max(.01f,bounds.size.y),size.z/Mathf.Max(.01f,bounds.size.z));
        var rotation=Quaternion.Euler(0,yaw,0);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.transform.localScale=scale;
        point.y=Height(terrain,point)+size.y*(.5f-embed);
        go.transform.SetPositionAndRotation(point-rotation*Vector3.Scale(bounds.center,scale),rotation);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
        if(name.StartsWith("Boundary Rock")) go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }

    static void AuditBoundary(GameObject env, Terrain terrain)
    {
        var walls=env.transform.Find("Tutorial Activity Boundary").GetComponentsInChildren<BoxCollider>();
        int failures=0,probes=0;
        foreach(var wall in walls)
        {
            foreach(float t in new[]{-.42f,0f,.42f})
            {
                var p=wall.transform.position+wall.transform.forward*wall.size.z*t;p.y=Height(terrain,p)+1.6f;
                var normal=wall.transform.right;
                probes++;
                if(!wall.Raycast(new Ray(p-normal*3,normal),out var hit,6)) failures++;
            }
        }
        foreach(var vertex in Perimeter)
        {
            var p=new Vector3(vertex.x,0,vertex.y);p.y=Height(terrain,p)+1.6f;
            var direction=new Vector3(vertex.x-117,0,vertex.y+110).normalized;
            var ray=new Ray(p-direction*3,direction);
            probes++;
            if(!walls.Any(w=>w.Raycast(ray,out var hit,6))) failures++;
        }
        foreach(var marker in env.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name=="PlayerSpawn" || t.name.StartsWith("BattleEntry")))
            if(Physics.CheckCapsule(marker.position+Vector3.up*.4f,marker.position+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException(marker.name+" is obstructed after decorating the boundary.");
        var data=terrain.terrainData;var map=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
        float maxError=0;
        for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++)
        {float sum=0;for(int l=0;l<data.alphamapLayers;l++)sum+=map[z,x,l];maxError=Mathf.Max(maxError,Mathf.Abs(sum-1));}
        float weightTolerance=data.alphamapLayers*.5f/255f+1e-5f;
        File.WriteAllText(Output+"/boundary-validation.txt","Closed perimeter vertices: "+Perimeter.Length+"\nSolid boundary colliders: "+walls.Length+"\nBlocked crossing probes (including corners): "+(probes-failures)+"/"+probes+"\nTerrain texture layers: "+data.alphamapLayers+"\nWeight texture format: "+data.alphamapTextures[0].format+"\nMaximum texture-weight error: "+maxError+"\nWeight rounding tolerance: "+weightTolerance+"\nSpawn and battle-entry capsules: unobstructed\nScene saved: "+!env.scene.isDirty);
        if(failures>0 || maxError>weightTolerance) throw new InvalidOperationException("Boundary/terrain validation failed; inspect "+Output);
    }
}
