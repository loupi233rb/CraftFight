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

    [Header("手持显示")]
    [Tooltip("手持模型最长边的目标长度（米），不影响掉落物或物品栏预览。")]
    [Min(0.01f)] public float heldItemSize = 0.3f;
    public Vector3 heldItemRotation;
    public Vector3 heldItemOffset;

    [Header("枪械")]
    public bool isFirearm;
    [Min(1)] public int firearmDamage = 10;
    [Min(0.1f)] public float firearmRange = 40f;
    [Min(0.05f)] public float firearmInterval = 0.25f;
    public AudioClip firearmShotSound;

    [Header("手持碰撞")]
    [Tooltip("持有时启用预制体内的 sword_collider 子物体及其碰撞体。")]
    public bool enableHeldSwordCollider;
}
