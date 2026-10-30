using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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
    private GameObject selectedBox;
    void Start()
    {
        selectedBox = transform.Find("selected").GameObject();
        selectedBox.SetActive(false);
        craftTable = GetComponentInParent<CraftTable>();
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // 检测交给手部检测点来实现
    // private void OnTriggerEnter(Collider other){}
    // private void OnTriggerExit(Collider other) {}


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

    public void OnSelect()
    {
        // 处理物品被选中的逻辑
        selectedBox.SetActive(true);
    }

    public void OffSelect()
    {
        // 处理物品取消选中的逻辑
        selectedBox.SetActive(false);
    }

    public void SetSlotItem(string itemName)
    {
        if (itemName != string.Empty)
        {
            var itemData = itemDatabase.Get(itemName);
            if (itemData == null) return;
            capturedItemId = itemData.itemId;
            capturedItemName = itemData.itemName;
        }
        else
        {
            capturedItemId = 0;
            capturedItemName = string.Empty;
        }
        craftTable.SetSlotItem(index, capturedItemId);
        SetItemIcon();
        Debug.Log($"Slot {index} is set to {(itemName==string.Empty?"empty":capturedItemName)}");
    }
}
