using UnityEngine;

/// <summary>One monster drops one coin. Walking into it adds one to the wallet.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class CoinPickup : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
    private float availableAt;
    private Vector3 restingPosition;
    private bool collected;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        availableAt = Time.time + pickupDelay;
    }

    private void Start()
    {
        restingPosition = visual != null ? visual.localPosition : Vector3.zero;
    }

    private void Update()
    {
        if (visual == null)
            return;
        visual.localPosition = restingPosition + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.08f);
        visual.Rotate(Vector3.up, 100f * Time.deltaTime, Space.World);
    }

    public static CoinPickup Spawn(Vector3 deathPosition, CoinPickup prefab = null)
    {
        if (prefab == null)
            prefab = Resources.Load<CoinPickup>("Economy/Coin");
        if (prefab == null)
        {
            Debug.LogError("Coin prefab is missing: Resources/Economy/Coin.");
            return null;
        }

        Vector3 position = deathPosition + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(deathPosition + Vector3.up, Vector3.down,
            5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<MonsterHealth>() != null ||
                hit.collider.GetComponentInParent<SteveHealth>() != null)
                continue;
            position = hit.point + Vector3.up * 0.5f;
            break;
        }
        return Instantiate(prefab, position, Quaternion.identity);
    }

    public bool TryCollect(PlayerEconomy player)
    {
        if (collected || player == null || !player.isActiveAndEnabled || player.Health.IsDead ||
            Time.time < availableAt || Vector3.Distance(transform.position,
                player.transform.position + Vector3.up * 0.6f) > GetComponent<SphereCollider>().radius + 0.5f)
            return false;

        collected = true;
        player.AddCoins(1);
        GetComponent<Collider>().enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private void OnTriggerEnter(Collider other) => TryCollect(other.GetComponentInParent<PlayerEconomy>());
    private void OnTriggerStay(Collider other) => TryCollect(other.GetComponentInParent<PlayerEconomy>());
}
