using UnityEngine;

/// <summary>
/// Ahirdaki inek: cogunlukla samandan yer (Eating), ara sira basini kaldirir (Idle / IdleBreak)
/// ve boegurur. Her inek farkli zamanlamayla calisir.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CowBarnBehaviour : MonoBehaviour
{
    public Animator animator;

    [Header("Animator state isimleri")]
    public string eatState = "Eating";
    public string idleState = "Idle";
    public string idleBreakState = "IdleBreak";

    [Header("Zamanlama")]
    public Vector2 eatDuration = new Vector2(8f, 18f);
    public Vector2 idleDuration = new Vector2(3f, 6f);
    [Range(0f, 1f)] public float idleBreakChance = 0.35f;
    public float crossFade = 0.6f;
    public Vector2 animSpeed = new Vector2(0.9f, 1.1f);

    [Header("Boegurme")]
    public AudioClip[] mooClips;
    [Range(0f, 1f)] public float mooChance = 0.6f;
    public Vector2 mooPitch = new Vector2(0.88f, 1.08f);
    public float minSecondsBetweenMoos = 10f;
    public float mooDelay = 0.5f;

    AudioSource audioSrc;
    bool eating = true;
    float timer;
    float lastMoo = -999f;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (animator)
        {
            animator.speed = Random.Range(animSpeed.x, animSpeed.y);
            animator.Play(eatState, 0, Random.value);
        }
        timer = Random.Range(2f, eatDuration.y);
        lastMoo = Time.time - Random.Range(0f, minSecondsBetweenMoos);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        eating = !eating;
        if (eating)
        {
            timer = Random.Range(eatDuration.x, eatDuration.y);
            Fade(eatState);
        }
        else
        {
            timer = Random.Range(idleDuration.x, idleDuration.y);
            Fade(Random.value < idleBreakChance ? idleBreakState : idleState);
            TryMoo(false);
        }
    }

    void Fade(string state)
    {
        if (animator) animator.CrossFadeInFixedTime(state, crossFade, 0);
    }

    bool TryMoo(bool force)
    {
        if (mooClips == null || mooClips.Length == 0) return false;
        if (!force && Time.time - lastMoo < minSecondsBetweenMoos) return false;
        if (!force && Random.value > mooChance) return false;
        AudioClip clip = mooClips[Random.Range(0, mooClips.Length)];
        if (!clip) return false;
        float p = Random.Range(mooPitch.x, mooPitch.y);
        audioSrc.clip = clip;
        audioSrc.pitch = p;
        audioSrc.PlayDelayed(mooDelay);
        lastMoo = Time.time;
        timer = Mathf.Max(timer, mooDelay + clip.length / p + 0.5f);
        return true;
    }

    /// <summary>Disaridan (ornegin oyuncu yaklasinca bir trigger ile) boegurtmek icin.</summary>
    public void MooNow()
    {
        if (eating)
        {
            eating = false;
            Fade(idleState);
        }
        timer = idleDuration.y;
        TryMoo(true);
    }
}
