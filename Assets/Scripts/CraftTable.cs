using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftTable : MonoBehaviour
{
    public float SlotSize = 0.15f;
    public float SlotGap = 0.075f;
    private List<GameObject> slots = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void GenerateSlots()
    {
        for (int i = 0; i < 9; i++)
        {
            GameObject slot = new GameObject("Slot" + i);
            slot.transform.SetParent(transform);
            slot.transform.localPosition = new Vector3((i % 3) * (SlotSize + SlotGap), (i / 3) * (SlotSize + SlotGap), 0);
            slot.transform.localScale = new Vector3(SlotSize, SlotSize, 1);
            slots.Add(slot);
        }
    }
}
