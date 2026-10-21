using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public string itemName = "Item";
    public int maxCount = 1;
    public int leftCount = 0;
    public TextMeshProUGUI infoText;
    public GameObject droppedItemPrefab;
    private ItemDatabase itemDatabase;
    private GameObject itemIcon;
    private Vector3 itemIconPos = new(0, 0.85f, 0);
    private Vector3 spawnPos = new(0, 1f, 0);
    public int _countInZone = 0;
    private float _spawnInterval = 1f;
    private float _lastSpawnTime = 0f;
    void Start()
    {
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        if (itemDatabase == null)
        {
            Debug.LogError("ItemDatabase not found in Resources/Data/ItemDatabase");
            return;
        }
        GameObject itemPrefab = itemDatabase.Get(itemName).worldPrefab;
        itemIcon = Instantiate(itemPrefab, transform);
        itemIcon.transform.SetLocalPositionAndRotation(itemIconPos, Quaternion.identity);
        leftCount = maxCount;
        spawnPos = transform.Find("spawn_point").GetComponent<Transform>().position;
    }

    // Update is called once per frame
    void Update()
    {
        if (infoText == null) Debug.LogError("infoText is null");
        infoText.SetText($"{leftCount}/{maxCount}");

        if (leftCount > 0 && _countInZone <= 0)
        {
            if (Time.time - _lastSpawnTime >= _spawnInterval)
            {
                Spawn();
                _lastSpawnTime = Time.time;
                leftCount--;
            }
        }
    }

    public void TriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Item"))
        {
            return;
        }
        var droppedItem = other.gameObject.transform.parent.GetComponent<DroppedItem>();
        if (!droppedItem) return;
        if (droppedItem.itemName == itemName)
        {
            _countInZone++;
            Debug.Log($"物品 {droppedItem.itemName} 进入生成器 {itemName}，当前数量 {_countInZone}");
        }
    }

    private void Spawn()
    {
        GameObject droppedItem = Instantiate(droppedItemPrefab, transform);
        droppedItem.GetComponent<DroppedItem>().Init(spawnPos, Vector3.zero, itemName);
    }

    public void TriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Item"))
        {
            Debug.Log("离开物品标签不匹配");
            return;
        }
        var droppedItem = other.gameObject.transform.parent.GetComponent<DroppedItem>();
        if (!droppedItem)
        {
            Debug.Log("离开物品没有DroppedItem组件");
            return;
        }
        if (droppedItem.itemName == itemName)
        {
            _countInZone--;
            Debug.Log($"物品 {droppedItem.itemName} 离开生成器 {itemName}，当前数量 {_countInZone}");
        }
    }
}
