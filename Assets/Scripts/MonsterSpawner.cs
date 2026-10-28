using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class MonsterSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class PrefabBinding
    {
        public string id;
        public GameObject prefab;
    }

    [SerializeField] private PrefabBinding[] monsterPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    private readonly List<GameObject> plan = new List<GameObject>();
    private readonly List<MonsterHealth> spawned = new List<MonsterHealth>();
    private SteveHealth player;
    private Transform runtimeRoot;
    private float interval;
    private float nextSpawnAt;
    private bool running;
    private bool reportedClear;

    public event Action AllMonstersDefeated;
    public event Action<string> SpawnFailed;
    public int PlannedCount => plan.Count;
    public int SpawnedCount { get; private set; }
    public bool SpawningComplete => SpawnedCount == PlannedCount;
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (MonsterHealth monster in spawned)
                if (monster != null && monster.gameObject.activeInHierarchy && !monster.IsDead) count++;
            return count;
        }
    }

    public void ValidateBindings(GameConfig config)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            throw new InvalidOperationException("Assign monster spawn points.");
        foreach (Transform point in spawnPoints)
            if (point == null) throw new InvalidOperationException("A monster spawn point is missing.");
        foreach (RoundConfig round in config.rounds)
        foreach (MonsterQuota entry in round.monsters)
        {
            GameObject prefab = Resolve(entry.id);
            if (prefab == null || prefab.GetComponent<MonsterHealth>() == null ||
                prefab.GetComponent<MonsterChaseTarget>() == null || prefab.GetComponent<NavMeshAgent>() == null)
                throw new InvalidOperationException("Invalid monster prefab binding: " + entry.id);
        }
    }

    private GameObject Resolve(string id)
    {
        if (monsterPrefabs != null)
            foreach (PrefabBinding binding in monsterPrefabs)
                if (binding != null && binding.id == id) return binding.prefab;
        return null;
    }

    public void BeginRound(RoundConfig round, float spawnInterval, SteveHealth target)
    {
        StopAndClear();
        player = target;
        plan.Clear();
        foreach (MonsterQuota entry in round.monsters)
        {
            GameObject prefab = Resolve(entry.id);
            if (prefab == null) throw new InvalidOperationException("Unknown monster id: " + entry.id);
            for (int i = 0; i < entry.count; i++) plan.Add(prefab);
        }
        SpawnedCount = 0;
        interval = spawnInterval;
        reportedClear = false;
        runtimeRoot = new GameObject("RoundMonsters").transform;
        runtimeRoot.SetParent(transform, false);
        running = true;
        nextSpawnAt = Time.time;
        AdvanceSpawning();
    }

    private void Update()
    {
        if (!running) return;
        if (player == null || player.IsDead) { running = false; return; }
        AdvanceSpawning();
        CheckCleared();
    }

    private void AdvanceSpawning()
    {
        while (running && SpawnedCount < plan.Count && Time.time >= nextSpawnAt)
        {
            GameObject prefab = plan[SpawnedCount];
            if (!TryFindPosition(prefab, out Vector3 position))
            {
                running = false;
                SpawnFailed?.Invoke("No reachable NavMesh spawn point for " + prefab.name);
                return;
            }
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, runtimeRoot);
            instance.SetActive(true);
            MonsterHealth health = instance.GetComponent<MonsterHealth>();
            spawned.Add(health);
            health.Died += OnMonsterDied;
            instance.GetComponent<MonsterChaseTarget>().Initialize(player.transform);
            SpawnedCount++;
            nextSpawnAt = Time.time + interval;
            if (interval > 0f) break;
        }
    }

    private bool TryFindPosition(GameObject prefab, out Vector3 position)
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(SpawnedCount + i) % spawnPoints.Length];
            if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, filter) &&
                NavMesh.SamplePosition(player.transform.position, out NavMeshHit destination, 2f, filter) &&
                NavMesh.CalculatePath(hit.position, destination.position, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                position = hit.position;
                return true;
            }
        }
        position = default;
        return false;
    }

    private void OnMonsterDied(MonsterHealth monster) => CheckCleared();

    private void CheckCleared()
    {
        if (!running || reportedClear || !SpawningComplete || AliveCount != 0) return;
        reportedClear = true;
        running = false;
        AllMonstersDefeated?.Invoke();
    }

    public void StopAndClear()
    {
        running = false;
        foreach (MonsterHealth monster in spawned)
            if (monster != null) monster.Died -= OnMonsterDied;
        spawned.Clear();
        if (runtimeRoot != null)
        {
            runtimeRoot.gameObject.SetActive(false);
            Destroy(runtimeRoot.gameObject);
            runtimeRoot = null;
        }
        foreach (ArrowProjectile arrow in FindObjectsOfType<ArrowProjectile>())
        {
            if (!arrow.Targets(player)) continue;
            arrow.gameObject.SetActive(false);
            Destroy(arrow.gameObject);
        }
    }

    private void OnDisable() => StopAndClear();
}
