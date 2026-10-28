using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public string itemName = "Item";
    public int maxCount = 1;
    public int leftCount = 0;
    public TextMeshProUGUI infoText;
    public GameObject droppedItemPrefab;
    public int _countInZone = 0;

    private ItemDatabase itemDatabase;
    private GameObject itemIcon;
    private readonly Vector3 itemIconPos = new Vector3(0, 0.85f, 0);
    private Vector3 spawnPos;
    private const float SpawnInterval = 1f;
    private float lastSpawnTime;
    private bool initialized;
    private bool roundManaged;
    private bool issuingResources = true;
    private readonly List<DroppedItem> producedItems = new List<DroppedItem>();
    private readonly HashSet<DroppedItem> itemsInZone = new HashSet<DroppedItem>();

    private void Start()
    {
        EnsureInitialized();
        if (!roundManaged) leftCount = maxCount;
        UpdateLabel();
    }

    public void ValidateForRound()
    {
        if (droppedItemPrefab == null || droppedItemPrefab.GetComponent<DroppedItem>() == null)
            throw new InvalidOperationException(name + " needs a DroppedItem prefab.");
        if (transform.Find("spawn_point") == null)
            throw new InvalidOperationException(name + " needs a spawn_point.");
        ItemDatabase database = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        if (database == null || database.Get(itemName) == null)
            throw new InvalidOperationException("Unknown resource item: " + itemName);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        ValidateForRound();
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        GameObject itemPrefab = itemDatabase.Get(itemName).worldPrefab;
        if (itemPrefab != null)
        {
            itemIcon = Instantiate(itemPrefab, transform);
            itemIcon.transform.SetLocalPositionAndRotation(itemIconPos, Quaternion.identity);
        }
        spawnPos = transform.Find("spawn_point").position;
        initialized = true;
    }

    public void UseRoundQuotas()
    {
        roundManaged = true;
        issuingResources = false;
        maxCount = leftCount = 0;
        UpdateLabel();
    }

    public void SetRoundQuota(int count)
    {
        EnsureInitialized();
        // Picked-up items were destroyed by the inventory and are no longer in this list.
        foreach (DroppedItem item in producedItems)
        {
            if (item == null) continue;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        producedItems.Clear();
        itemsInZone.Clear();
        _countInZone = 0;
        roundManaged = true;
        issuingResources = true;
        maxCount = leftCount = Mathf.Max(0, count);
        lastSpawnTime = Time.time - SpawnInterval;
        UpdateLabel();
    }

    public void StopRoundGeneration() => issuingResources = false;

    private void Update()
    {
        itemsInZone.RemoveWhere(item => item == null || !item.gameObject.activeInHierarchy);
        _countInZone = itemsInZone.Count;
        if (initialized && issuingResources && leftCount > 0 && _countInZone == 0 &&
            Time.time - lastSpawnTime >= SpawnInterval)
        {
            GameObject instance = Instantiate(droppedItemPrefab, transform);
            DroppedItem item = instance.GetComponent<DroppedItem>();
            item.Init(spawnPos, Vector3.zero, itemName);
            producedItems.Add(item);
            lastSpawnTime = Time.time;
            leftCount--;
        }
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (infoText != null) infoText.SetText($"{leftCount}/{maxCount}");
    }

    public void TriggerEnter(Collider other)
    {
        if (!other.CompareTag("Item")) return;
        DroppedItem item = other.GetComponentInParent<DroppedItem>();
        if (item != null && item.itemName == itemName) itemsInZone.Add(item);
        _countInZone = itemsInZone.Count;
    }

    public void TriggerExit(Collider other)
    {
        if (!other.CompareTag("Item")) return;
        DroppedItem item = other.GetComponentInParent<DroppedItem>();
        if (item != null) itemsInZone.Remove(item);
        _countInZone = itemsInZone.Count;
    }
}
