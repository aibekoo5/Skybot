using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

// Үш жаудың ортақ «сезімдері» мен денесі: көру, HP, ескерту (telegraph), экранда тұр ма.
// МІНЕЗ-ҚҰЛЫҚ мұнда ЖОҚ — әр жаудың өз state machine-і бар (Rifleman, MachineGunTurret, Brawler).
//
// Көру (CanSeePlayer): 1) қашықтық ≤ viewDistance  2) бұрыш ≤ viewAngle/2
//                      3) көзден ойыншының кеудесіне Raycast — қабырға тұрса, көрмейді.
// Әділдік: шабуыл тек жау экранда көрініп тұрғанда басталады (IsOnScreen).
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Денсаулық")]
    [SerializeField] protected int maxHp = 3;

    [Header("Көру")]
    [SerializeField] protected Transform eye;                 // көз (Raycast осыдан басталады)
    [SerializeField] protected float viewDistance = 14f;
    [SerializeField, Range(10f, 360f)] protected float viewAngle = 120f;
    [SerializeField] protected LayerMask sightBlockers = ~0;  // көруді не бөгейді (қабырғалар)
    [SerializeField] protected float playerChestHeight = 1.2f;

    [Header("Дене / анимация")]
    [SerializeField] protected Animator anim;                 // адам тәрізді жауларда (бос болуы мүмкін)
    [SerializeField] protected Renderer[] bodyRenderers;      // ескерту кезінде қызыл жыпылықтайды
    [SerializeField] protected Color bodyTint = Color.white;  // жау түрін ажырату үшін реңк
    [SerializeField] protected Color telegraphColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] protected AudioClip telegraphClip;       // ескерту дыбысы (міндетті емес)

    [Header("Күйді көрсету (бейне үшін)")]
    [SerializeField] protected TextMeshPro stateLabel;        // басының үстіндегі жазу
    [SerializeField] protected bool showStateLabel = true;

    protected NavMeshAgent agent;                             // пулеметте жоқ
    protected Transform player;
    protected Health playerHealth;
    protected Rigidbody playerRb;
    protected Camera cam;
    protected AudioSource audioSource;
    protected int hp;
    protected bool dead;

    MaterialPropertyBlock mpb;
    Color[] baseColors;
    float flashUntil;                                         // оқ тигенде ақ жарқыл
    float telegraphT = -1f;                                   // осы кадрда ескерту бар ма (−1 = жоқ)
    bool tinted = true;                                       // бірінші кадрда реңкті қоямыз

    public bool IsDead => dead;
    public event System.Action<EnemyBase> Died;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        mpb = new MaterialPropertyBlock();

        if (bodyRenderers == null || bodyRenderers.Length == 0)
        {
            bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (bodyRenderers.Length == 0) bodyRenderers = GetComponentsInChildren<MeshRenderer>();
        }
        baseColors = new Color[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Material m = bodyRenderers[i].sharedMaterial;
            Color c = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            baseColors[i] = c * bodyTint;
        }
    }

    protected virtual void Start()
    {
        hp = maxHp;
        cam = Camera.main;

        // ойыншыны бір рет табамыз (Update-те іздемейміз)
        Movement mv = FindFirstObjectByType<Movement>();
        if (mv != null)
        {
            player = mv.transform;
            playerHealth = mv.GetComponent<Health>();
            playerRb = mv.GetComponent<Rigidbody>();
        }
        if (stateLabel != null) stateLabel.gameObject.SetActive(showStateLabel);
    }

    protected virtual void LateUpdate()
    {
        // күй жазуы камераға қарап тұрады
        if (stateLabel != null && showStateLabel && cam != null)
            stateLabel.transform.rotation = Quaternion.LookRotation(stateLabel.transform.position - cam.transform.position);

        // түс: оқ тиген жарқыл > ескерту > қалыпты
        if (Time.time < flashUntil) Tint(Color.white, 0.8f);
        else if (telegraphT >= 0f)
        {
            float freq = Mathf.Lerp(6f, 20f, telegraphT);
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * freq * Mathf.PI);
            Tint(telegraphColor, Mathf.Lerp(0.35f, 1f, k));
        }
        else if (tinted) ResetTint();
        telegraphT = -1f;   // келесі кадрда күй қайта сұрау керек
    }

    // ---------- сезу ----------

    protected bool Ready => !dead && player != null && (agent == null || agent.isOnNavMesh);
    protected Vector3 PlayerChest => player.position + Vector3.up * playerChestHeight;
    protected Vector3 EyePos => eye != null ? eye.position : transform.position + Vector3.up * 1.5f;

    protected float FlatDistanceToPlayer()
    {
        Vector3 d = player.position - transform.position; d.y = 0f;
        return d.magnitude;
    }

    public bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 toPlayer = PlayerChest - EyePos;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;                                   // 1) қашықтық

        Vector3 flat = toPlayer; flat.y = 0f;
        if (Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;   // 2) бұрыш

        // 3) көру сызығы: көз мен ойыншының арасында қабырға бар ма
        foreach (RaycastHit h in Physics.RaycastAll(EyePos, toPlayer / dist, dist, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(transform)) continue;   // өз денесі
            if (h.transform.IsChildOf(player)) continue;      // ойыншының өзі
            if (h.collider.GetComponentInParent<EnemyBase>() != null) continue;   // басқа жау көруді бөгемейді
            return false;                                     // қабырға
        }
        return true;
    }

    // ойыншы жауды экранда көріп тұр ма — экраннан тыс шабуыл жоқ
    protected bool IsOnScreen()
    {
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up);
        return v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
    }

    protected void FacePlayer(float turnSpeed)
    {
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
    }

    protected void HurtPlayer(int damage)
    {
        if (playerHealth != null) playerHealth.TakeDamage(damage);
    }

    // анимацияға жылдамдық береміз
    protected void AnimSpeed()
    {
        if (anim != null && agent != null) anim.SetFloat("Speed", agent.velocity.magnitude);
    }

    // ---------- ескерту ----------

    // t = 0..1: ескерту басталғаннан бері қанша өтті — әр кадр Update-тен шақырылады,
    // қызыл жыпылықтау жиілейді; шақырылмаған кадрда түс өзі қалпына келеді
    protected void Telegraph(float t)
    {
        telegraphT = Mathf.Clamp01(t);
    }

    // дыбыс: биіктігі сәл кездейсоқ — қатарынан атқанда бірдей естілмейді
    protected void PlaySound(AudioClip clip, float volume = 1f, float pitchJitter = 0.08f)
    {
        if (audioSource == null || clip == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        audioSource.PlayOneShot(clip, volume);
    }

    protected void PlayTelegraphSound()
    {
        if (audioSource != null && telegraphClip != null) audioSource.PlayOneShot(telegraphClip);
    }

    void ResetTint()
    {
        tinted = false;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", baseColors[i]);
            mpb.SetColor("_EmissionColor", Color.black);
            bodyRenderers[i].SetPropertyBlock(mpb);
        }
    }

    void Tint(Color c, float amount)
    {
        tinted = true;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", Color.Lerp(baseColors[i], c, amount));
            mpb.SetColor("_EmissionColor", c * amount * 1.5f);
            bodyRenderers[i].SetPropertyBlock(mpb);
        }
    }

    protected void ShowState(string s)
    {
        if (stateLabel != null && showStateLabel) stateLabel.text = s;
    }

    // ---------- зақым ----------

    public virtual void TakeHit(int damage)
    {
        if (dead) return;
        hp -= damage;
        flashUntil = Time.time + 0.08f;
        if (hp <= 0) { Die(); return; }
        OnHit();
    }

    protected virtual void OnHit() { }   // мысалы, атқанда ойыншыны бірден «көреді»

    protected virtual void Die()
    {
        dead = true;
        StopAllCoroutines();
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        ShowState("");
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Died?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        // адам тәрізді жау құлау анимациясын ойнайды, пулемет кішірейіп жоғалады
        if (anim != null && HasParam("Die"))
        {
            anim.SetTrigger("Die");
            yield return new WaitForSeconds(2.5f);
        }
        Vector3 s = transform.localScale;
        for (float t = 0; t < 0.35f; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(s, Vector3.zero, t / 0.35f);
            yield return null;
        }
        Destroy(gameObject);
    }

    protected bool HasParam(string name)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters) if (p.name == name) return true;
        return false;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // көру конусы — Scene терезесінде
        Vector3 o = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
        Gizmos.color = Color.yellow;
        Quaternion l = Quaternion.Euler(0, -viewAngle * 0.5f, 0), r = Quaternion.Euler(0, viewAngle * 0.5f, 0);
        Gizmos.DrawLine(o, o + l * transform.forward * viewDistance);
        Gizmos.DrawLine(o, o + r * transform.forward * viewDistance);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}
