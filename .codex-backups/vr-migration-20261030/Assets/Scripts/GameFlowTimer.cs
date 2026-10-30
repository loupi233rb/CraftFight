using System;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Owns the three-round lifecycle. Teleporting never changes player health.</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class GameFlowTimer : MonoBehaviour
{
    public enum Phase { Waiting, Preparation, Combat, Completed, Failed }

    [SerializeField] private TextAsset configuration;
    [SerializeField] private SteveHealth player;
    [SerializeField] private Transform waitingPoint;
    [SerializeField] private Transform preparationPoint;
    [SerializeField] private Transform combatPoint;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private ItemSpawner[] resourceSpawners;
    private SteveFirstPersonController movement;
    private GameConfig config;
    private int roundIndex;
    private float deadline;
    private bool initialized;
    private bool triggerWasPressed;

    public Phase CurrentPhase { get; private set; } = Phase.Waiting;
    public int RoundNumber => roundIndex + 1;
    public int RoundCount => config != null ? config.rounds.Length : 0;
    public float RemainingSeconds => CurrentPhase == Phase.Preparation || CurrentPhase == Phase.Combat
        ? Mathf.Max(0f, deadline - Time.time) : 0f;
    public MonsterSpawner Monsters => monsterSpawner;
    public string StatusMessage { get; private set; } = string.Empty;
    public event Action PhaseChanged;

    private void Awake()
    {
        // Claim the existing dispensers before their Start/Update can issue free resources.
        if (resourceSpawners != null)
            foreach (ItemSpawner spawner in resourceSpawners)
                if (spawner != null) spawner.UseRoundQuotas();
    }

    private void Start()
    {
        try
        {
            if (configuration == null) configuration = Resources.Load<TextAsset>("Config/GameConfig");
            config = GameConfig.Load(configuration);
            if (player == null || waitingPoint == null || preparationPoint == null || combatPoint == null || monsterSpawner == null)
                throw new InvalidOperationException("Assign player, waiting/preparation/combat points and MonsterSpawner.");
            movement = player.GetComponent<SteveFirstPersonController>();
            if (movement == null) throw new InvalidOperationException("The player needs SteveFirstPersonController.");
            if (resourceSpawners == null || resourceSpawners.Length != 3)
                throw new InvalidOperationException("Assign the three resource dispensers.");
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (ItemSpawner spawner in resourceSpawners)
            {
                if (spawner == null || !names.Add(spawner.itemName))
                    throw new InvalidOperationException("Resource dispensers must be present and unique.");
                config.rounds[0].resources.ForItem(spawner.itemName);
                spawner.ValidateForRound();
            }
            monsterSpawner.ValidateBindings(config);
            monsterSpawner.AllMonstersDefeated += OnAllMonstersDefeated;
            monsterSpawner.SpawnFailed += Fail;
            Teleport(waitingPoint);
            movement.SetMovementSuspended(true);
            initialized = true;
            SetPhase(Phase.Waiting, "按 Enter / 空格或手柄扳机开始游戏");
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private void Update()
    {
        if (!initialized || CurrentPhase == Phase.Completed || CurrentPhase == Phase.Failed) return;
        if (player.IsDead) { Fail("玩家已死亡"); return; }
        if (CurrentPhase == Phase.Waiting)
        {
            bool pressed = TriggerPressed(XRNode.LeftHand) || TriggerPressed(XRNode.RightHand);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || (pressed && !triggerWasPressed))
                StartGame();
            triggerWasPressed = pressed;
            return;
        }
        if (Time.time < deadline) return;
        if (CurrentPhase == Phase.Preparation) BeginCombat();
        else if (CurrentPhase == Phase.Combat) FinishCombat("战斗时间结束");
    }

    private static bool TriggerPressed(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
    }

    public void StartGame()
    {
        if (!initialized || CurrentPhase != Phase.Waiting || player.IsDead) return;
        roundIndex = 0;
        BeginPreparation();
    }

    private void BeginPreparation()
    {
        Teleport(preparationPoint);
        movement.SetMovementSuspended(false);
        foreach (ItemSpawner spawner in resourceSpawners)
            spawner.SetRoundQuota(config.rounds[roundIndex].resources.ForItem(spawner.itemName));
        deadline = Time.time + config.rounds[roundIndex].preparationSeconds;
        SetPhase(Phase.Preparation, "合成准备");
    }

    private void BeginCombat()
    {
        try
        {
            foreach (ItemSpawner spawner in resourceSpawners) spawner.StopRoundGeneration();
            Teleport(combatPoint);
            deadline = Time.time + config.rounds[roundIndex].combatSeconds;
            SetPhase(Phase.Combat, "战斗");
            monsterSpawner.BeginRound(config.rounds[roundIndex], config.spawnIntervalSeconds, player);
        }
        catch (Exception exception) { Fail(exception.Message); }
    }

    private void OnAllMonstersDefeated() => FinishCombat("怪物已全部击败");

    private void FinishCombat(string reason)
    {
        // Moving out of Combat first prevents timeout/death notifications from advancing twice.
        if (CurrentPhase != Phase.Combat) return;
        CurrentPhase = Phase.Preparation;
        monsterSpawner.StopAndClear();
        if (player.IsDead) { Fail("玩家已死亡"); return; }
        if (roundIndex + 1 >= config.rounds.Length)
        {
            Teleport(preparationPoint);
            movement.SetMovementSuspended(false);
            SetPhase(Phase.Completed, "三关已完成！");
            return;
        }
        roundIndex++;
        BeginPreparation();
        Debug.Log("[GameFlow] " + reason + "; entering round " + RoundNumber, this);
    }

    private void Teleport(Transform destination)
    {
        PlayerEconomy economy = player.GetComponent<PlayerEconomy>();
        if (economy != null) economy.CloseTrade();
        movement.TeleportTo(destination.position, destination.eulerAngles.y);
    }

    private void Fail(string message)
    {
        if (monsterSpawner != null) monsterSpawner.StopAndClear();
        if (resourceSpawners != null)
            foreach (ItemSpawner spawner in resourceSpawners)
                if (spawner != null) spawner.StopRoundGeneration();
        if (movement != null) movement.SetMovementSuspended(true);
        SetPhase(Phase.Failed, message);
        if (player == null || !player.IsDead) Debug.LogError("[GameFlow] " + message, this);
    }

    private void SetPhase(Phase phase, string message)
    {
        CurrentPhase = phase;
        StatusMessage = message;
        Debug.Log("[GameFlow] " + phase + " round=" + RoundNumber + " " + message, this);
        PhaseChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (monsterSpawner == null) return;
        monsterSpawner.AllMonstersDefeated -= OnAllMonstersDefeated;
        monsterSpawner.SpawnFailed -= Fail;
    }
}
