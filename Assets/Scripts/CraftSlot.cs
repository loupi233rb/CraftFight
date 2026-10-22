using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftSlot : MonoBehaviour
{
    public int index;
    public int capturedItemId = -1;
    public string capturedItemName = "";
    public bool isOutputSlot = false;
    private ItemDatabase itemDatabase;
    private GameObject itemIcon;
    private CraftTable craftTable;
    private bool handInArea = false;
    void Start()
    {
        craftTable = GetComponentInParent<CraftTable>();
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        // 非输出格子才会检测
        if (other.gameObject.CompareTag("Item") && !isOutputSlot)
        {
            var droppedItem = other.gameObject.GetComponentInParent<DroppedItem>();
            if (!droppedItem)
            {
                Debug.LogError("DroppedItem component not found on the parent of the collided object.");
                return;
            };
            capturedItemId = droppedItem.itemId;
            capturedItemName = droppedItem.itemName;
            craftTable.SetSlotItem(index, capturedItemId);
            SetItemIcon();
            Debug.Log($"Slot {index} captured item {droppedItem.itemName}");
            return;
        }
        Debug.Log("Entered Slot Area");
    }

    public void SetItemIcon()
    {
        if (capturedItemId <= 0)
        {
            if (itemIcon != null)
            {
                Destroy(itemIcon);
                itemIcon = null;
            }
            return;
        }
        GameObject itemPrefab = itemDatabase.Get(capturedItemName).worldPrefab;
        itemIcon = Instantiate(itemPrefab, transform);
        itemIcon.transform.localPosition = new Vector3(0, 0.8f, 0);
        itemIcon.transform.localScale = Vector3.one / 0.15f * 0.75f;
        Debug.Log($"槽{index} 图标实例化成功");
    }
}
