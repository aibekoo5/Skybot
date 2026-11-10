using UnityEngine;

// BRAWLER — боксшы (Swat, мылтықсыз). АТПАЙДЫ. Ойыншыны көрсе өте жылдам жүгіріп келіп,
// жақыннан ұрады. Rifleman-нан әлдеқайда жылдам (6.5 м/с — ойыншыдан да сәл жылдам).
//
//  Idle ──көрді──▶ Chase ──жақын (≤ 1.7 м) + экранда──▶ WindUp (0.6 с: қызыл, бокс қалпы)
//   ▲               ▲ │ көрмей қалды (3 с)                     │
//   └───────────────┘ ▼                                         ▼
//                   Idle            Recover (1.2 с) ◀──── Punch (соққы бір рет тексеріледі)
//
// Әділдік: WindUp кезінде тоқтап тұрады, соққы бағыты бекітіледі —
// ойыншы ескертуді көріп артқа шегінсе, соққы жетпейді.
public class Brawler : EnemyBase
{
    public enum State { Idle, Chase, WindUp, Punch, Recover }

    [Header("Chase")]
    [SerializeField] float runSpeed = 6.5f;
    [SerializeField] float forgetTime = 3f;
    [SerializeField] Transform[] waypoints;          // міндетті емес: көрмей тұрғанда күзетеді
    [SerializeField] float walkSpeed = 1.5f;
    [SerializeField] AudioClip shoutClip;            // ойыншыны көргенде айқайлайды
    [SerializeField] float shoutCooldown = 5f;       // қайта айқайлағанша

    [Header("Punch")]
    [SerializeField] float attackRange = 1.7f;       // WindUp басталатын қашықтық
    [SerializeField] float windUp = 0.6f;            // ескерту ұзақтығы (0.3–0.8)
    [SerializeField] float hitRange = 2.0f;          // соққы жетеді
    [SerializeField] float hitAngle = 80f;
    [SerializeField] int damage = 1;
    [SerializeField] float knockback = 6f;
    [SerializeField] float punchTime = 0.35f;
    [SerializeField] float recoverTime = 1.2f;       // cooldown

    public State Current { get; private set; }

    float stateTime, lastSeen, waitUntil, nextShout;
    int wp;
    Vector3 lockedDir;
    bool hitDone;

    protected override void Start()
    {
        base.Start();
        Enter(State.Idle);
    }

    void Update()
    {
        if (!Ready) return;
        stateTime += Time.deltaTime;
        AnimSpeed();
        bool sees = CanSeePlayer();
        if (sees) lastSeen = Time.time;

        switch (Current)
        {
            case State.Idle:
                if (sees) { Enter(State.Chase); break; }
                Wander();
                break;

            case State.Chase:
                agent.SetDestination(player.position);
                if (!sees && Time.time - lastSeen > forgetTime) { Enter(State.Idle); break; }
                if (FlatDistanceToPlayer() <= attackRange && IsOnScreen()) Enter(State.WindUp);
                break;

            case State.WindUp:
                Telegraph(stateTime / windUp);
                if (stateTime >= windUp) Enter(State.Punch);
                break;

            case State.Punch:
                if (!hitDone && stateTime >= punchTime * 0.4f) { hitDone = true; TryHit(); }
                if (stateTime >= punchTime) Enter(State.Recover);
                break;

            case State.Recover:
                FacePlayer(120f);
                if (stateTime >= recoverTime) Enter(sees ? State.Chase : State.Idle);
                break;
        }
    }

    void Enter(State s)
    {
        State prev = Current;
        Current = s;
        stateTime = 0f;
        ShowState(s.ToString());
        if (anim != null) anim.SetBool("Punching", s == State.WindUp || s == State.Punch);

        switch (s)
        {
            case State.Idle:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                agent.updateRotation = true;
                break;
            case State.Chase:
                agent.isStopped = false;
                agent.speed = runSpeed;
                agent.updateRotation = true;
                // жаңа көрді (Idle-ден) — айқайлап, жүгіреді
                if (prev == State.Idle && Time.time >= nextShout)
                {
                    PlaySound(shoutClip, 1f, 0.1f);
                    nextShout = Time.time + shoutCooldown;
                }
                break;
            case State.WindUp:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updateRotation = false;
                // бағыт ОСЫ сәтте бекітіледі
                lockedDir = player.position - transform.position; lockedDir.y = 0f;
                lockedDir = lockedDir.sqrMagnitude > 0.001f ? lockedDir.normalized : transform.forward;
                transform.rotation = Quaternion.LookRotation(lockedDir);
                PlayTelegraphSound();
                break;
            case State.Punch:
                hitDone = false;
                break;
            case State.Recover:
                agent.isStopped = true;
                agent.updateRotation = false;
                break;
        }
    }

    void Wander()
    {
        if (waypoints == null || waypoints.Length == 0) { agent.isStopped = true; return; }
        if (agent.pathPending || agent.remainingDistance > 0.3f) return;
        if (waitUntil == 0f) waitUntil = Time.time + 1.5f;
        if (Time.time < waitUntil) return;
        waitUntil = 0f;
        wp = (wp + 1) % waypoints.Length;
        if (waypoints[wp] != null) agent.SetDestination(waypoints[wp].position);
    }

    void TryHit()
    {
        Vector3 d = player.position - transform.position;
        float dy = Mathf.Abs(d.y); d.y = 0f;
        if (d.magnitude <= hitRange && dy < 1.5f && Vector3.Angle(lockedDir, d) <= hitAngle * 0.5f)
        {
            HurtPlayer(damage);
            if (playerRb != null) playerRb.linearVelocity = lockedDir * knockback + Vector3.up * 2.5f;
        }
    }

    protected override void OnHit()
    {
        if (Current == State.Idle) { lastSeen = Time.time; FacePlayer(720f); Enter(State.Chase); }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
