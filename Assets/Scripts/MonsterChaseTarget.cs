using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(MonsterHealth))]
public sealed class MonsterChaseTarget : MonoBehaviour
{
    public enum AttackStyle { Melee, Bow }

    [SerializeField] private Transform target;
    [SerializeField] private AttackStyle attackStyle;
    [SerializeField, Min(0.05f)] private float pathUpdateInterval = 0.2f;
    [SerializeField, Min(0f)] private float bodyTurnSpeed = 120f;
    [SerializeField, Min(0f)] private float headTurnSpeed = 480f;
    [SerializeField, Min(0f)] private float meleeRange = 1.8f;
    [SerializeField, Min(0f)] private float bowRange = 15f;
    [SerializeField, Min(0f)] private float attackInterval = 1.2f;
    [SerializeField, Min(0f)] private float attackImpactTime = 0.35f;
    [SerializeField, Min(0)] private int attackDamage = 3;
    [SerializeField, Min(0f)] private float knockbackSpeed = 6f;
    [SerializeField, Min(0f)] private float knockbackLiftSpeed = 3.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private MonsterAudio monsterAudio;
    private Transform head;
    private Transform loadedArrow;
    private SteveHealth targetHealth;
    private MonsterHealth health;
    private Vector3 knockbackVelocity;
    private float knockbackVerticalSpeed;
    private float knockbackHeight;
    private bool isKnockedBack;
    private float nextPathUpdate;
    private float nextAttack;
    private float impactAt;
    private bool impactPending;
    private float headYaw;
    private float headPitch;
    private Quaternion headRestRotation;
    private Vector3 previousPosition;
    private int upperBodyLayer;

    public bool IsKnockedBack => isKnockedBack;

    // The current AI pursues its assigned player continuously, without an aggro-radius system.
    public bool IsPursuing(SteveHealth player) => isActiveAndEnabled && targetHealth == player &&
        player != null && !player.IsDead && health != null && !health.IsDead && agent.isOnNavMesh;

