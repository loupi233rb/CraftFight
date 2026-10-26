using System.Collections;
using UnityEngine;

public sealed class SteveHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 20;
    [SerializeField] private int currentHealth = 20;
    private DamageVisuals visuals;
    private Animator animator;
    private bool showDeathMessage;

    public bool IsDead => currentHealth <= 0;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        visuals = GetComponent<DamageVisuals>();
        if (visuals == null)
            visuals = gameObject.AddComponent<DamageVisuals>();
    }

    public void TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (IsDead || amount <= 0)
            return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        SteveFirstPersonController movement = GetComponent<SteveFirstPersonController>();
        if (IsDead)
        {
            if (movement != null)
                movement.EnterDeathView();
            visuals.ShowDeath();
            if (animator != null && animator.HasState(0, Animator.StringToHash("Base Layer.Death")))
            {
                animator.SetBool("Moving", false);
                animator.CrossFadeInFixedTime("Base Layer.Death", 0.06f, 0);
                StartCoroutine(ShowMessageAfterDeathAnimation());
            }
            else
            {
                Debug.LogError("Steve death animation state is missing.", this);
                showDeathMessage = true;
            }
        }
        else
        {
            visuals.ShowHit();
            if (movement != null)
                movement.ApplyKnockback(sourcePosition);
        }
        Debug.Log("Steve HP: " + currentHealth + "/" + maxHealth, this);
    }

    public void IncreaseMaxHealth(int amount)
    {
        if (IsDead || amount <= 0)
            return;
        maxHealth += amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private IEnumerator ShowMessageAfterDeathAnimation()
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Death"))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        showDeathMessage = true;
    }

    private void OnGUI()
    {
        if (!IsDead)
            GUI.Box(new Rect(Screen.width * 0.5f - 85f, Screen.height - 48f, 170f, 32f),
                "Steve  " + currentHealth + " / " + maxHealth + " HP");
        if (showDeathMessage)
            GUI.Box(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.5f - 20f, 180f, 40f), "Steve 已死亡");
    }
}
