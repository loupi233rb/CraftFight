using System.Collections;
using UnityEngine;

public sealed class MonsterHealth : MonoBehaviour
{
    private const int MaxHealthValue = 20;
    [SerializeField] private int currentHealth = MaxHealthValue;
    [SerializeField, Min(0f)] private float corpseDuration = 1.4f;

    private DamageVisuals visuals;
    private MonsterAudio monsterAudio;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => MaxHealthValue;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = MaxHealthValue;
        monsterAudio = GetComponent<MonsterAudio>();
        visuals = GetComponent<DamageVisuals>();
        if (visuals == null)
            visuals = gameObject.AddComponent<DamageVisuals>();
    }

    public void TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (IsDead || amount <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (IsDead)
        {
            visuals.ShowDeath();
            if (monsterAudio != null)
                monsterAudio.PlayDeath();
            MonsterChaseTarget chase = GetComponent<MonsterChaseTarget>();
            if (chase != null)
                chase.Die();
            StartCoroutine(DespawnAfterDeath());
            return;
        }

        visuals.ShowHit();
        if (monsterAudio != null)
            monsterAudio.PlayHurt();
        MonsterChaseTarget livingChase = GetComponent<MonsterChaseTarget>();
        if (livingChase != null)
            livingChase.ApplyKnockback(sourcePosition);
    }

    private IEnumerator DespawnAfterDeath()
    {
        yield return new WaitForSeconds(corpseDuration);
        DeathBurstEffect.Spawn(transform.position + Vector3.up * 1.1f);
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (IsDead || Camera.main == null)
            return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.25f);
        if (screen.z <= 0f)
            return;

        Rect label = new Rect(screen.x - 42f, Screen.height - screen.y - 13f, 84f, 25f);
        GUI.Box(label, currentHealth + " / " + MaxHealthValue + " HP");
    }
}
