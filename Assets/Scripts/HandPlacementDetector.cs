using UnityEngine;
using System.Collections.Generic;

/// <summary>Shared, transactional hand interactions for desktop and XR controllers.</summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class HandPlacementDetector : MonoBehaviour
{
    public enum SelectedType { None, DroppedItem, CraftSlot, Anvil, CraftButton, Trader }
    public enum Hand { Left, Right }
    [SerializeField] private HandInventory inventory;
    private KeyCode interactKey;
    private KeyCode dropKey;
    private PlayerAudio playerAudio;
    private SteveHealth health;
    private PlayerEconomy economy;
    private SteveFirstPersonController movement;
    private readonly HashSet<Collider> nearby = new HashSet<Collider>();
    public GameObject selectedObject;
    public SelectedType selectedType = SelectedType.None;
    public HandInventory Inventory => inventory;
    public GameObject droppedItemPrefab;

    private void Awake()
    {
        playerAudio = GetComponentInParent<PlayerAudio>();
        health = GetComponentInParent<SteveHealth>();
        economy = GetComponentInParent<PlayerEconomy>();
        movement = GetComponentInParent<SteveFirstPersonController>();
        if (inventory == null) inventory = GetComponentInParent<HandInventory>();
        bool left = inventory != null && inventory.GetHandLabel() == "LEFT";
        // 分别为左手的逗号和分号，右手的句号和单引号。
        interactKey = left ? KeyCode.Comma : KeyCode.Period;
        dropKey = left ? KeyCode.Semicolon : KeyCode.Quote;
        GetComponent<SphereCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
    }

    private bool CanInteract => inventory != null && inventory.IsInventoryEnabled &&
        (health == null || !health.IsDead) &&
        (economy == null || !economy.BlocksGameplayInput) &&
        (movement == null || !movement.IsMovementSuspended);

    private void Update()
    {
        if (CraftXRPlayer.Active != null) return;
        if (Input.GetKeyDown(interactKey)) TryInteract();
        if (Input.GetKeyDown(dropKey)) TryDrop();
    }

    public void SetSelection(GameObject target)
    {
        if (selectedObject == target) return;
        Highlight(selectedObject, false);
        selectedObject = target;
        selectedType = SelectedType.None;
        if (target != null)
        {
            if (target.GetComponent<DroppedItem>() != null) selectedType = SelectedType.DroppedItem;
            else if (target.GetComponent<CraftSlot>() != null) selectedType = SelectedType.CraftSlot;
            else if (target.GetComponent<AnvilManager>() != null) selectedType = SelectedType.Anvil;
            else if (target.GetComponent<global::Button>() != null) selectedType = SelectedType.CraftButton;
            else if (target.GetComponent<VillagerTrader>() != null) selectedType = SelectedType.Trader;
        }
        Highlight(selectedObject, true);
    }

    private static void Highlight(GameObject target, bool selected)
    {
        if (target == null) return;
        var item = target.GetComponent<DroppedItem>();
        if (item != null) { if (selected) item.OnSelect(); else item.OffSelect(); }
        var slot = target.GetComponent<CraftSlot>();
        if (slot != null) { if (selected) slot.OnSelect(); else slot.OffSelect(); }
    }

    public bool TryInteract()
    {
        if (!CanInteract || selectedObject == null) return false;
        bool success = false;
        switch (selectedType)
        {
            case SelectedType.DroppedItem:
                var item = selectedObject.GetComponent<DroppedItem>();
                if (item != null && item.gameObject.activeInHierarchy && inventory.AddItem(item.itemName))
                {
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                    SetSelection(null);
                    success = true;
                }
                break;
            case SelectedType.CraftSlot:
                var slot = selectedObject.GetComponent<CraftSlot>();
                if (slot == null) break;
                if (!string.IsNullOrEmpty(slot.capturedItemName))
                {
                    if (inventory.AddItem(slot.capturedItemName))
                    {
                        slot.SetSlotItem(string.Empty);
                        success = true;
                    }
                }
                else if (!slot.isOutputSlot)
                {
                    // Cache the item name before removing it from the hand.
                    string name = inventory.GetSelectedItemName();
                    if (!string.IsNullOrEmpty(name) && inventory.RemoveItem())
                    {
                        slot.SetSlotItem(name);
                        success = true;
                    }
                }
                break;
            case SelectedType.Anvil:
                var anvil = selectedObject.GetComponent<AnvilManager>();
                if (anvil == null) break;
                if (!anvil.captured && inventory.GetSelectedItemName() == "iron_ingot" && inventory.RemoveItem())
                {
                    anvil.SlotUpdate(true);
                    success = true;
                }
                else if (anvil.captured && inventory.AddItem(anvil.process >= 1f ? "iron_plate" : "iron_ingot"))
                {
                    anvil.SlotUpdate(false);
                    success = true;
                }
                break;
            case SelectedType.CraftButton:
                success = selectedObject.GetComponent<global::Button>().Press();
                break;
            case SelectedType.Trader:
                success = economy != null && economy.OpenTrade(selectedObject.GetComponent<VillagerTrader>());
                break;
        }
        if (success && playerAudio != null) playerAudio.PlayInteract();
        return success;
    }

    public bool TryDrop()
    {
        if (!CanInteract || droppedItemPrefab == null ||
            droppedItemPrefab.GetComponent<DroppedItem>() == null) return false;
        string itemName = inventory.GetSelectedItemName();
        if (string.IsNullOrEmpty(itemName)) return false;
        GameObject instance = Instantiate(droppedItemPrefab, transform.position, Quaternion.identity);
        instance.GetComponent<DroppedItem>().Init(transform.position, Vector3.zero, itemName);
        if (!inventory.RemoveItem()) { Destroy(instance); return false; }
        if (playerAudio != null) playerAudio.PlayInteract();
        return true;
    }

    private void LateUpdate()
    {
        if (inventory != null) transform.position = inventory.PlacementPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (CraftXRPlayer.Active != null) return;
        nearby.Add(other);
        SelectNearest();
    }
    private void OnTriggerExit(Collider other)
    {
        if (CraftXRPlayer.Active != null) return;
        nearby.Remove(other);
        SelectNearest();
    }
    private void SelectNearest()
    {
        GameObject best = null;
        float distance = float.PositiveInfinity;
        nearby.RemoveWhere(c => c == null);
        foreach (var collider in nearby)
        {
            GameObject target = null;
            if (collider.GetComponent<DroppedItem>() != null || collider.GetComponent<CraftSlot>() != null ||
                collider.GetComponent<AnvilManager>() != null) target = collider.gameObject;
            if (target == null) continue;
            float d = (target.transform.position - transform.position).sqrMagnitude;
            if (d < distance) { best = target; distance = d; }
        }
        SetSelection(best);
    }
    private void OnDisable() => SetSelection(null);
}
