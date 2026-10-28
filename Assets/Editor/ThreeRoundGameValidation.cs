using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Play Mode checks accelerate deadlines in memory; the JSON and scene stay unchanged.</summary>
[InitializeOnLoad]
public static class ThreeRoundGameValidation
{
    private const string RunningKey = "CraftFight.ThreeRoundValidation.Running";
    private const string ScenarioKey = "CraftFight.ThreeRoundValidation.Scenario";
    private const string BackgroundKey = "CraftFight.ThreeRoundValidation.Background";
    private const string Report = "Docs/ThreeRoundGameValidation.txt";
    private static double startedAt;
    private static float stageStarted;
    private static int stage;
    private static GameFlowTimer flow;
    private static SteveHealth player;
    private static int expectedHealth;
    private static string[] inventory;
    private static int coins;
    private static float speed;
    private static int attack;
    private static bool stopping;
    private static bool hadError;
    private static GameObject testArrow;

    static ThreeRoundGameValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += ResumePendingScenario;
        Application.logMessageReceived += OnLog;
    }

    private static void ResumePendingScenario()
    {
        if (SessionState.GetBool(RunningKey, false) && SessionState.GetInt(ScenarioKey, 0) == 1 &&
            !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/CraftFight/Validate three-round game")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before validation.");
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/MainScene.unity" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open and save MainScene first.");
        GameConfig config = GameConfig.Load(Resources.Load<TextAsset>("Config/GameConfig"));
        Directory.CreateDirectory("Docs");
        File.WriteAllText(Report, "Three-round game validation " + DateTime.Now.ToString("s") + "\n");
        foreach (RoundConfig round in config.rounds)
            Write("Config: prep=" + round.preparationSeconds + " combat=" + round.combatSeconds +
                " monsters=" + round.monsters.Sum(m => m.count) + " resources=" +
                round.resources.oak_plank + "/" + round.resources.iron_ingot + "/" + round.resources.flint);
        bool rejected = false;
        try { GameConfig.Load(new TextAsset("{\"rounds\":[]}")); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid configuration is rejected");
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(BackgroundKey, Application.runInBackground);
        SessionState.SetInt(ScenarioKey, 0);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            startedAt = EditorApplication.timeSinceStartup;
            stage = 0;
            stageStarted = Time.time;
            stopping = hadError = false;
            flow = null;
            EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetInt(ScenarioKey, 0) != 1)
            {
                SessionState.SetBool(RunningKey, false);
                Write("Final scene=" + SceneManager.GetActiveScene().path + " dirty=" + SceneManager.GetActiveScene().isDirty);
                Write("Play Mode stopped. Physical PICO headset/controller input and rendering were not tested.");
            }
        }
    }

    private static void Tick()
    {
        if (stopping || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 90)
                throw new InvalidOperationException("Runtime validation timed out at stage " + stage);
            if (Time.time - stageStarted < 0.35f) return;
            if (flow == null)
            {
                flow = UnityEngine.Object.FindObjectOfType<GameFlowTimer>();
                player = UnityEngine.Object.FindObjectOfType<SteveHealth>();
                Check(flow != null && player != null, "Runtime flow and player exist");
            }
            if (SessionState.GetInt(ScenarioKey, 0) == 1) { CheckDeath(); return; }

            // Keep AI from injuring the player while checking wave lifecycle, without altering prefabs.
            foreach (MonsterChaseTarget monster in UnityEngine.Object.FindObjectsOfType<MonsterChaseTarget>())
                monster.enabled = false;
            if (stage == 0)
            {
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Waiting, "Game waits for explicit start");
                Check(player.transform.position.y > 25f && player.GetComponent<SteveFirstPersonController>().IsMovementSuspended,
                    "Player stays in the air with movement and gravity suspended");
                Check(UnityEngine.Object.FindObjectsOfType<ItemSpawner>().All(s => s.maxCount == 0 && s.leftCount == 0),
                    "No resources issued before start");
                Check(UnityEngine.Object.FindObjectsOfType<MonsterHealth>().Length == 0, "No monsters before combat");
                flow.StartGame();
                player.TakeDamage(3, player.transform.position + Vector3.back);
                expectedHealth = player.CurrentHealth;
                Check(expectedHealth == player.MaxHealth - 3, "Player can carry damaged health across rounds");
                foreach (HandInventory hand in player.GetComponentsInChildren<HandInventory>())
                    if (string.IsNullOrEmpty(hand.GetSelectedItemName())) hand.AddItem("wood_sword");
                inventory = player.GetComponentsInChildren<HandInventory>().Select(h => h.GetSelectedItemName()).ToArray();
                Check(inventory.Any(name => name == "wood_sword"), "Validation carries an equipped weapon across rounds");
                player.GetComponent<PlayerEconomy>().AddCoins(2);
                player.GetComponent<SteveFirstPersonController>().IncreaseMoveSpeed(0.5f);
                player.GetComponent<SteveMeleeAttack>().IncreaseAttackDamage(2);
                coins = player.GetComponent<PlayerEconomy>().Coins;
                speed = player.GetComponent<SteveFirstPersonController>().MoveSpeed;
                attack = player.GetComponent<SteveMeleeAttack>().AttackDamage;
                CheckPreparation(1, 8, 4, 1, 120f);
                flow.StartGame();
                Check(flow.RoundNumber == 1, "Repeated start cannot reset the current round");
                Next(1);
            }
            else if (stage == 1)
            {
                Check(UnityEngine.Object.FindObjectsOfType<DroppedItem>().Any(i => i.itemName == "oak_plank"),
                    "Resource dispenser produces real dropped items");
                Expire(); Next(2);
            }
            else if (stage == 2)
            {
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat && flow.RoundNumber == 1, "Preparation timeout enters round 1 combat");
                Check(flow.RemainingSeconds > 88f && flow.RemainingSeconds <= 90f, "Round 1 configured combat countdown is 90 seconds");
                Check(flow.Monsters.PlannedCount == 2 && flow.Monsters.SpawnedCount == 1, "First monster spawns immediately; others wait for interval");
                CheckAgents(); KillLiving();
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat, "Killing the first batch cannot finish an incompletely spawned wave");
                Next(3);
            }
            else if (stage == 3)
            {
                CheckAgents();
                if (!flow.Monsters.SpawningComplete) return;
                KillLiving();
                CheckPreparation(2, 6, 6, 1, 90f);
                Check(player.CurrentHealth == expectedHealth, "Round 1 clear returns without healing");
                Expire(); Next(4);
            }
            else if (stage == 4)
            {
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat && flow.RoundNumber == 2, "Round 2 combat starts");
                Check(flow.RemainingSeconds > 118f && flow.RemainingSeconds <= 120f, "Round 2 configured combat countdown is 120 seconds");
                Check(flow.Monsters.PlannedCount == 4, "Round 2 plans four monsters");
                CheckAgents();
                testArrow = new GameObject("ValidationArrow");
                testArrow.transform.position = player.transform.position + Vector3.up * 10f;
                testArrow.AddComponent<ArrowProjectile>().Launch(Vector3.up, null, player, 1);
                Expire(); Next(5);
            }
            else if (stage == 5)
            {
                CheckPreparation(3, 6, 8, 2, 90f);
                Check(UnityEngine.Object.FindObjectsOfType<MonsterHealth>().Length == 0, "Timeout cancels pending spawns and clears living monsters");
                Check(testArrow == null, "Timeout also clears projectiles targeting the player");
                Check(player.CurrentHealth == expectedHealth, "Round 2 timeout returns without healing");
                Expire(); Next(6);
            }
            else if (stage == 6)
            {
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat && flow.RoundNumber == 3, "Round 3 combat starts");
                Check(flow.RemainingSeconds > 148f && flow.RemainingSeconds <= 150f, "Round 3 configured combat countdown is 150 seconds");
                Check(flow.Monsters.PlannedCount == 6, "Round 3 plans six monsters");
                Next(7);
            }
            else if (stage == 7)
            {
                CheckAgents();
                if (!flow.Monsters.SpawningComplete) return;
                Check(UnityEngine.Object.FindObjectsOfType<MonsterChaseTarget>().Count(m =>
                    new SerializedObject(m).FindProperty("attackStyle").enumValueIndex == 1) == 2,
                    "Round 3 contains four zombies and two bow skeletons");
                KillLiving();
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Completed && flow.RoundNumber == 3, "Third clear completes the game");
                Next(8);
            }
            else if (stage == 8)
            {
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Completed && flow.RemainingSeconds == 0f, "No fourth round or preparation timer after completion");
                Check(Vector3.Distance(player.transform.position, Point("preparationPoint").position) < 0.5f, "Completion returns to the crafting area");
                Check(player.CurrentHealth == expectedHealth, "All transitions preserve current health");
                Check(player.GetComponent<PlayerEconomy>().Coins == coins &&
                    player.GetComponent<SteveFirstPersonController>().MoveSpeed == speed &&
                    player.GetComponent<SteveMeleeAttack>().AttackDamage == attack &&
                    player.GetComponentsInChildren<HandInventory>().Select(h => h.GetSelectedItemName()).SequenceEqual(inventory),
                    "Wallet, upgrades and selected hand items survive transitions");
                Check(!hadError, "No runtime errors during three-round flow");
                Write("PASS: Three-round lifecycle checks complete.");
                SessionState.SetInt(ScenarioKey, 1);
                stopping = true;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception) { Abort(exception); }
    }

    private static void CheckDeath()
    {
        if (stage == 0)
        {
            flow.StartGame();
            Expire(); Next(1);
        }
        else if (stage == 1)
        {
            Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat, "Death scenario enters combat");
            player.TakeDamage(int.MaxValue, player.transform.position);
            Next(2);
        }
        else
        {
            Check(flow.CurrentPhase == GameFlowTimer.Phase.Failed && player.IsDead, "Player death enters failure state");
            Check(UnityEngine.Object.FindObjectsOfType<MonsterHealth>().Length == 0, "Failure stops spawning and clears enemies");
            Check(!hadError, "No runtime errors during failure handling");
            Write("ALL RUNTIME CHECKS PASSED");
            SessionState.SetInt(ScenarioKey, 2);
            stopping = true;
            EditorApplication.isPlaying = false;
        }
    }

    private static void CheckPreparation(int round, int planks, int iron, int flint, float duration)
    {
        Check(flow.CurrentPhase == GameFlowTimer.Phase.Preparation && flow.RoundNumber == round, "Preparation phase for round " + round);
        Check(flow.RemainingSeconds > duration - 1f && flow.RemainingSeconds <= duration, "Configured preparation countdown for round " + round);
        Check(Vector3.Distance(player.transform.position, Point("preparationPoint").position) < 0.5f, "Crafting teleport for round " + round);
        foreach (ItemSpawner s in UnityEngine.Object.FindObjectsOfType<ItemSpawner>())
        {
            int expected = s.itemName == "oak_plank" ? planks : s.itemName == "iron_ingot" ? iron : flint;
            Check(s.maxCount == expected && s.leftCount <= expected && s.leftCount >= expected - 1,
                "Round " + round + " quota " + s.itemName + "=" + expected);
        }
    }

    private static Transform Point(string field) => (Transform)typeof(GameFlowTimer).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);
    private static void Expire() => typeof(GameFlowTimer).GetField("deadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, Time.time - 0.1f);
    private static void Next(int value) { stage = value; stageStarted = Time.time; }
    private static void CheckAgents()
    {
        foreach (NavMeshAgent agent in UnityEngine.Object.FindObjectsOfType<NavMeshAgent>())
            if (!agent.isOnNavMesh) throw new InvalidOperationException("Spawned monster is not on the NavMesh: " + agent.name);
    }
    private static void KillLiving()
    {
        foreach (MonsterHealth monster in UnityEngine.Object.FindObjectsOfType<MonsterHealth>())
            if (!monster.IsDead) monster.TakeDamage(int.MaxValue, monster.transform.position);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Write("PASS: " + message);
    }
    private static void Write(string message) => File.AppendAllText(Report, message + "\n");
    private static void OnLog(string condition, string stack, LogType type)
    {
        if (SessionState.GetBool(RunningKey, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        { hadError = true; Write("RUNTIME ERROR: " + condition + "\n" + stack); }
    }
    private static void Abort(Exception exception)
    {
        Write("FAIL: " + exception);
        SessionState.SetInt(ScenarioKey, -1);
        stopping = true;
        Debug.LogError("Three-round validation failed: " + exception.Message);
        EditorApplication.isPlaying = false;
    }
}
