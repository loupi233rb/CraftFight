using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Game/Item")]
public class ItemData : ScriptableObject
{
    [Header("基础信息")]
    public int itemId;
    public string itemName;
    public string displayName;

    [Header("物品属性")]
    public float attckPower;
    public GameObject worldPrefab;
}