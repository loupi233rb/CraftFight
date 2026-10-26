using UnityEngine;

[DisallowMultipleComponent]
public sealed class VillagerAudio : MonoBehaviour
{
    private GameAudioSettings settings;
    private AudioSource voice;
    private float nextAmbient;
    private int previousAmbient = -1;

    private void Awake()
    {
        settings = GameAudioSettings.Load();
        if (settings == null) { enabled = false; return; }
        var emitter = new GameObject("Villager Voice");
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = Vector3.up * 1.5f;
        voice = emitter.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.dopplerLevel = 0f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = Mathf.Max(0.1f, settings.villagerMinDistance);
        voice.maxDistance = Mathf.Max(voice.minDistance + 0.1f, settings.villagerMaxDistance);
        ScheduleAmbient();
    }

    private void ScheduleAmbient()
    {
        float minimum = Mathf.Max(1f, settings.villagerInterval.x);
        nextAmbient = Time.time + Random.Range(minimum, Mathf.Max(minimum, settings.villagerInterval.y));
    }

    private void Update()
    {
        if (Time.time < nextAmbient || voice.isPlaying) return;
        Speak(GameAudioSettings.Pick(settings.villagerAmbient, ref previousAmbient));
    }

    public void Greet() => Speak(settings != null
        ? GameAudioSettings.Pick(settings.villagerAmbient, ref previousAmbient) : null);

    public void Respond(bool accepted) => Speak(settings != null
        ? (accepted ? settings.villagerYes : settings.villagerNo) : null);

    private void Speak(AudioClip clip)
    {
        if (!isActiveAndEnabled || settings == null) return;
        if (clip != null)
        {
            voice.Stop();
            voice.clip = clip;
            voice.pitch = Random.Range(0.97f, 1.03f);
            voice.volume = settings.villagerVolume;
            voice.Play();
        }
        ScheduleAmbient();
    }

    private void OnDisable()
    {
        if (voice != null) voice.Stop();
    }
}
