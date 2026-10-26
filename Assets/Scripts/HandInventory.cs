using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Three independent slots, wrist previews, and the selected item held in the hand.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class HandInventory : MonoBehaviour
{
    public const int Capacity = 3;

    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private string[] itemNames = new string[Capacity];
    [SerializeField, Range(0, Capacity - 1)] private int selectedIndex;
    [SerializeField] private string handLabel = "LEFT";
    [SerializeField] private Transform viewCamera;
    [SerializeField] private Vector3 panelOffset = new Vector3(0f, -0.1f, -0.24f);
    [SerializeField] private Vector3 panelRotation;
    [SerializeField, Range(0.25f, 2f)] private float panelScale = 0.85f;
    [SerializeField] private Material boardMaterial;
    [SerializeField] private Material slotMaterial;
    [SerializeField] private Material selectedMaterial;

    [Header("Hand-held item")]
    [SerializeField] private Vector3 heldItemOffset = new Vector3(0f, 0f, -0.08f);
    [SerializeField] private Vector3 heldItemRotation;
    [SerializeField, Min(0.01f)] private float heldItemScale = 1f;

    private Transform panel;
    private Transform heldItemRoot;
    private GameObject heldItem;
    private ItemData heldItemData;
    private Vector3 heldPrefabScale;
    private float heldPrefabSize;
    private string heldItemName;
    private bool initialized;
    private readonly Transform[] previews = new Transform[Capacity];
    private readonly MeshRenderer[] frames = new MeshRenderer[Capacity];
    private readonly TextMesh[] numbers = new TextMesh[Capacity];
    private readonly Dictionary<Material, Material> previewMaterials = new Dictionary<Material, Material>();

    public int SelectedIndex => selectedIndex;
    public Vector3 PlacementPosition => heldItem != null && heldItemRoot != null
        ? heldItemRoot.position : transform.TransformPoint(heldItemOffset);

    private void Awake() => ValidateSlots();

    private void Start()
    {
        if (itemDatabase == null)
        {
            Debug.LogError("Assign Assets/Resources/Data/ItemDatabase.asset to the hand inventory.", this);
            return;
        }
        itemDatabase.Init();
        if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
        BuildView();
        initialized = true;
        RefreshHeldItem();
    }

    private void OnValidate() => ValidateSlots();

    private void ValidateSlots()
    {
        if (itemNames == null) itemNames = new string[Capacity];
        else if (itemNames.Length != Capacity) Array.Resize(ref itemNames, Capacity);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Capacity - 1);
    }

    public void Cycle(int direction)
    {
        int next = (((selectedIndex + direction) % Capacity) + Capacity) % Capacity;
        if (selectedIndex == next) return;
        selectedIndex = next;
        PlayerAudio playerAudio = GetComponentInParent<PlayerAudio>();
        if (playerAudio != null) playerAudio.PlaySelection();
        if (panel != null) UpdateHighlight();
        if (initialized) RefreshHeldItem();
    }

    private void LateUpdate()
    {
        // The panel stays on the wrist; its face remains readable from the player's head.
        // Position and item selection never depend on whether the arm is raised.
        if (panel != null && viewCamera != null)
            panel.rotation = viewCamera.rotation * Quaternion.Euler(panelRotation);

        if (initialized) RefreshHeldItem();
        ApplyHeldItemPose();
    }

    private void RefreshHeldItem()
    {
        string selectedName = itemNames[selectedIndex] ?? string.Empty;
        if (heldItemName == selectedName) return;
        heldItemName = selectedName;
        heldItemData = null;

        if (heldItem != null)
        {
            // Hide immediately so switching several times in one frame never shows two items.
            heldItem.SetActive(false);
            Destroy(heldItem);
            heldItem = null;
        }
        if (string.IsNullOrEmpty(selectedName)) return;

        ItemData item = itemDatabase.Get(selectedName);
        if (item == null || item.worldPrefab == null)
        {
            Debug.LogWarning("No held-item prefab found for: " + selectedName, this);
            return;
        }

        if (heldItemRoot == null)
        {
            heldItemRoot = new GameObject("HeldItem").transform;
            heldItemRoot.SetParent(transform, false);
            heldItemRoot.localPosition = heldItemOffset;
            heldItemRoot.localRotation = Quaternion.Euler(heldItemRotation);
            heldItemRoot.localScale = Vector3.one * heldItemScale;
        }

        // Use the same original prefab as the inventory slot, without miniature scaling/materials.
        heldItem = SafeInstance(item.worldPrefab, heldItemRoot, false);
        heldItem.name = selectedName + " (Held)";
        heldItemData = item;
        heldPrefabScale = heldItem.transform.localScale;
        AlignHeldItemGrip(heldItem.transform);
        heldPrefabSize = TryGetLocalBounds(heldItem, heldItemRoot, out Bounds bounds)
            ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 0f;
        ApplyHeldItemPose();
        heldItem.SetActive(true);
        if (item.enableHeldSwordCollider) EnableHeldSwordCollider();
    }

    private void ApplyHeldItemPose()
    {
        if (heldItemRoot == null || heldItem == null || heldItemData == null) return;

        heldItemRoot.localPosition = heldItemOffset + heldItemData.heldItemOffset;
        heldItemRoot.localRotation = Quaternion.Euler(heldItemRotation) *
            Quaternion.Euler(heldItemData.heldItemRotation);
        heldItemRoot.localScale = Vector3.one * Mathf.Max(0.01f, heldItemScale);

        Transform model = heldItem.transform;
        float sizeMultiplier = heldPrefabSize > 0.00001f
            ? Mathf.Max(0.01f, heldItemData.heldItemSize) / heldPrefabSize : 1f;
        model.localScale = heldPrefabScale * sizeMultiplier;
        AlignHeldItemGrip(model);
    }

    private void EnableHeldSwordCollider()
    {
        Transform colliderRoot = heldItem.transform.Find("sword_collider");
        if (colliderRoot == null)
        {
            Debug.LogWarning("Held sword has no sword_collider child: " + heldItemName, this);
            return;
        }

        colliderRoot.gameObject.SetActive(true);
        SteveHealth owner = GetComponentInParent<SteveHealth>();
        Collider[] ownerColliders = owner != null
            ? owner.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
        foreach (var swordCollider in colliderRoot.GetComponentsInChildren<Collider>(true))
        {
            swordCollider.gameObject.SetActive(true);
            swordCollider.enabled = true;
            Rigidbody body = swordCollider.attachedRigidbody;
            if (body != null) body.detectCollisions = true;
            foreach (var ownerCollider in ownerColliders)
            {
                if (ownerCollider.enabled && ownerCollider.gameObject.activeInHierarchy &&
                    !ownerCollider.transform.IsChildOf(heldItem.transform))
                    Physics.IgnoreCollision(swordCollider, ownerCollider, true);
            }
        }
    }

    private static void AlignHeldItemGrip(Transform model)
    {
        Transform grip = model.Find("hand");
        if (grip != null)
        {
            // Align the prefab's authored grip with the palm, preserving its root scale.
            Vector3 gripPosition = grip.localPosition;
            Quaternion gripRotation = grip.localRotation;
            model.localRotation = Quaternion.Inverse(gripRotation);
            model.localPosition = -(model.localRotation * Vector3.Scale(model.localScale, gripPosition));
        }
        else
        {
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
        }
    }

    private void BuildView()
    {
        if (boardMaterial == null || slotMaterial == null || selectedMaterial == null)
        {
            Debug.LogError("Hand inventory display materials are missing.", this);
            return;
        }

        panel = new GameObject("InventoryPanel").transform;
        panel.SetParent(transform, false);
        panel.localPosition = panelOffset;
        panel.localScale = Vector3.one * panelScale;
        Cube("Board", panel, Vector3.zero, new Vector3(0.326f, 0.145f, 0.016f), boardMaterial);
        MakeText(handLabel, panel, new Vector3(-0.145f, 0.057f, -0.016f), 0.0028f, TextAnchor.UpperLeft);

        for (int i = 0; i < Capacity; i++)
        {
            float x = (i - 1) * 0.102f;
            frames[i] = Cube("Slot" + (i + 1), panel, new Vector3(x, -0.005f, -0.013f),
                new Vector3(0.094f, 0.094f, 0.008f), slotMaterial);
            Cube("Inset", frames[i].transform, new Vector3(0f, 0f, -0.75f),
                new Vector3(0.91f, 0.91f, 0.5f), boardMaterial);
            previews[i] = new GameObject("Preview" + (i + 1)).transform;
            previews[i].SetParent(panel, false);
            previews[i].localPosition = new Vector3(x, 0f, -0.035f);
            numbers[i] = MakeText((i + 1).ToString(), panel,
                new Vector3(x, -0.057f, -0.02f), 0.0035f, TextAnchor.MiddleCenter);
            CreatePreview(i);
        }
        UpdateHighlight();
    }

    private void CreatePreview(int index)
    {
        Transform root = previews[index];
        string itemName = itemNames[index];
        if (string.IsNullOrEmpty(itemName)) return;
        ItemData item = itemDatabase.Get(itemName);
        if (item == null)
        {
            Debug.LogWarning("ItemDatabase does not contain: " + itemName, this);
            return;
        }
        if (item.worldPrefab == null) return;

        GameObject model = SafeInstance(item.worldPrefab, root);
        MakePreviewUnlit(model);
        model.name = item.name + " (Preview)";
        model.transform.localRotation = Quaternion.Euler(0f, 20f, -18f);
        if (TryGetLocalBounds(model, root, out Bounds bounds))
        {
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = longest > 0.00001f ? 0.056f / longest : 1f;
            model.transform.localScale *= scale;
            model.transform.localPosition -= bounds.center * scale;
        }
        model.SetActive(true);
    }

    private void MakePreviewUnlit(GameObject model)
    {
        // Preserve the original textures, but make miniature previews legible in shadows.
        // Only these preview instances receive replacement materials.
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i];
                if (original == null) continue;
                if (!previewMaterials.TryGetValue(original, out Material unlit))
                {
                    unlit = new Material(boardMaterial) { name = original.name + " (Inventory Preview)" };
                    Texture texture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
                    Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
                    unlit.SetTexture("_BaseMap", texture);
                    unlit.SetColor("_BaseColor", color);
                    previewMaterials.Add(original, unlit);
                }
                materials[i] = unlit;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private void OnDestroy()
    {
        foreach (var material in previewMaterials.Values) Destroy(material);
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < Capacity; i++)
        {
            bool selected = i == selectedIndex;
            frames[i].sharedMaterial = selected ? selectedMaterial : slotMaterial;
            numbers[i].color = selected ? new Color(1f, 0.85f, 0.25f) : Color.white;
            previews[i].localScale = Vector3.one * (selected ? 1.08f : 1f);
        }
    }

    private static GameObject SafeInstance(GameObject prefab, Transform parent, bool preview = true)
    {
        // Instantiate under an inactive holder so physics/scripts cannot run before sanitizing.
        var staging = new GameObject("Staging");
        staging.SetActive(false);
        staging.transform.SetParent(parent, false);
        GameObject instance = Instantiate(prefab, staging.transform, false);
        instance.SetActive(false);
        foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }
        foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        if (preview)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
        instance.transform.SetParent(parent, false);
        Destroy(staging);
        return instance;
    }

    private static bool TryGetLocalBounds(GameObject model, Transform relativeTo, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Bounds source = renderer.localBounds;
            Matrix4x4 matrix = relativeTo.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = source.center + Vector3.Scale(source.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (found) bounds.Encapsulate(point);
                else { bounds = new Bounds(point, Vector3.zero); found = true; }
            }
        }
        return found;
    }

    private static MeshRenderer Cube(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        var collider = cube.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = cube.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static TextMesh MakeText(string text, Transform parent, Vector3 position, float size, TextAnchor anchor)
    {
        var label = new GameObject("Label " + text);
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        var mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = anchor;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 48;
        mesh.characterSize = size;
        mesh.color = Color.white;
        var renderer = label.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return mesh;
    }
}
