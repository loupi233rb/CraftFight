using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnvilManager : MonoBehaviour
{
    private ItemDatabase itemDatabase;
    private GameObject ingot;
    private GameObject plate;
    [InspectorName("是否捕获铁锭")] public bool captured = false;
    [InspectorName("加工进度0-1")] public float process = 0f;
    [InspectorName("加工最大速度")] public float maxSpeed = 20f;
    [InspectorName("最少加工次数")] public int minCount = 5;
    void Start()
    {
        itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
        ingot = itemDatabase.Get("iron_ingot").worldPrefab;
        plate = itemDatabase.Get("iron_plate").worldPrefab;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Item")) return;
        var droppedItem = other.GetComponentInParent<DroppedItem>();
        if (droppedItem.itemName != "iron_ingot") return;

        captured = true;
        process = 0f;
    }

    public void SetIcon()
    {
        
    }

    public void Process(float velocity)
    {
        if (velocity <= 0f) return;
        if (process >= 1f) return;
        float amount = velocity<maxSpeed ? velocity/maxSpeed : 1f;
        amount = amount<1/minCount ? amount : 1/minCount;
        process += amount;
        if (process >= 1f)
        {
            process = 1f;
            SetIcon();
        }
    }
}
