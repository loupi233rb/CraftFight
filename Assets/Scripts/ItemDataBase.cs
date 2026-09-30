using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items = new();

    private Dictionary<string, ItemData> dict;

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

    public ItemData Get(string itemName)
    {
        if (dict == null) Init();
        dict.TryGetValue(itemName, out var item);
        return item;
    }
}