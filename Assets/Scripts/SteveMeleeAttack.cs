using UnityEngine;

[RequireComponent(typeof(SteveHealth))]
public sealed class SteveMeleeAttack : MonoBehaviour
{
    [SerializeField, Min(0f)] private float attackRange = 3f;
    [SerializeField, Min(0f)] private float attackInterval = 0.625f;
    [SerializeField, Min(1)] private int attackDamage = 6;
    private SteveHealth health;
    private Camera viewCamera;
    private float nextAttackTime;
    private int initialAttackDamage;
    private PlayerEconomy economy;
    private SteveFirstPersonController movement;
    public int AttackDamage => attackDamage;
    public int DamageBonus => Mathf.Max(0, attackDamage - initialAttackDamage);

    public void IncreaseAttackDamage(int amount) { if (amount > 0) attackDamage += amount; }
    private void Awake()
    {
        health = GetComponent<SteveHealth>();
        initialAttackDamage = attackDamage;
        economy = GetComponent<PlayerEconomy>();
        movement = GetComponent<SteveFirstPersonController>();
        viewCamera = Camera.main;
    }
    public bool CanUseMelee => health != null && !health.IsDead &&
        (movement == null || !movement.IsMovementSuspended) &&
        (economy == null || !economy.BlocksGameplayInput);
    private bool CanAttack => CanUseMelee && Time.time >= nextAttackTime;

    private void Update()
    {
        if (CraftXRPlayer.Active != null) return;
        if (Input.GetMouseButtonDown(0) && viewCamera != null) TryAttackFrom(viewCamera.transform);
    }

    public bool TryAttackFrom(Transform source)
    {
        if (CraftXRPlayer.Active != null || !CanAttack || source == null) return false;
        nextAttackTime = Time.time + attackInterval;
        RaycastHit[] hits = Physics.RaycastAll(source.position, source.forward, attackRange, ~0,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            MonsterHealth monster = hit.collider.GetComponentInParent<MonsterHealth>();
            if (monster != null)
            {
                monster.TakeDamage(attackDamage, source.position);
                return true;
            }
            break;
        }
        return false;
    }

    public bool TryDamageMonster(MonsterHealth monster, Vector3 source)
    {
        if (!CanAttack || monster == null || monster.IsDead) return false;
        nextAttackTime = Time.time + attackInterval;
        monster.TakeDamage(attackDamage, source);
        return true;
    }

    private void OnGUI()
    {
        if (CraftXRPlayer.Active != null) return;
        if (!health.IsDead && (economy == null || !economy.BlocksGameplayInput))
            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
    }
}
