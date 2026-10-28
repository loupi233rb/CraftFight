using UnityEngine;
using UnityEngine.Rendering;

namespace CraftFight.Tutorial
{
    [ExecuteAlways]
    public sealed class TutorialVegetationSetup : MonoBehaviour
    {
        private static readonly int Position = Shader.PropertyToID("_Position");

        private void OnEnable()
        {
            ApplyAmbient();
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.35f, 0.4f, 0.45f));
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.gameObject.isStatic = false;
                renderer.lightmapIndex = -1;
                renderer.receiveGI = ReceiveGI.LightProbes;
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                renderer.GetPropertyBlock(properties);
                properties.CopySHCoefficientArraysFrom(new[] { ambient });
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.HasProperty(Position))
                    {
                        properties.SetVector(Position, renderer.transform.position);
                        break;
                    }
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.gameObject.scene == gameObject.scene) ApplyAmbient();
        }

        private static void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.69f, 0.76f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.35f, 0.4f, 0.45f));
            RenderSettings.ambientProbe = ambient;
        }
    }
}
