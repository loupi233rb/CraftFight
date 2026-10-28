using UnityEngine;

public sealed class ArrowProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 18f;
    [SerializeField, Min(0f)] private float lifetime = 3f;

    private Vector3 direction;
    private Transform owner;
    private SteveHealth target;
    private int damage;
    private MonsterAudioProfile audioProfile;

    public bool Targets(SteveHealth player) => player != null && target == player;

    public void Launch(Vector3 travelDirection, Transform shooter, SteveHealth hitTarget, int hitDamage,
        MonsterAudioProfile soundProfile = null)
    {
        direction = travelDirection.normalized;
        owner = shooter;
        target = hitTarget;
        damage = hitDamage;
        audioProfile = soundProfile;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        Vector3 step = direction * speed * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.12f, direction,
            step.magnitude, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        RaycastHit closest = default;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner))
                continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                closest = hit;
                found = true;
            }
        }

        if (found)
        {
            if (audioProfile != null)
                audioProfile.PlayAtPoint(audioProfile.Pick(audioProfile.arrowImpactClips), closest.point,
                    audioProfile.combatVolume);
            if (target != null && (closest.transform == target.transform ||
                closest.transform.IsChildOf(target.transform)))
                target.TakeDamage(damage, transform.position - direction);
            Destroy(gameObject);
            return;
        }

        transform.position += step;
    }
}