    private static readonly int MoveSpeedParameter = Animator.StringToHash("MoveSpeed");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<MonsterHealth>();
        monsterAudio = GetComponent<MonsterAudio>();
        head = transform.Find("root/body/head") ?? transform.Find("body/head");
        loadedArrow = transform.Find("root/bow/arrow") ?? transform.Find("bow/arrow");
        headRestRotation = head != null ? head.localRotation : Quaternion.identity;
        previousPosition = transform.position;
        upperBodyLayer = animator.GetLayerIndex("Upper Body");
        agent.updateRotation = false;
        agent.stoppingDistance = attackStyle == AttackStyle.Bow ? 10f : 1.25f;
        if (target != null)
            targetHealth = target.GetComponent<SteveHealth>();
    }


    private void Start()
    {
        if (target != null)
            return;
        SteveHealth player = FindObjectOfType<SteveHealth>();
        if (player != null)
            Initialize(player.transform);
    }

    public void Initialize(Transform playerTarget)
    {
        target = playerTarget;
        targetHealth = target != null ? target.GetComponent<SteveHealth>() : null;
        nextPathUpdate = 0f;
    }

    private void Update()
    {
        UpdateLocomotion();
        if (!agent.isOnNavMesh)
            return;
        if (isKnockedBack)
        {
            AdvanceKnockback();
            return;
        }
        if (health.IsDead)
        {
            agent.isStopped = true;
            animator.SetBool("Moving", false);
            return;
        }
        if (target == null)
            return;
        if (targetHealth != null && targetHealth.IsDead)
        {
            agent.isStopped = true;
            animator.SetBool("Moving", false);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        Vector3 flatTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        float distance = flatTarget.magnitude;
        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + pathUpdateInterval;
            if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, 2f, agent.areaMask))
                agent.SetDestination(hit.position);
        }

        bool lineOfSight = HasLineOfSight();
        float range = attackStyle == AttackStyle.Bow ? bowRange : meleeRange;
        bool canAttack = distance <= range && lineOfSight;
        agent.isStopped = canAttack &&
            (attackStyle == AttackStyle.Bow || distance <= agent.stoppingDistance);

        Vector3 bodyDirection = agent.isStopped ? flatTarget : agent.desiredVelocity;
        if (bodyDirection.sqrMagnitude < 0.01f)
            bodyDirection = flatTarget;
        bodyDirection.y = 0f;
        if (bodyDirection.sqrMagnitude > 0.001f)
        {
            // The imported Blockbench models face local -Z.
            Quaternion facing = Quaternion.LookRotation(-bodyDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, bodyTurnSpeed * Time.deltaTime);
        }

        if (impactPending && attackStyle == AttackStyle.Bow && Time.time >= impactAt)
        {
            impactPending = false;
            FireArrow();
        }

        if (!impactPending && canAttack && Time.time >= nextAttack &&
            Vector3.Angle(-transform.forward, flatTarget) < 40f)
        {
            animator.SetTrigger("Attack");
            if (attackStyle == AttackStyle.Melee && monsterAudio != null)
                monsterAudio.PlayAttack();
            if (loadedArrow != null && attackStyle == AttackStyle.Bow)
                loadedArrow.gameObject.SetActive(true);
            impactPending = true;
            if (attackStyle == AttackStyle.Bow)
                impactAt = Time.time + attackImpactTime;
            nextAttack = Time.time + attackInterval;
        }
    }

    public void OnMeleeImpact()
    {
        if (attackStyle != AttackStyle.Melee || !impactPending || health.IsDead || isKnockedBack)
            return;

        impactPending = false;
        HitTarget();
    }

    private void UpdateLocomotion()
    {
        Vector3 displacement = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up);
        previousPosition = transform.position;
        float speed = Time.deltaTime > 0f ? displacement.magnitude / Time.deltaTime : 0f;
        bool moving = !health.IsDead && speed > 0.05f;
        if (moving)
        {
            float amount = Mathf.Clamp01(speed / Mathf.Max(0.1f, agent.speed));
            animator.SetFloat(MoveSpeedParameter, amount, 0.14f, Time.deltaTime);
        }
        else
        {
            // Fully select the standing pose instead of retaining a damped walk weight.
            animator.SetFloat(MoveSpeedParameter, 0f);
        }
        animator.SetBool("Moving", moving);
    }

    private bool HasLineOfSight()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit source, 2f, agent.areaMask) ||
            !NavMesh.SamplePosition(target.position, out NavMeshHit destination, 2f, agent.areaMask) ||
            NavMesh.Raycast(source.position, destination.position, out _, agent.areaMask))
            return false;

        Vector3 start = head != null ? head.position : transform.position + Vector3.up * 1.5f;
        Vector3 end = target.position + Vector3.up * 1.3f;
        Vector3 direction = end - start;
        RaycastHit[] hits = Physics.RaycastAll(start, direction.normalized, direction.magnitude,
            ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            return hit.transform == target || hit.transform.IsChildOf(target);
        }
        return true;
    }

    public void ApplyKnockback(Vector3 sourcePosition)
    {
        Vector3 away = Vector3.ProjectOnPlane(transform.position - sourcePosition, Vector3.up);
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;
        knockbackVelocity = away.normalized * knockbackSpeed;
        knockbackVerticalSpeed = knockbackLiftSpeed;
        knockbackHeight = Mathf.Max(0f, transform.position.y - agent.nextPosition.y);
        isKnockedBack = true;
        agent.isStopped = true;
        agent.updatePosition = false;
        impactPending = false;
        animator.ResetTrigger("Attack");
        if (upperBodyLayer >= 0)
            animator.CrossFadeInFixedTime("Upper Body.Rest", 0.1f, upperBodyLayer);
        if (loadedArrow != null && attackStyle == AttackStyle.Bow)
            loadedArrow.gameObject.SetActive(false);
    }

    public void Die()
    {
        isKnockedBack = false;
        impactPending = false;
        if (agent.isOnNavMesh)
        {
            agent.updatePosition = true;
            agent.isStopped = true;
        }
        if (loadedArrow != null && attackStyle == AttackStyle.Bow)
            loadedArrow.gameObject.SetActive(false);
        animator.SetBool("Moving", false);
        animator.SetFloat(MoveSpeedParameter, 0f);
        animator.ResetTrigger("Attack");
        if (upperBodyLayer >= 0)
            animator.SetLayerWeight(upperBodyLayer, 0f);
        if (animator.HasState(0, Animator.StringToHash("Base Layer.Death")))
            animator.CrossFadeInFixedTime("Base Layer.Death", 0.05f, 0);
        CapsuleCollider bodyCollider = GetComponent<CapsuleCollider>();
        if (bodyCollider != null)
            bodyCollider.enabled = false;
    }

    private void AdvanceKnockback()
    {
        agent.Move(knockbackVelocity * Time.deltaTime);
        knockbackVerticalSpeed += Physics.gravity.y * Time.deltaTime;
        knockbackHeight = Mathf.Max(0f, knockbackHeight + knockbackVerticalSpeed * Time.deltaTime);
        transform.position = agent.nextPosition + Vector3.up * knockbackHeight;
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 12f * Time.deltaTime);

        if (knockbackHeight <= 0f && knockbackVerticalSpeed <= 0f)
        {
            transform.position = agent.nextPosition;
            agent.updatePosition = true;
            isKnockedBack = false;
        }
    }

    private void HitTarget()
    {
        if (targetHealth == null || targetHealth.IsDead)
            return;
        Vector3 difference = target.position - transform.position;
        difference.y = 0f;
        if (difference.magnitude <= meleeRange && HasLineOfSight())
            targetHealth.TakeDamage(attackDamage, transform.position);
    }

    private void FireArrow()
    {
        if (loadedArrow == null || target == null)
            return;
        Vector3 origin = loadedArrow.position;
        Vector3 direction = (target.position + Vector3.up * 1.3f - origin).normalized;
        if (direction.sqrMagnitude < 0.001f)
            return;

        GameObject arrow = Instantiate(loadedArrow.gameObject, origin, Quaternion.LookRotation(-direction));
        arrow.name = "SkeletonArrow";
        MonsterAudioProfile audioProfile = monsterAudio != null ? monsterAudio.Profile : null;
        arrow.AddComponent<ArrowProjectile>().Launch(direction, transform, targetHealth, attackDamage, audioProfile);
        if (monsterAudio != null)
            monsterAudio.PlayShoot();
        loadedArrow.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (health.IsDead || head == null || target == null)
            return;

        Vector3 toTarget = target.position + Vector3.up * 1.5f - head.position;
        Vector3 local = transform.InverseTransformDirection(toTarget.normalized);
        float wantedYaw = Mathf.Clamp(Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg, -70f, 70f);
        float wantedPitch = Mathf.Clamp(Mathf.Atan2(local.y,
            new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
        headYaw = Mathf.MoveTowardsAngle(headYaw, wantedYaw, headTurnSpeed * Time.deltaTime);
        headPitch = Mathf.MoveTowardsAngle(headPitch, wantedPitch, headTurnSpeed * Time.deltaTime);
        head.localRotation = headRestRotation * Quaternion.Euler(headPitch, headYaw, 0f);
    }
}
