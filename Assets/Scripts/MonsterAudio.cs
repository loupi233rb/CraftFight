using UnityEngine;

[RequireComponent(typeof(MonsterHealth))]
public sealed class MonsterAudio : MonoBehaviour
{
    [SerializeField, InspectorName("音效配置")] private MonsterAudioProfile profile;

    private MonsterHealth health;
    private MonsterChaseTarget chase;
    private AudioSource voiceSource;
    private AudioSource stepSource;
    private AudioSource actionSource;
    private Vector3 previousPosition;
    private float walkedDistance;
    private float nextAmbient;
    private bool deathPlayed;
    private AudioClip previousAmbient;
    private AudioClip previousStep;
    private AudioClip previousHurt;
    private AudioClip previousAction;

    public MonsterAudioProfile Profile => profile;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        chase = GetComponent<MonsterChaseTarget>();
        voiceSource = CreateSource("Monster Voice", Vector3.up * 1.3f);
        stepSource = CreateSource("Monster Footsteps", Vector3.up * 0.15f);
        actionSource = CreateSource("Monster Actions", Vector3.up * 1.2f);
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        walkedDistance = 0f;
        ScheduleAmbient();
    }

    private AudioSource CreateSource(string sourceName, Vector3 offset)
    {
        GameObject emitter = new GameObject(sourceName);
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = offset;
        AudioSource source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        if (profile != null)
            profile.ConfigureSource(source);
        return source;
    }

    private void Update()
    {
        float distance = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
        previousPosition = transform.position;
        if (profile == null || health.IsDead)
            return;

        if (Time.time >= nextAmbient && !voiceSource.isPlaying)
        {
            AudioClip clip = profile.Pick(profile.ambientClips, previousAmbient);
            Play(voiceSource, clip, profile.ambientVolume);
            previousAmbient = clip;
            ScheduleAmbient();
        }

        if ((chase != null && chase.IsKnockedBack) || distance > 2f)
        {
            walkedDistance = 0f;
            return;
        }

        walkedDistance += distance;
        float stride = Mathf.Max(0.1f, profile.stepDistance);
        if (walkedDistance >= stride)
        {
            walkedDistance %= stride;
            AudioClip clip = profile.Pick(profile.footstepClips, previousStep);
            Play(stepSource, clip, profile.footstepVolume, true);
            previousStep = clip;
        }
    }

    private void ScheduleAmbient()
    {
        nextAmbient = profile != null ? Time.time + profile.RandomAmbientDelay() : float.PositiveInfinity;
    }

    private void Play(AudioSource source, AudioClip clip, float volume, bool oneShot = false)
    {
        if (clip == null || profile == null || !isActiveAndEnabled)
            return;
        profile.ConfigureSource(source);
        source.pitch = profile.RandomPitch();
        source.volume = Mathf.Clamp01(profile.masterVolume * volume);
        source.clip = clip;
        if (oneShot)
            source.PlayOneShot(clip);
        else
            source.Play();
    }

    public void PlayAttack()
    {
        if (profile == null || deathPlayed)
            return;
        AudioClip clip = profile.Pick(profile.attackClips, previousAction);
        Play(actionSource, clip, profile.combatVolume);
        previousAction = clip;
    }

    public void PlayShoot()
    {
        if (profile == null || deathPlayed)
            return;
        AudioClip clip = profile.Pick(profile.shootClips, previousAction);
        Play(actionSource, clip, profile.combatVolume);
        previousAction = clip;
    }

    public void PlayHurt()
    {
        if (profile == null || deathPlayed)
            return;
        voiceSource.Stop();
        actionSource.Stop();
        stepSource.Stop();
        AudioClip clip = profile.Pick(profile.hurtClips, previousHurt);
        Play(voiceSource, clip, profile.combatVolume);
        previousHurt = clip;
        ScheduleAmbient();
    }

    public void PlayDeath()
    {
        if (deathPlayed)
            return;
        deathPlayed = true;
        voiceSource.Stop();
        stepSource.Stop();
        actionSource.Stop();
        if (profile != null)
            profile.PlayAtPoint(profile.Pick(profile.deathClips), transform.position + Vector3.up * 1.3f,
                profile.combatVolume);
    }

    private void OnDisable()
    {
        if (voiceSource != null)
            voiceSource.Stop();
        if (stepSource != null)
            stepSource.Stop();
        if (actionSource != null)
            actionSource.Stop();
    }
}
