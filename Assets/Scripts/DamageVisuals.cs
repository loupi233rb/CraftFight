using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class DamageVisuals : MonoBehaviour
{
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Min(0f)] private float hitDuration = 0.2f;

    private readonly List<MaterialSlot> slots = new List<MaterialSlot>();
    private MaterialPropertyBlock block;
    private Coroutine hitRoutine;
    private bool dead;

    private struct MaterialSlot
    {
        public Renderer renderer;
        public int index;
        public int colorProperty;
        public Color originalColor;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                int property = material.HasProperty("_BaseColor")
                    ? Shader.PropertyToID("_BaseColor")
                    : material.HasProperty("_Color") ? Shader.PropertyToID("_Color") : 0;
                if (property == 0)
                    continue;

                slots.Add(new MaterialSlot
                {
                    renderer = renderer,
                    index = i,
                    colorProperty = property,
                    originalColor = material.GetColor(property)
                });
            }
        }
    }

    public void ShowHit()
    {
        if (dead)
            return;
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        SetTint(hitColor);
        hitRoutine = StartCoroutine(RestoreAfterHit());
    }

    public void ShowDeath()
    {
        dead = true;
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        SetTint(hitColor);
    }

    private IEnumerator RestoreAfterHit()
    {
        yield return new WaitForSeconds(hitDuration);
        if (!dead)
            RestoreColors();
        hitRoutine = null;
    }

    private void SetTint(Color color)
    {
        foreach (MaterialSlot slot in slots)
        {
            if (slot.renderer == null)
                continue;
            slot.renderer.GetPropertyBlock(block, slot.index);
            block.SetColor(slot.colorProperty, slot.originalColor * color);
            slot.renderer.SetPropertyBlock(block, slot.index);
        }
    }

    private void RestoreColors()
    {
        foreach (MaterialSlot slot in slots)
        {
            if (slot.renderer == null)
                continue;
            slot.renderer.GetPropertyBlock(block, slot.index);
            block.SetColor(slot.colorProperty, slot.originalColor);
            slot.renderer.SetPropertyBlock(block, slot.index);
        }
    }
}
