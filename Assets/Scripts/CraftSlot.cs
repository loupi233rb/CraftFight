using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftSlot : MonoBehaviour
{
    public int index;
    public int capturedItemId = 0;
    public string capturedItemName = string.Empty;
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
        // ***检测方法不使用掉落物而改用手部检测点
        // 非输出格子为空才会检测新物品
        // if (other.gameObject.CompareTag("Item") && !isOutputSlot && capturedItemId == 0)
        // {
        //     var droppedItem = other.gameObject.GetComponentInParent<DroppedItem>();
        //     if (!droppedItem)
        //     {
        //         Debug.LogError("DroppedItem component not found on the parent of the collided object.");
        //         return;
        //     };
        //     capturedItemId = droppedItem.itemId;
        //     capturedItemName = droppedItem.itemName;
        //     craftTable.SetSlotItem(index, capturedItemId);
        //     SetItemIcon();
        //     Debug.Log($"Slot {index} captured item {droppedItem.itemName}");
        //     return;
        // }

        // 手部检测点进入
        if (other.gameObject.CompareTag("Player"))
        {
            // 更新外观 发光

            handInArea = true;
        }
        Debug.Log("Entered Slot Area");
    }

    private void OnTriggerExit(Collider other)
    {
       if (other.gameObject.CompareTag("Item") && !isOutputSlot)
        {
            var droppedItem = other.gameObject.GetComponentInParent<DroppedItem>();
            if (!droppedItem)
            {
                Debug.LogError("DroppedItem component not found on the parent of the collided object.");
                return;
            };
            capturedItemId = 0;
            capturedItemName = string.Empty;
            craftTable.SetSlotItem(index, capturedItemId);
            SetItemIcon();
            Debug.Log($"Slot {index} cleared item {droppedItem.itemName}");
            return;
        }

        // 手部检测点退出
        if (other.gameObject.CompareTag("Player"))
        {
            // 更新外观 不发光

            handInArea = false;
        }
        Debug.Log("Exited Slot Area");
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

    public void IsSelected(bool selected)
    {
        // 处理物品被选中的逻辑
    }

    public void SetSlotItem(string itemName)
    {
        var itemData = itemDatabase.Get(itemName);
        if (itemData == null) return;
        capturedItemId = itemData.itemId;
        capturedItemName = itemData.itemName;
        craftTable.SetSlotItem(index, capturedItemId);
        SetItemIcon();
        Debug.Log($"Slot {index} captured item {itemData.itemName}");
    }
}
