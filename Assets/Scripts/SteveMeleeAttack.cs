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

    private void Awake()
    {
        health = GetComponent<SteveHealth>();
        viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    private void Update()
    {
        if (health.IsDead || !Input.GetMouseButtonDown(0) || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackInterval;
        if (viewCamera == null)
            return;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, attackRange, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            MonsterHealth monster = hit.collider.GetComponentInParent<MonsterHealth>();
            if (monster != null)
                monster.TakeDamage(attackDamage, transform.position);
            break;
        }
    }

    private void OnGUI()
    {
        if (!health.IsDead)
            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
    }
}
