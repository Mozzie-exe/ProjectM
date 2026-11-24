using UnityEngine;

// Menünün radyo sesleri: arkada hafif parazit, üzerine gelince istasyon arama, seçince klik, başlarken parazit dalgası.
public class RadioUiAudio : MonoBehaviour
{
    public AudioClip staticLoop;
    public AudioClip hover;
    public AudioClip click;
    public AudioClip tick;
    public AudioClip transition;

    [Range(0f, 1f)] public float staticVolume = 0.22f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    AudioSource loop, oneShot;
    float loopTarget, lastTick;

    void Awake()
    {
        var group = GameAudio.Group(AudioChannel.UI);
        loop = gameObject.AddComponent<AudioSource>();
        loop.clip = staticLoop;
        loop.loop = true;
        loop.playOnAwake = false;
        loop.spatialBlend = 0f;
        loop.volume = 0f;
        loop.outputAudioMixerGroup = group;
        loop.ignoreListenerPause = true;

        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 0f;
        oneShot.outputAudioMixerGroup = group;
        oneShot.ignoreListenerPause = true;

        loopTarget = staticVolume;
        if (staticLoop) loop.Play();
    }

    void Update()
    {
        loop.volume = Mathf.MoveTowards(loop.volume, loopTarget, Time.unscaledDeltaTime * 0.6f);
    }

    public void SetStaticLevel(float level01) => loopTarget = staticVolume * level01;

    public void Hover() => Play(hover, 0.55f);
    public void Click() => Play(click, 1f);
    public void Transition() => Play(transition, 1f);

    public void Tick()
    {
        if (Time.unscaledTime - lastTick < 0.05f) return;
        lastTick = Time.unscaledTime;
        Play(tick, 0.5f);
    }

    void Play(AudioClip c, float v)
    {
        if (c) oneShot.PlayOneShot(c, v * sfxVolume);
    }
}
