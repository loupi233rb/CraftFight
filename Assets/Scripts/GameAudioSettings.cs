using UnityEngine;

[CreateAssetMenu(fileName = "GameAudioSettings", menuName = "CraftFight/游戏音效配置")]
public sealed class GameAudioSettings : ScriptableObject
{
    public AudioClip[] playerSteps;
    public AudioClip playerHurt;
    public AudioClip itemSelect;
    public AudioClip[] villagerAmbient;
    public AudioClip villagerYes;
    public AudioClip villagerNo;
    public AudioClip combatMusic;
    public AudioClip explorationMusic;

    [Range(0f, 1f)] public float playerStepVolume = 0.45f;
    [Range(0f, 1f)] public float playerHurtVolume = 0.8f;
    [Range(0f, 1f)] public float selectionVolume = 0.55f;
    [Range(0f, 1f)] public float villagerVolume = 0.65f;
    [Range(0f, 1f)] public float musicVolume = 0.22f;
    [Min(0.1f)] public float playerStepDistance = 1.4f;
    public Vector2 villagerInterval = new Vector2(6f, 12f);
    [Min(0.1f)] public float villagerMinDistance = 2f;
    [Min(0.2f)] public float villagerMaxDistance = 16f;
    [Min(0.1f)] public float musicFadeSeconds = 1.5f;
    [Min(0f)] public float combatReleaseDelay = 3f;

    public static GameAudioSettings Load() => Resources.Load<GameAudioSettings>("Audio/GameAudioSettings");

    public static AudioClip Pick(AudioClip[] clips, ref int previous)
    {
        if (clips == null || clips.Length == 0) return null;
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == previous) index = (index + 1) % clips.Length;
        previous = index;
        return clips[index];
    }
}
