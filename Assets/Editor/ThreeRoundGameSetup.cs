using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable scene wiring; never runs automatically on asset refresh.</summary>
public static class ThreeRoundGameSetup
{
    [MenuItem("Tools/CraftFight/Set up three-round game")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setting up the scene.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/MainScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/MainScene.unity first.");
        SteveHealth player = UnityEngine.Object.FindObjectOfType<SteveHealth>();
        if (player == null) throw new InvalidOperationException("No active player in MainScene.");
        Camera camera = player.GetComponentInChildren<Camera>();
        ItemSpawner[] resources = GameObject.Find("craft/item_spawner").GetComponentsInChildren<ItemSpawner>(true);
        if (resources.Length != 3) throw new InvalidOperationException("Expected three crafting resource dispensers.");
        foreach (ItemSpawner resource in resources) resource.ValidateForRound();

        GameObject root = GameObject.Find("GameSystems");
        if (root == null) { root = new GameObject("GameSystems"); Undo.RegisterCreatedObjectUndo(root, "Set up three-round game"); }
        MonsterSpawner spawner = root.GetComponent<MonsterSpawner>() ?? Undo.AddComponent<MonsterSpawner>(root);
        GameFlowTimer flow = root.GetComponent<GameFlowTimer>() ?? Undo.AddComponent<GameFlowTimer>(root);
        GameFlowHud hud = root.GetComponent<GameFlowHud>() ?? Undo.AddComponent<GameFlowHud>(root);
        Transform anchors = Child(root.transform, "TeleportPoints");
        float cameraYaw = camera != null ? camera.transform.localEulerAngles.y : 0f;
        Transform waiting = Place(anchors, "WaitingPoint", new Vector3(76.5f, 30f, 8f), 180f - cameraYaw);
        Transform preparation = Place(anchors, "PreparationPoint", OnGround(new Vector3(76.5f, 15.1f, 7f)), 180f - cameraYaw);
        Vector3 arena = OnGround(new Vector3(-4.26f, 7.1f, 3.07f));
        Transform combat = Place(anchors, "CombatPoint", arena, 90f - cameraYaw);

        Transform spawnRoot = Child(root.transform, "MonsterSpawnPoints");
        var points = new List<Transform>();
        Vector3[] candidates = {
            new Vector3(-16f, 7.1f, 3f), new Vector3(8f, 7.1f, 3f),
            new Vector3(-4f, 7.1f, 15f), new Vector3(-4f, 7.1f, -9f),
            new Vector3(-12f, 7.1f, -5f), new Vector3(4f, 7.1f, 11f)
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 position = OnGround(candidates[i]);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(position, arena, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Spawn point " + i + " cannot reach the arena player spawn.");
            points.Add(Place(spawnRoot, "SpawnPoint" + (i + 1), position, 0f));
        }

        var serializedSpawner = new SerializedObject(spawner);
        SerializedProperty prefabs = serializedSpawner.FindProperty("monsterPrefabs");
        prefabs.arraySize = 2;
        BindPrefab(prefabs.GetArrayElementAtIndex(0), "zombie", "Assets/Prefabs/models/Zombie.prefab");
        BindPrefab(prefabs.GetArrayElementAtIndex(1), "skeleton", "Assets/Prefabs/models/Skeleton.prefab");
        SerializedProperty serializedPoints = serializedSpawner.FindProperty("spawnPoints");
        serializedPoints.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++) serializedPoints.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        serializedSpawner.ApplyModifiedProperties();

        TextAsset config = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Config/GameConfig.json");
        GameConfig.Load(config);
        var serializedFlow = new SerializedObject(flow);
        Assign(serializedFlow, "configuration", config);
        Assign(serializedFlow, "player", player);
        Assign(serializedFlow, "waitingPoint", waiting);
        Assign(serializedFlow, "preparationPoint", preparation);
        Assign(serializedFlow, "combatPoint", combat);
        Assign(serializedFlow, "monsterSpawner", spawner);
        SerializedProperty resourceArray = serializedFlow.FindProperty("resourceSpawners");
        resourceArray.arraySize = resources.Length;
        for (int i = 0; i < resources.Length; i++) resourceArray.GetArrayElementAtIndex(i).objectReferenceValue = resources[i];
        serializedFlow.ApplyModifiedProperties();
        var serializedHud = new SerializedObject(hud);
        Assign(serializedHud, "flow", flow);
        Assign(serializedHud, "headCamera", camera);
        serializedHud.ApplyModifiedProperties();

        spawner.ValidateBindings(GameConfig.Load(config));
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save MainScene.");
        Debug.Log("[GameFlow] Three-round setup saved. Play, then press Enter/Space or either controller trigger.");
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        Undo.RegisterCreatedObjectUndo(child.gameObject, "Set up three-round game");
        child.SetParent(parent, false);
        return child;
    }

    private static Transform Place(Transform parent, string name, Vector3 position, float yaw)
    {
        Transform point = Child(parent, name);
        Undo.RecordObject(point, "Place game flow anchor");
        point.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return point;
    }

    private static Vector3 OnGround(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            throw new InvalidOperationException("No NavMesh near " + position);
        // Root is at the player's feet; a small gap avoids spawning inside the ground.
        Vector3 feet = hit.position + Vector3.up * 0.08f;
        if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f,
            0.28f, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("Teleport/spawn point is obstructed: " + feet);
        return feet;
    }

    private static void BindPrefab(SerializedProperty element, string id, string path)
    {
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void Assign(SerializedObject target, string name, UnityEngine.Object value) =>
        target.FindProperty(name).objectReferenceValue = value;
}
