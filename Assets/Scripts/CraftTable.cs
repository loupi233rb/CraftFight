using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CraftTable : MonoBehaviour
{
    public GameObject SlotPrefab;
    public float SlotSize = 0.15f;
    public float SlotGap = 0.075f;
    private readonly List<GameObject> slots = new();
    private CraftSlot outputSlot;
    private ItemDatabase itemDatabase;
    public int[] slotItemId = new int[9];
    public int outputItemId = 0;
    public Recipe[] recipes;
    private Dictionary<string, Recipe> shapelessRecipeDict = new();
    void Start()
    {
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        GenerateSlots();
        slotItemId = new int[9]{0, 0, 0, 0, 0, 0, 0, 0, 0};
        outputSlot = slots[9].GetComponent<CraftSlot>();
        recipes = Resources.LoadAll<Recipe>("Data/Recipe");
        foreach (var recipe in recipes)
        {
            if (recipe.recipeType == RecipeType.Shapeless)
            {
                shapelessRecipeDict[SortedKey(recipe.pattern)] = recipe;
            }
        }
    }

    string Key(int[] p) => string.Join(",", p);
    string SortedKey(int[] p)
    {
        var list = p.Where(id => id != 0).OrderBy(id => id).ToList();
        return string.Join(",", list);
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
            slot.GetComponent<CraftSlot>().isOutputSlot = false;
            slot.transform.localPosition = new Vector3((1 - (i % 3)) * (SlotSize + SlotGap),  0, (-1 + (i / 3)) * (SlotSize + SlotGap));
            slot.transform.localScale = new Vector3(SlotSize, SlotSize, SlotSize);
            slots.Add(slot);
        }
        GameObject outputSlot = Instantiate(SlotPrefab, transform);
        outputSlot.name = "OutputSlot";
        outputSlot.GetComponent<CraftSlot>().index = 9;
        outputSlot.GetComponent<CraftSlot>().isOutputSlot = true;
        outputSlot.transform.localPosition = new Vector3(- 2 * (SlotSize + SlotGap), 0, 0);
        outputSlot.transform.localScale = new Vector3(SlotSize, SlotSize, SlotSize);
        slots.Add(outputSlot);
    }

    public void SetSlotItem(int slotIndex, int itemId)
    {
        // 只有合成格子变动才会调用
        if (slotIndex < 0 || slotIndex > 8)
        {
            Debug.LogError($"Invalid slot index: {slotIndex}");
            return;
        }
        slotItemId[slotIndex] = itemId;
        // 每次有物品变动时检查
        CheckSlots();
        // 顺便更新输出槽
        if (outputItemId == 0)
        {
            outputSlot.capturedItemId = 0;
            outputSlot.capturedItemName = "";
        }
        else
        {
            outputSlot.capturedItemId = outputItemId;
            outputSlot.capturedItemName = itemDatabase.Get(outputItemId).itemName;
        }
        outputSlot.SetItemIcon();
    }

    private void CheckSlots()
    {
        shapelessRecipeDict.TryGetValue(SortedKey(slotItemId), out Recipe result);
        if (result != null)
        {
            outputItemId = result.outputItemId;
            return;
        }
        
        // 形状匹配
        foreach (var recipe in recipes)
        {
            int l = recipe.pattern.Length;
            for (int i = 0; i <= 9 - l; i++)
            {
                if (slotItemId[i] == recipe.pattern[0])
                {
                    for (int j = 0; j < 9; j++)
                    {
                        if (j < i && slotItemId[j] != 0) break;
                        if (j >=i && j < i + l && slotItemId[j] != recipe.pattern[j-i]) break;
                        if (j >= i + l && slotItemId[j] != 0) break;
                        if (j == 8)
                        {
                            outputItemId = recipe.outputItemId;
                            return;
                        }
                    }
                }
            }
        }
        outputItemId = 0;
    }
}
