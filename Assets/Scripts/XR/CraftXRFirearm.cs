using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Single-shot hitscan fire from the held model's muzzle, shared by real and simulated XR input.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HandInventory))]
public sealed class CraftXRFirearm : MonoBehaviour
{
    private HandInventory inventory;
    private SteveHealth health;
    private SteveFirstPersonController movement;
    private PlayerEconomy economy;
    private SteveMeleeAttack attack;
    private AudioSource audioSource;
    private float nextShotTime;
    private float flashUntil;
    private float tracerUntil;
    private GameObject effects;
    private GameObject flash;
    private LineRenderer tracer;
    private Material effectMaterial;

    public int ShotsFired { get; private set; }

    private void Awake()
    {
        inventory = GetComponent<HandInventory>();
        health = GetComponentInParent<SteveHealth>();
        movement = GetComponentInParent<SteveFirstPersonController>();
        economy = GetComponentInParent<PlayerEconomy>();
        attack = GetComponentInParent<SteveMeleeAttack>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 40f;
        audioSource.volume = 0.45f;
    }

    public bool TryFire()
    {
        ItemData item = inventory.SelectedItemData;
        Transform model = inventory.HeldItemTransform;
        if (!isActiveAndEnabled || !inventory.IsInventoryEnabled || item == null || !item.isFirearm ||
            model == null || !model.gameObject.activeInHierarchy || Time.time < nextShotTime ||
            (health != null && health.IsDead) || (movement != null && movement.IsMovementSuspended) ||
            (economy != null && economy.BlocksGameplayInput)) return false;
        Transform muzzle = model.Find("muzzle");
        if (muzzle == null) return false;
        nextShotTime = Time.time + Mathf.Max(0.05f, item.firearmInterval);
        Vector3 origin = muzzle.position;
        Vector3 direction = muzzle.forward;
        Vector3 end = origin + direction * Mathf.Max(0.1f, item.firearmRange);
        RaycastHit hit = default;
        // A gun pushed through a wall must still hit the wall between the hand and muzzle.
        Vector3 barrel = origin - transform.position;
        bool blocked = barrel.sqrMagnitude > 0.000001f &&
            FirstHit(transform.position, barrel.normalized, barrel.magnitude, out hit);
        if (!blocked) blocked = FirstHit(origin, direction, Mathf.Max(0.1f, item.firearmRange), out hit);
        if (blocked)
        {
            end = hit.point;
            var monster = hit.collider.GetComponentInParent<MonsterHealth>();
            if (monster != null)
                monster.TakeDamage(Mathf.Max(1, item.firearmDamage) + (attack != null ? attack.DamageBonus : 0), origin);
        }
        ShotsFired++;
        ShowFeedback(origin, end);
        if (item.firearmShotSound != null) audioSource.PlayOneShot(item.firearmShotSound);
        return true;
    }

    private bool FirstHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Transform owner = health != null ? health.transform : transform;
        foreach (var hit in hits)
        {
            if (hit.transform == owner || hit.transform.IsChildOf(owner)) continue;
            result = hit;
            return true;
        }
        result = default;
        return false;
    }

    private void ShowFeedback(Vector3 origin, Vector3 end)
    {
        if (effects == null)
        {
            effects = new GameObject("Gun Shot FX " + inventory.GetHandLabel());
            effects.layer = 2;
            effectMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            effectMaterial.SetColor("_BaseColor", new Color(1f, 0.78f, 0.16f));
            tracer = effects.AddComponent<LineRenderer>();
            tracer.sharedMaterial = effectMaterial;
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.008f;
            tracer.endWidth = 0.002f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Muzzle Flash";
            flash.layer = 2;
            flash.transform.SetParent(effects.transform, false);
            var collider = flash.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = flash.GetComponent<Renderer>();
            renderer.sharedMaterial = effectMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        tracer.SetPosition(0, origin);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        flash.transform.position = origin;
        flash.transform.localScale = Vector3.one * 0.045f;
        flash.SetActive(true);
        flashUntil = Time.time + 0.035f;
        tracerUntil = Time.time + 0.065f;
    }

    private void Update()
    {
        if (flash != null && Time.time >= flashUntil) flash.SetActive(false);
        if (tracer != null && Time.time >= tracerUntil) tracer.enabled = false;
    }
    private void OnDisable()
    {
        if (flash != null) flash.SetActive(false);
        if (tracer != null) tracer.enabled = false;
        if (audioSource != null) audioSource.Stop();
    }
    private void OnDestroy()
    {
        if (effects != null) Destroy(effects);
        if (effectMaterial != null) Destroy(effectMaterial);
    }
}
