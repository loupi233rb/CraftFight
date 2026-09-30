using UnityEngine;

[CreateAssetMenu(fileName = "MonsterAudioProfile", menuName = "CraftFight/怪物音效配置")]
public sealed class MonsterAudioProfile : ScriptableObject
{
    [Header("音效素材（同类素材随机播放）")]
    [InspectorName("环境叫声")] public AudioClip[] ambientClips;
    [InspectorName("脚步")] public AudioClip[] footstepClips;
    [InspectorName("近战挥动")] public AudioClip[] attackClips;
    [InspectorName("受击")] public AudioClip[] hurtClips;
    [InspectorName("死亡")] public AudioClip[] deathClips;
    [InspectorName("射箭")] public AudioClip[] shootClips;
    [InspectorName("箭命中")] public AudioClip[] arrowImpactClips;

    [Header("音量")]
    [InspectorName("总音量"), Range(0f, 1f)] public float masterVolume = 0.8f;
    [InspectorName("叫声音量"), Range(0f, 1f)] public float ambientVolume = 0.65f;
    [InspectorName("脚步音量"), Range(0f, 1f)] public float footstepVolume = 0.45f;
    [InspectorName("战斗音量"), Range(0f, 1f)] public float combatVolume = 1f;

    [Header("播放节奏")]
    [InspectorName("叫声间隔（秒）")] public Vector2 ambientInterval = new Vector2(4f, 8f);
    [InspectorName("每步距离（米）"), Min(0.1f)] public float stepDistance = 1.1f;
    [InspectorName("音高变化范围")] public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("3D 距离衰减")]
    [InspectorName("开始衰减距离（米）"), Min(0.1f)] public float minDistance = 1.5f;
    [InspectorName("最远可听距离（米）"), Min(0.2f)] public float maxDistance = 24f;

    public float RandomPitch()
    {
        float minimum = Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y));
        return Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(pitchRange.x, pitchRange.y)));
    }

    public float RandomAmbientDelay()
    {
        float minimum = Mathf.Max(0.5f, Mathf.Min(ambientInterval.x, ambientInterval.y));
        return Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(ambientInterval.x, ambientInterval.y)));
    }

    public AudioClip Pick(AudioClip[] clips, AudioClip previous = null)
    {
        if (clips == null || clips.Length == 0)
            return null;
        int start = Random.Range(0, clips.Length);
        AudioClip fallback = null;
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip == null)
                continue;
            fallback = clip;
            if (clip != previous)
                return clip;
        }
        return fallback;
    }

    public void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = Mathf.Max(0.1f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
        source.volume = 1f;
    }

    public void PlayAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
            return;
        GameObject emitter = new GameObject("Monster sound: " + clip.name);
        emitter.transform.position = position;
        AudioSource source = emitter.AddComponent<AudioSource>();
        ConfigureSource(source);
        source.pitch = RandomPitch();
        source.volume = Mathf.Clamp01(masterVolume * volume);
        source.clip = clip;
        source.Play();
        Destroy(emitter, clip.length / source.pitch + 0.1f);
    }
}
