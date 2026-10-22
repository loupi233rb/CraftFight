using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items = new();

    private Dictionary<string, ItemData> dict;
    private Dictionary<int, ItemData> idDict;

    public void Init()
    {
        dict = new Dictionary<string, ItemData>();

        foreach (var item in items)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemName))
            {
                dict[item.itemName] = item;
            }
        }
    }

    public void IdDictInit()
    {
        idDict = new Dictionary<int, ItemData>();

        foreach (var item in items)
        {
            if (item != null)
            {
                idDict[item.itemId] = item;
            }
        }
    }

    public ItemData Get(string itemName)
    {
        if (dict == null) Init();
        dict.TryGetValue(itemName, out var item);
        return item;
    }

    public ItemData Get(int itemId)
    {
        if (idDict == null) IdDictInit();
        idDict.TryGetValue(itemId, out var item);
        return item;
    }
}