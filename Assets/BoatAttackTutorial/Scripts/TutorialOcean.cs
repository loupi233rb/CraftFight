using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem;
using WaterSystem.Data;

namespace CraftFight.Tutorial
{
    /// <summary>BoatAttack water shading and meshes, scoped to the tutorial's cameras.</summary>
    [ExecuteAlways]
    public sealed class TutorialOcean : MonoBehaviour
    {
        public WaterResources resources;
        public WaterSurfaceData surface;
        public Texture2D seafloorHeight;
        public Vector4 depthBounds;
        public Cubemap skyReflection;

        Material material;
        Texture2D ramp, neutralEffects;
        MaterialPropertyBlock properties;
        readonly Dictionary<UniversalAdditionalCameraData, Vector2Int> cameraOptions = new Dictionary<UniversalAdditionalCameraData, Vector2Int>();
        public float SeaLevel => transform.position.y;
        public Material WaterMaterial => material != null ? material : resources != null ? resources.defaultSeaMaterial : null;

        void OnEnable()
        {
            Rebuild();
            RenderPipelineManager.beginCameraRendering += DrawOcean;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawOcean;
            foreach (var entry in cameraOptions)
                if (entry.Key != null)
                {
                    entry.Key.requiresDepthOption = (CameraOverrideOption)entry.Value.x;
                    entry.Key.requiresColorOption = (CameraOverrideOption)entry.Value.y;
                }
            cameraOptions.Clear();
            Release(material); Release(ramp); Release(neutralEffects);
            material = null; ramp = null; neutralEffects = null;
        }

        static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        [ContextMenu("Rebuild Ocean Material")]
        public void Rebuild()
        {
            if (resources == null || surface == null || seafloorHeight == null) return;
            Release(material); Release(ramp); Release(neutralEffects);
            material = new Material(resources.defaultSeaMaterial) { hideFlags = HideFlags.HideAndDontSave };
            material.DisableKeyword("_REFLECTION_PLANARREFLECTION");
            material.DisableKeyword("_REFLECTION_PROBES");
            material.EnableKeyword("_REFLECTION_CUBEMAP");
            material.DisableKeyword("USE_STRUCTURED_BUFFER");
            material.DisableKeyword("_STATIC_SHADER");
            ramp = new Texture2D(128, 4, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var colors = new Color[512];
            for (int i = 0; i < 128; i++)
            {
                float t = i / 128f;
                colors[i] = surface._absorptionRamp.Evaluate(t);
                colors[i+128] = surface._scatterRamp.Evaluate(t);
                float foam = surface._foamSettings.foamType == 1 ? surface._foamSettings.basicFoam.Evaluate(t) : t;
                colors[i+256] = resources.defaultFoamRamp.GetPixelBilinear(foam,.5f);
                colors[i+384] = colors[i+256];
            }
            ramp.SetPixels(colors); ramp.Apply();
            neutralEffects = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            neutralEffects.SetPixel(0, 0, new Color(0,.5f,.5f,.5f)); neutralEffects.Apply();
            properties = new MaterialPropertyBlock();
            properties.SetTexture("_AbsorptionScatteringRamp", ramp);
            properties.SetTexture("_WaterFXMap", neutralEffects);
            properties.SetTexture("_WaterDepthMap", seafloorHeight);
            properties.SetTexture("_FoamMap", resources.defaultFoamMap);
            properties.SetTexture("_SurfaceMap", resources.defaultSurfaceMap);
            if (skyReflection != null) properties.SetTexture("_CubemapTexture", skyReflection);
            properties.SetVector("_TutorialDepthBounds", depthBounds);
            properties.SetFloat("_MaxDepth", surface._waterMaxVisibility);
            SetupWaves();
        }

        void SetupWaves()
        {
            var data = new Vector4[20];
            var origins = new Vector4[10];
            bool custom = surface._customWaves && surface._waves.Count > 0;
            int count = Mathf.Clamp(custom ? surface._waves.Count : surface._basicWaveSettings.numWaves,1,10);
            float amplitude = 0;
            var oldState = Random.state;
            try
            {
                Random.InitState(surface.randomSeed);
                for (int i = 0; i < count; i++)
                {
                    var basic = surface._basicWaveSettings;
                    float p = Mathf.Lerp(.5f,1.5f,i/(float)count);
                    var wave = custom ? surface._waves[i] : new Wave(
                        basic.amplitude*p*Random.Range(.8f,1.2f), basic.direction+Random.Range(-90f,90f),
                        basic.wavelength*p*Random.Range(.6f,1.4f),Vector2.zero,false);
                    data[i] = new Vector4(Mathf.Max(.001f,wave.amplitude),wave.direction,Mathf.Max(.1f,wave.wavelength),wave.onmiDir);
                    origins[i] = new Vector4(wave.origin.x,wave.origin.y,0,0);
                    amplitude += data[i].x;
                    Random.InitState(surface.randomSeed+i+1);
                }
            }
            finally { Random.state = oldState; }
            for (int i = 0; i < 10; i++) data[i+10] = origins[i];
            properties.SetVectorArray("waveData", data);
            properties.SetInt("_WaveCount", count);
            properties.SetFloat("_MaxWaveHeight", amplitude/count);
        }

        void DrawOcean(ScriptableRenderContext context, Camera camera)
        {
            if (material == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            bool sceneView = camera.cameraType == CameraType.SceneView;
            if (sceneView ? UnityEngine.SceneManagement.SceneManager.GetActiveScene() != gameObject.scene : camera.gameObject.scene != gameObject.scene) return;
            if (!sceneView)
            {
                var options = camera.GetUniversalAdditionalCameraData();
                if (!cameraOptions.ContainsKey(options)) cameraOptions.Add(options,new Vector2Int((int)options.requiresDepthOption,(int)options.requiresColorOption));
                options.requiresDepthOption = CameraOverrideOption.On;
                options.requiresColorOption = CameraOverrideOption.On;
            }
            properties.SetFloat("_TutorialHasRefraction",sceneView ? 0 : 1);
            properties.SetFloat("_WaveHeight",SeaLevel);
            var position = camera.transform.TransformPoint(Vector3.forward*10);
            position.x = Mathf.Floor(position.x/6.25f)*6.25f;
            position.z = Mathf.Floor(position.z/6.25f)*6.25f;
            position.y = SeaLevel;
            var matrix = Matrix4x4.TRS(position,Quaternion.identity,Vector3.one);
            foreach (var mesh in resources.defaultWaterMeshes)
                if (mesh != null) Graphics.DrawMesh(mesh,matrix,material,gameObject.layer,camera,0,properties,ShadowCastingMode.Off,true,null,LightProbeUsage.Off);
        }
    }
}
