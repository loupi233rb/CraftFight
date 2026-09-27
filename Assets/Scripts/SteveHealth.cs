using UnityEngine;

public sealed class SteveHealth : MonoBehaviour
{
    private const int MaxHealthValue = 20;
    [SerializeField] private int currentHealth = MaxHealthValue;
    private DamageVisuals visuals;

    public bool IsDead => currentHealth <= 0;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => MaxHealthValue;

    private void Awake()
    {
        currentHealth = MaxHealthValue;
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
            visuals.ShowDeath();
        else
            visuals.ShowHit();
        SteveFirstPersonController movement = GetComponent<SteveFirstPersonController>();
        if (movement != null)
            movement.ApplyKnockback(sourcePosition);
        Debug.Log("Steve HP: " + currentHealth + "/" + MaxHealthValue, this);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(Screen.width * 0.5f - 85f, Screen.height - 48f, 170f, 32f),
            "Steve  " + currentHealth + " / " + MaxHealthValue + " HP");
        if (IsDead)
            GUI.Box(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.5f - 20f, 180f, 40f), "Steve 已死亡");
    }
}
