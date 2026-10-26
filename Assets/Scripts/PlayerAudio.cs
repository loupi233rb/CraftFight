using UnityEngine;

/// <summary>Local player sounds and music; installed by SteveHealth on scene players and spawned players.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAudio : MonoBehaviour
{
    private GameAudioSettings settings;
    private SteveHealth health;
    private AudioSource steps;
    private AudioSource hurt;
    private AudioSource selection;
    private AudioSource combat;
    private AudioSource exploration;
    private float walkedDistance;
    private int previousStep = -1;
    private float nextThreatCheck;
    private float combatUntil = float.NegativeInfinity;

    private void Awake()
    {
        health = GetComponent<SteveHealth>();
        settings = GameAudioSettings.Load();
        if (settings == null)
        {
            Debug.LogError("Missing Resources/Audio/GameAudioSettings.", this);
            enabled = false;
            return;
        }
        steps = CreateSource("Player Footsteps");
        hurt = CreateSource("Player Hurt");
        selection = CreateSource("Item Selection");
        combat = CreateSource("Combat Music", settings.combatMusic);
        exploration = CreateSource("Exploration Music", settings.explorationMusic);
    }

    private AudioSource CreateSource(string sourceName, AudioClip music = null)
    {
        var emitter = new GameObject(sourceName);
        emitter.transform.SetParent(transform, false);
        var source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        // Player/UI/music stay audible in first person and on a VR headset.
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = music != null ? 128 : 32;
        source.clip = music;
        source.loop = music != null;
        source.volume = music != null ? 0f : 1f;
        return source;
    }

    public void RecordMovement(float distance, bool walking)
    {
        if (!isActiveAndEnabled || settings == null) return;
        if (!walking || health == null || health.IsDead || distance > 2f)
        {
            walkedDistance = 0f;
            return;
        }
        walkedDistance += distance;
        float stride = Mathf.Max(0.1f, settings.playerStepDistance);
        if (walkedDistance < stride) return;
        walkedDistance %= stride;
        AudioClip clip = GameAudioSettings.Pick(settings.playerSteps, ref previousStep);
        if (clip != null)
        {
            steps.pitch = Random.Range(0.96f, 1.04f);
            steps.PlayOneShot(clip, settings.playerStepVolume);
        }
    }

    public void PlayHurt()
    {
        if (!isActiveAndEnabled || settings == null || settings.playerHurt == null) return;
        steps.Stop();
        walkedDistance = 0f;
        hurt.Stop();
        hurt.PlayOneShot(settings.playerHurt, settings.playerHurtVolume);
    }

    public void PlaySelection()
    {
        if (isActiveAndEnabled && settings != null && settings.itemSelect != null)
            selection.PlayOneShot(settings.itemSelect, settings.selectionVolume);
    }

    private void Update()
    {
        if (settings == null) return;
        bool alive = health != null && !health.IsDead;
        if (alive && Time.time >= nextThreatCheck)
        {
            nextThreatCheck = Time.time + 0.5f;
            foreach (MonsterChaseTarget monster in FindObjectsOfType<MonsterChaseTarget>())
            {
                if (!monster.IsPursuing(health)) continue;
                combatUntil = Time.time + Mathf.Max(0f, settings.combatReleaseDelay) + 0.5f;
                break;
            }
        }
        bool inCombat = alive && Time.time < combatUntil;
        FadeMusic(combat, inCombat);
        FadeMusic(exploration, alive && !inCombat);
    }

    private void FadeMusic(AudioSource source, bool audible)
    {
        float target = audible ? settings.musicVolume : 0f;
        if (target > 0f && source.clip != null && !source.isPlaying) source.Play();
        source.volume = Mathf.MoveTowards(source.volume, target,
            Mathf.Max(0.01f, settings.musicVolume) * Time.unscaledDeltaTime /
            Mathf.Max(0.1f, settings.musicFadeSeconds));
        if (!audible && source.volume <= 0f && source.isPlaying) source.Pause();
    }

    private void OnDisable()
    {
        if (steps != null) steps.Stop();
        if (hurt != null) hurt.Stop();
        if (selection != null) selection.Stop();
        if (combat != null) combat.Stop();
        if (exploration != null) exploration.Stop();
    }
}
