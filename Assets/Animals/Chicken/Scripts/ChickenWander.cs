using UnityEngine;

/// <summary>
/// Kumesteki tavuk: alan icinde rastgele dolasir, durur, yerden yem gagalar, etrafina bakar
/// ve ara sira gidaklar. Iskelet/animasyon gerektirmez; 'visual' objesini prosedurel hareket ettirir.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ChickenWander : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Tavugun modeli (ayaklari pivotta olacak sekilde)")]
    public Transform visual;
    [Tooltip("Dolasma alani: bu objenin yerel X/Z ekseninde 'areaSize' boyutunda dikdortgen")]
    public Transform area;
    public Vector2 areaSize = new Vector2(10f, 6f);
    [Tooltip("Yuruyus hedefi secerken engel kontrolu icin")]
    public LayerMask obstacleMask = ~0;

    [Header("Hareket")]
    public Vector2 walkSpeed = new Vector2(0.9f, 1.5f);
    public float turnSpeed = 300f;
    public Vector2 walkDistance = new Vector2(1.2f, 5f);
    public Vector2 idleTime = new Vector2(0.6f, 2.5f);
    public Vector2 peckTime = new Vector2(1.5f, 4.5f);
    public float stepBob = 0.035f;
    public float peckAngle = 38f;

    [Header("Gidaklama")]
    public AudioClip[] cluckClips;
    public Vector2 cluckInterval = new Vector2(6f, 16f);
    public Vector2 cluckPitch = new Vector2(0.9f, 1.2f);

    enum State { Idle, Peck, Walk, Look }
    State state;
    float timer, speed, nextCluck, seed, pitch, roll, lookYaw;
    Vector3 target;
    AudioSource audioSrc;
    Vector3 visualBasePos;
    Quaternion visualBaseRot;
    float scale = 1f;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (visual) { visualBasePos = visual.localPosition; visualBaseRot = visual.localRotation; }
        scale = transform.lossyScale.x;
    }

    void Start()
    {
        seed = Random.value * 100f;
        transform.Rotate(0f, Random.Range(0f, 360f), 0f);
        Enter((State)Random.Range(0, 3));
        nextCluck = Time.time + Random.Range(1f, cluckInterval.y);
    }

    void Enter(State s)
    {
        state = s;
        switch (s)
        {
            case State.Idle: timer = Random.Range(idleTime.x, idleTime.y); break;
            case State.Look: timer = Random.Range(0.8f, 2f); lookYaw = Random.Range(-60f, 60f); break;
            case State.Peck: timer = Random.Range(peckTime.x, peckTime.y); break;
            case State.Walk:
                if (!PickTarget()) { Enter(State.Peck); return; }
                speed = Random.Range(walkSpeed.x, walkSpeed.y) * scale;
                timer = 12f;
                break;
        }
    }

    bool PickTarget()
    {
        if (!area) return false;
        for (int i = 0; i < 12; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float d = Random.Range(walkDistance.x, walkDistance.y) * scale;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
            Vector3 l = area.InverseTransformPoint(p);
            if (Mathf.Abs(l.x) > areaSize.x * 0.5f || Mathf.Abs(l.z) > areaSize.y * 0.5f) continue;
            Vector3 up = Vector3.up * 0.25f * scale;
            if (Physics.Linecast(transform.position + up, p + up, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            p.y = transform.position.y;
            target = p;
            return true;
        }
        return false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time + seed;
        timer -= dt;

        float wantPitch = 0f, wantRoll = 0f, bob = 0f, yawOffset = 0f;

        switch (state)
        {
            case State.Idle:
                wantPitch = Mathf.Sin(t * 1.7f) * 3f;
                if (timer <= 0f) Enter(Random.value < 0.45f ? State.Walk : (Random.value < 0.6f ? State.Peck : State.Look));
                break;

            case State.Look:
                yawOffset = lookYaw;
                wantPitch = -6f;
                if (timer <= 0f) Enter(Random.value < 0.5f ? State.Walk : State.Peck);
                break;

            case State.Peck:
            {
                // hizli asagi-yukari gagalama vuruslari
                float cyc = Mathf.Repeat(t * 2.6f, 1f);
                float hit = cyc < 0.35f ? Mathf.Sin(cyc / 0.35f * Mathf.PI) : 0f;
                wantPitch = 12f + hit * peckAngle;
                if (timer <= 0f) Enter(Random.value < 0.6f ? State.Walk : State.Idle);
                break;
            }

            case State.Walk:
            {
                Vector3 to = target - transform.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.08f * scale || timer <= 0f) { Enter(Random.value < 0.65f ? State.Peck : State.Idle); break; }
                Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
                float facing = Vector3.Dot(transform.forward, to.normalized);
                if (facing > 0.5f)
                {
                    float step = Mathf.Min(dist, speed * dt * Mathf.Clamp01(facing));
                    transform.position += transform.forward * step;
                }
                float stepFreq = 7.5f;
                bob = Mathf.Abs(Mathf.Sin(t * stepFreq)) * stepBob * scale;
                wantPitch = 6f + Mathf.Sin(t * stepFreq * 2f) * 6f; // kafa sallama
                wantRoll = Mathf.Sin(t * stepFreq) * 5f;
                break;
            }
        }

        float k = 1f - Mathf.Exp(-14f * dt);
        pitch = Mathf.Lerp(pitch, wantPitch, k);
        roll = Mathf.Lerp(roll, wantRoll, k);
        if (visual)
        {
            visual.localRotation = Quaternion.Euler(0f, yawOffset * Mathf.Clamp01(1f), 0f) * Quaternion.Euler(pitch, 0f, roll) * visualBaseRot;
            visual.localPosition = visualBasePos + Vector3.up * (bob / Mathf.Max(0.0001f, scale));
        }

        if (Time.time >= nextCluck)
        {
            nextCluck = Time.time + Random.Range(cluckInterval.x, cluckInterval.y);
            Cluck();
        }
    }

    public void Cluck()
    {
        if (cluckClips == null || cluckClips.Length == 0) return;
        var c = cluckClips[Random.Range(0, cluckClips.Length)];
        if (!c) return;
        audioSrc.pitch = Random.Range(cluckPitch.x, cluckPitch.y);
        audioSrc.PlayOneShot(c);
    }

    void OnDrawGizmosSelected()
    {
        if (!area) return;
        Gizmos.matrix = area.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(areaSize.x, 0.2f, areaSize.y));
    }
}
