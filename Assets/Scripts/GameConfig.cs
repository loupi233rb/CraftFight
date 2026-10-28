using System;
using UnityEngine;

/// <summary>All round quantities and durations come from Resources/Config/GameConfig.json.</summary>
[Serializable]
public sealed class GameConfig
{
    public float spawnIntervalSeconds;
    public RoundConfig[] rounds;

    public static GameConfig Load(TextAsset source)
    {
        if (source == null) throw new InvalidOperationException("GameConfig.json is missing.");
        GameConfig config = JsonUtility.FromJson<GameConfig>(source.text);
        if (config == null) throw new InvalidOperationException("GameConfig.json is empty.");
        config.Validate();
        return config;
    }

    public void Validate()
    {
        if (!Finite(spawnIntervalSeconds) || spawnIntervalSeconds < 0f)
            throw new InvalidOperationException("spawnIntervalSeconds must be finite and >= 0.");
        if (rounds == null || rounds.Length != 3)
            throw new InvalidOperationException("GameConfig.json must contain exactly three rounds.");
        for (int i = 0; i < rounds.Length; i++)
        {
            RoundConfig round = rounds[i];
            if (round == null || !Finite(round.preparationSeconds) || round.preparationSeconds <= 0f ||
                !Finite(round.combatSeconds) || round.combatSeconds <= 0f)
                throw new InvalidOperationException("Round " + (i + 1) + " durations must be finite and > 0.");
            if (round.resources == null || round.resources.oak_plank < 0 ||
                round.resources.iron_ingot < 0 || round.resources.flint < 0)
                throw new InvalidOperationException("Round " + (i + 1) + " resource counts must be >= 0.");
            if (round.monsters == null || round.monsters.Length == 0)
                throw new InvalidOperationException("Round " + (i + 1) + " needs a monster list.");
            long total = 0;
            foreach (MonsterQuota monster in round.monsters)
            {
                if (monster == null || string.IsNullOrWhiteSpace(monster.id) || monster.count < 0)
                    throw new InvalidOperationException("Round " + (i + 1) + " has an invalid monster entry.");
                total += monster.count;
            }
            if (total <= 0 || total > int.MaxValue)
                throw new InvalidOperationException("Round " + (i + 1) + " needs a positive monster total.");
        }
    }

    private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
}

[Serializable]
public sealed class RoundConfig
{
    public float preparationSeconds;
    public float combatSeconds;
    public MonsterQuota[] monsters;
    public ResourceQuota resources;
}

[Serializable]
public sealed class MonsterQuota
{
    public string id;
    public int count;
}

[Serializable]
public sealed class ResourceQuota
{
    public int oak_plank;
    public int iron_ingot;
    public int flint;

    public int ForItem(string itemName)
    {
        switch (itemName)
        {
            case "oak_plank": return oak_plank;
            case "iron_ingot": return iron_ingot;
            case "flint": return flint;
            default: throw new InvalidOperationException("Resource is not configured: " + itemName);
        }
    }
}
