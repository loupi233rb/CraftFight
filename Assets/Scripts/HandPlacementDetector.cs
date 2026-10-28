using Unity.VisualScripting;
using UnityEngine;

public enum SelectedType
{
    None,
    DroppedItem,
    CraftSlot,
    Anvil
}

/// <summary>Small palm trigger reserved for workbench slot detection.</summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class HandPlacementDetector : MonoBehaviour
{
    [SerializeField] private HandInventory inventory;
    private PlayerAudio playerAudio;
    public GameObject selectedObject;
    public SelectedType selectedType = SelectedType.None;

    public HandInventory Inventory => inventory;
    public GameObject droppedItemPrefab;

    private void Awake()
    {
        playerAudio = GetComponentInParent<PlayerAudio>();
        if (inventory == null) inventory = GetComponentInParent<HandInventory>();
        GetComponent<SphereCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (selectedObject != null)
            {
                switch (selectedType)
                {
                    case SelectedType.DroppedItem:
                        var droppedItem = selectedObject.GetComponent<DroppedItem>();
                        if (droppedItem == null)
                        {
                            Debug.LogError("Selected Dropped Item is null");
                        }
                        string itemName = droppedItem.itemName;
                        Destroy(selectedObject);
                        inventory.AddItem(itemName);
                        break;
                    case SelectedType.CraftSlot:
                        var craftSlot = selectedObject.GetComponent<CraftSlot>();
                        if (craftSlot != null)
                        {
                            // 放置物品到合成栏
                            if (craftSlot.capturedItemName == string.Empty)
                            {
                                if (inventory.RemoveItem())
                                {
                                    craftSlot.SetSlotItem(inventory.GetSelectedItemName());
                                    Debug.Log($"Placed item in Craft Slot: {craftSlot.index}");
                                }
                                else
                                {
                                    Debug.Log("No item to place in Craft Slot");
                                }
                            }
                            // 拿取物品至物品栏
                            else
                            {
                                if (inventory.GetSelectedItemName() == string.Empty)
                                {
                                    inventory.AddItem(craftSlot.capturedItemName);
                                    craftSlot.SetSlotItem(string.Empty);
                                    Debug.Log($"Removed item from Craft Slot: {craftSlot.index}");
                                }
                                else
                                {
                                    Debug.Log("Inventory already has an item, cannot remove from Craft Slot");
                                }
                            }
                            
                        }
                        break;
                    case SelectedType.Anvil:
                        var anvil = selectedObject.GetComponent<AnvilManager>();
                        if (anvil != null)
                        {
                            // 放置铁锭到铁砧
                            if (!anvil.captured)
                            {
                                if (inventory.GetSelectedItemName() == "iron_ingot")
                                {
                                    inventory.RemoveItem();
                                    anvil.captured = true;
                                    Debug.Log("Placed item in Anvil");
                                }
                                else
                                {
                                    Debug.Log("No item to place in Anvil");
                                }
                            }
                            // 拿取物品至物品栏
                            else
                            {
                                if (inventory.GetSelectedItemName() == string.Empty)
                                {
                                    if (anvil.process >= 1f)
                                    {
                                        inventory.AddItem("iron_plate");
                                        anvil.captured = false;
                                        Debug.Log("Removed iron_plate from Anvil");
                                    }
                                    else
                                    {
                                        inventory.AddItem("iron_ingot");
                                        anvil.captured = false;
                                        anvil.process = 0f;
                                        Debug.Log("Removed iron_ingot from Anvil");
                                    } 
                                }
                                else
                                {
                                    Debug.Log("Inventory already has an item, cannot remove from Anvil");
                                }
                            }
                        }
                        break;
                }
                playerAudio.PlayInteract();
            }

        }

        // 丢物品
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (inventory.GetSelectedItemName() == string.Empty)
            {
                Debug.Log("No item to drop");
            }
            else
            {
                string itemName = inventory.GetSelectedItemName();
                inventory.RemoveItem();
                if (droppedItemPrefab == null)
                {
                    Debug.LogError("DroppedItem prefab is not assigned in HandPlacementDetector");
                }
                else
                {
                    GameObject droppedItem = Instantiate(droppedItemPrefab, transform.position, Quaternion.identity);
                    droppedItem.GetComponent<DroppedItem>().Init(transform.position, Vector3.zero, itemName);
                    Debug.Log($"Dropped item: {itemName}");
                }
            }
            playerAudio.PlayInteract();
        }
    }

    private void LateUpdate()
    {
        if (inventory != null) transform.position = inventory.PlacementPosition;
    }

    private void OnDrawGizmos()
    {
        SphereCollider area = GetComponent<SphereCollider>();
        if (area == null) return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(area.center, area.radius);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    // 物品选择检测
    // 分为掉落物和合成栏
    private void OnTriggerEnter(Collider other)
    {
        if (inventory == null) return;

        if (other.gameObject.CompareTag("Item"))
        {
            var droppedItem = other.gameObject.GetComponent<DroppedItem>();
            if (!droppedItem) return;
            droppedItem.IsSelected(true);
            selectedObject = other.gameObject;
            selectedType = SelectedType.DroppedItem;
        }
        else if (other.gameObject.CompareTag("Slot"))
        {
            var slot = other.gameObject.GetComponent<CraftSlot>();
            if (!slot) return;
            slot.IsSelected(true);
            selectedObject = other.gameObject;
            selectedType = SelectedType.CraftSlot;
        }
        else if (other.gameObject.CompareTag("Anvil"))
        {
            selectedObject = other.gameObject;
            selectedType = SelectedType.Anvil;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (inventory == null) return;

        if (other.gameObject.CompareTag("Item"))
        {
            var droppedItem = other.gameObject.GetComponent<DroppedItem>();
            if (!droppedItem) return;
            droppedItem.IsSelected(false);
            selectedObject = null;
            selectedType = SelectedType.None;
        }
        else if (other.gameObject.CompareTag("Slot"))
        {
            var slot = other.gameObject.GetComponent<CraftSlot>();
            if (!slot) return;
            slot.IsSelected(false);
            selectedObject = null;
            selectedType = SelectedType.None;
        }
        else if (other.gameObject.CompareTag("Anvil"))
        {
            selectedObject = null;
            selectedType = SelectedType.None;
        }
    }
}
