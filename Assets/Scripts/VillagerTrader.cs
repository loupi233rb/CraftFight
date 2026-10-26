using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VillagerTrader : MonoBehaviour
{
    public enum Upgrade { MoveSpeed, AttackDamage, MaxHealth }

    internal static readonly HashSet<VillagerTrader> ActiveTraders = new HashSet<VillagerTrader>();
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;
    [SerializeField, Min(1)] private int moveSpeedCost = 1;
    [SerializeField, Min(0.01f)] private float moveSpeedIncrease = 0.5f;
    [SerializeField, Min(1)] private int attackDamageCost = 1;
    [SerializeField, Min(1)] private int attackDamageIncrease = 2;
    [SerializeField, Min(1)] private int maxHealthCost = 1;
    [SerializeField, Min(1)] private int maxHealthIncrease = 5;

    private void OnEnable() => ActiveTraders.Add(this);
    private void OnDisable() => ActiveTraders.Remove(this);

    public int GetCost(Upgrade upgrade)
    {
        switch (upgrade)
        {
            case Upgrade.MoveSpeed: return Mathf.Max(1, moveSpeedCost);
            case Upgrade.AttackDamage: return Mathf.Max(1, attackDamageCost);
            case Upgrade.MaxHealth: return Mathf.Max(1, maxHealthCost);
            default: return int.MaxValue;
        }
    }

    public string GetDescription(Upgrade upgrade)
    {
        switch (upgrade)
        {
            case Upgrade.MoveSpeed: return "移动速度 +" + moveSpeedIncrease.ToString("0.##");
            case Upgrade.AttackDamage: return "攻击力 +" + attackDamageIncrease;
            case Upgrade.MaxHealth: return "最大血量 +" + maxHealthIncrease + "（同时恢复等量血量）";
            default: return string.Empty;
        }
    }

    public bool CanInteract(PlayerEconomy player)
    {
        if (!isActiveAndEnabled || player == null || player.Health.IsDead ||
            Vector3.Distance(player.transform.position, transform.position) > interactionRange)
            return false;

        Vector3 from = player.transform.position + Vector3.up;
        Vector3 delta = transform.position + Vector3.up - from;
        RaycastHit[] hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(transform))
                continue;
            return false;
        }
        return true;
    }

    // Revalidate range, life, and funds even for calls made outside the UI.
    public bool TryPurchase(PlayerEconomy player, Upgrade upgrade)
    {
        if (!CanInteract(player) || player.CurrentTrader != this)
            return false;
        if (upgrade != Upgrade.MoveSpeed && upgrade != Upgrade.AttackDamage && upgrade != Upgrade.MaxHealth)
            return false;
        if (!player.TrySpendCoins(GetCost(upgrade)))
            return false;

        switch (upgrade)
        {
            case Upgrade.MoveSpeed: player.Movement.IncreaseMoveSpeed(moveSpeedIncrease); break;
            case Upgrade.AttackDamage: player.Attack.IncreaseAttackDamage(attackDamageIncrease); break;
            case Upgrade.MaxHealth: player.Health.IncreaseMaxHealth(maxHealthIncrease); break;
        }
        return true;
    }

    private void OnValidate()
    {
        moveSpeedIncrease = Mathf.Max(0.01f, moveSpeedIncrease);
        attackDamageIncrease = Mathf.Max(1, attackDamageIncrease);
        maxHealthIncrease = Mathf.Max(1, maxHealthIncrease);
    }
}
