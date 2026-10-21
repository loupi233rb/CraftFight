using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftSlot : MonoBehaviour
{
    public int index;
    public int capturedItemId = -1;
    public string capturedItemName = "";
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

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Item"))
        {
            var droppedItem = other.gameObject.transform.parent.GetComponent<DroppedItem>();
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

    private void SetItemIcon()
    {
        GameObject itemPrefab = itemDatabase.Get(capturedItemName).worldPrefab;
        if (itemIcon != null)
        {
            itemIcon = Instantiate(itemPrefab, transform);
            itemIcon.transform.localPosition = Vector3.zero;
            itemIcon.transform.localScale = Vector3.one * 0.7f;
        }
    }
}
