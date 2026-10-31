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
            // Scene RenderSettings own ambient lighting. Only these renderers need
            // the custom vegetation probe; never replace the global ambient probe.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.35f, 0.4f, 0.45f));
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.gameObject.isStatic = false;
                renderer.lightmapIndex = -1;
                // This lighting-bake setting exists only in the Unity Editor API.
#if UNITY_EDITOR
                renderer.receiveGI = ReceiveGI.LightProbes;
#endif
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

    }
}
