using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared sword/fist contact damage. Each monster is hit once until contact ends.</summary>
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CraftXRContactDamage : MonoBehaviour
{
    [SerializeField] private SteveMeleeAttack owner;
    [SerializeField] private HandInventory inventory;
    [SerializeField] private bool emptyHandOnly;
    private Collider hitbox;
    private readonly Dictionary<Collider, MonsterHealth> contacts = new Dictionary<Collider, MonsterHealth>();
    private readonly List<Collider> expired = new List<Collider>();

    public SteveMeleeAttack Owner { get => owner; set => owner = value; }
    public bool EmptyHandOnly => emptyHandOnly;

    public void Configure(SteveMeleeAttack attack, HandInventory hand, bool fist)
    {
        owner = attack;
        inventory = hand;
        emptyHandOnly = fist;
        RefreshHitbox();
    }

    private bool Available => owner != null && owner.CanUseMelee && inventory != null &&
        inventory.IsInventoryEnabled && (!emptyHandOnly || string.IsNullOrEmpty(inventory.GetSelectedItemName()));

    protected virtual void Awake()
    {
        hitbox = GetComponent<Collider>();
        if (owner == null) owner = GetComponentInParent<SteveMeleeAttack>();
        if (inventory == null) inventory = GetComponentInParent<HandInventory>();
    }

    protected virtual void OnEnable() => contacts.Clear();

    protected virtual void LateUpdate()
    {
        RefreshHitbox();
        // Unity may omit exit callbacks when the other collider is disabled or destroyed.
        expired.Clear();
        foreach (var pair in contacts)
            if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy || pair.Value == null)
                expired.Add(pair.Key);
        foreach (var collider in expired) contacts.Remove(collider);
    }

    private void RefreshHitbox()
    {
        if (hitbox == null) hitbox = GetComponent<Collider>();
        bool available = Available;
        if (hitbox != null) hitbox.enabled = available;
        if (!available) contacts.Clear();
    }

    protected virtual void OnDisable()
    {
        contacts.Clear();
        if (hitbox != null) hitbox.enabled = false;
    }

    protected virtual void OnTriggerEnter(Collider other) => Enter(other);
    protected virtual void OnTriggerExit(Collider other) => contacts.Remove(other);
    protected virtual void OnCollisionEnter(Collision other) => Enter(other.collider);
    protected virtual void OnCollisionExit(Collision other) => contacts.Remove(other.collider);

    private void Enter(Collider other)
    {
        if (!Available || other == null || other.isTrigger || contacts.ContainsKey(other)) return;
        var monster = other.GetComponentInParent<MonsterHealth>();
        if (monster == null || monster.IsDead) return;
        bool alreadyTouching = contacts.ContainsValue(monster);
        contacts.Add(other, monster);
        // Use the player's position so a penetrating blade still knocks the monster away from the player.
        if (!alreadyTouching) owner.TryDamageMonster(monster, owner.transform.position);
    }
}
