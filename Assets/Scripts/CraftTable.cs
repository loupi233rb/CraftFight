using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftTable : MonoBehaviour
{
    public GameObject SlotPrefab;
    public float SlotSize = 0.15f;
    public float SlotGap = 0.075f;
    private readonly List<GameObject> slots = new();
    private int[] slotItems = new int[10];
    // Start is called before the first frame update
    void Start()
    {
        GenerateSlots();
        slotItems = new int[10]{0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void GenerateSlots()
    {
        for (int i = 0; i < 9; i++)
        {
            GameObject slot = Instantiate(SlotPrefab, transform);
            slot.name = "Slot" + i;
            slot.GetComponent<CraftSlot>().index = i;
            slot.transform.localPosition = new Vector3((1 - (i / 3)) * (SlotSize + SlotGap),  0, (1 - (i % 3)) * (SlotSize + SlotGap));
            slot.transform.localScale = new Vector3(SlotSize, SlotSize, SlotSize);
            slots.Add(slot);
        }
        GameObject outputSlot = Instantiate(SlotPrefab, transform);
        outputSlot.name = "OutputSlot";
        outputSlot.GetComponent<CraftSlot>().index = 9;
        outputSlot.transform.localPosition = new Vector3(- 2 * (SlotSize + SlotGap), 0, 0);
        outputSlot.transform.localScale = new Vector3(SlotSize, SlotSize, SlotSize);
        slots.Add(outputSlot);
    }

    public void SetSlotItem(int slotIndex, int itemId)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            Debug.LogError($"Invalid slot index: {slotIndex}");
            return;
        }
        slotItems[slotIndex] = itemId;
    }
}
