using UnityEngine;

// RIFLEMAN — мылтықты сарбаз (Swat). Жүреді, ойыншыны көрсе тоқтап көздейді де атады.
// Ойыншы қашса — артынан қуып келіп, көрген сәтте қайта көздейді.
//
//  Patrol ──көрді──▶ Chase ──көреді + қашықтық ≤ fireRange + экранда──▶ Aim (0.6 с: лазер, қызыл)
//    ▲                 ▲  │ көрмей қалды                                     │ көрмей қалды → Chase
//    │                 │  ▼                                                  ▼
//    └──уақыт бітті── Search                         Cooldown ◀────────── Fire (оқ)
//                                                       └──▶ Aim (әлі көреді) / Chase
public class Rifleman : EnemyBase
{
    public enum State { Patrol, Chase, Search, Aim, Fire, Cooldown }

    [Header("Patrol")]
    [SerializeField] Transform[] waypoints;
    [SerializeField] float patrolSpeed = 1.6f;
    [SerializeField] float waypointWait = 1.5f;

    [Header("Chase / Search")]
    [SerializeField] float chaseSpeed = 2.8f;         // Brawler-ден әлдеқайда баяу
    [SerializeField] float searchTime = 4f;
    [SerializeField] float searchTurnSpeed = 90f;

    [Header("Aim / Fire")]
    [SerializeField] Transform muzzle;                // мылтықтың ұшы
    [SerializeField] LineRenderer laser;              // көздеу сызығы (ескерту)
    [SerializeField] float fireRange = 11f;           // осыдан жақын болса тоқтап көздейді
    [SerializeField] float aimTime = 0.6f;            // ескерту ұзақтығы (0.3–0.8)
    [SerializeField] float aimTurnSpeed = 200f;
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] float projectileSpeed = 10f;
    [SerializeField] int damage = 1;
    [SerializeField] float cooldown = 1.4f;
    [SerializeField] AudioClip shotClip;              // ату дыбысы

    public State Current { get; private set; }

    int wp;
    float stateTime, waitUntil;
    Vector3 lastKnown, aimPoint;

    protected override void Start()
    {
        base.Start();
        Enter(State.Patrol);
    }

    void Update()
    {
        if (!Ready) return;
        stateTime += Time.deltaTime;
        AnimSpeed();
        bool sees = CanSeePlayer();
        if (sees) lastKnown = player.position;
        bool canShoot = sees && FlatDistanceToPlayer() <= fireRange && IsOnScreen();

        switch (Current)
        {
            case State.Patrol:
                if (sees) { Enter(State.Chase); break; }
                PatrolStep();
                break;

            case State.Chase:
                agent.SetDestination(sees ? player.position : lastKnown);
                if (canShoot) { Enter(State.Aim); break; }
                if (!sees && !agent.pathPending && agent.remainingDistance < 0.6f) Enter(State.Search);
                break;

            case State.Search:
                if (sees) { Enter(State.Chase); break; }
                transform.Rotate(0f, searchTurnSpeed * Time.deltaTime, 0f);   // айналып қарайды
                if (stateTime > searchTime) Enter(State.Patrol);
                break;

            case State.Aim:
                if (!sees) { Enter(State.Chase); break; }   // ойыншы тығылды — атпайды, қуады
                FacePlayer(aimTurnSpeed);
                aimPoint = PlayerChest;
                Telegraph(stateTime / aimTime);
                DrawLaser(stateTime / aimTime);
                if (stateTime >= aimTime) Enter(State.Fire);
                break;

            case State.Fire:
                Shoot();
                Enter(State.Cooldown);
                break;

            case State.Cooldown:
                if (sees) FacePlayer(aimTurnSpeed * 0.5f);
                if (stateTime >= cooldown) Enter(canShoot ? State.Aim : State.Chase);
                break;
        }
    }

    void Enter(State s)
    {
        Current = s;
        stateTime = 0f;
        ShowState(s.ToString());
        if (laser != null) laser.enabled = s == State.Aim;
        bool standing = s == State.Aim || s == State.Fire || s == State.Cooldown || s == State.Search;
        if (anim != null) anim.SetBool("Aiming", s == State.Aim || s == State.Fire || s == State.Cooldown);
        agent.updateRotation = !standing;

        if (standing)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else
        {
            agent.isStopped = false;
            agent.speed = s == State.Chase ? chaseSpeed : patrolSpeed;
        }

        if (s == State.Patrol) GoToWaypoint();
        if (s == State.Aim) PlayTelegraphSound();
    }

    void PatrolStep()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (agent.pathPending || agent.remainingDistance > 0.3f) return;
        if (waitUntil == 0f) waitUntil = Time.time + waypointWait;   // нүктеде сәл тұрады
        if (Time.time < waitUntil) return;
        waitUntil = 0f;
        wp = (wp + 1) % waypoints.Length;
        GoToWaypoint();
    }

    void GoToWaypoint()
    {
        if (waypoints != null && waypoints.Length > 0 && waypoints[wp] != null)
            agent.SetDestination(waypoints[wp].position);
    }

    void DrawLaser(float t)
    {
        if (laser == null || muzzle == null) return;
        Vector3 from = muzzle.position;
        Vector3 dir = (aimPoint - from).normalized;
        float len = Vector3.Distance(from, aimPoint);
        laser.SetPosition(0, from);
        laser.SetPosition(1, from + dir * len);
        laser.startWidth = laser.endWidth = Mathf.Lerp(0.015f, 0.05f, t);   // атар алдында қалыңдайды
        laser.enabled = !(t > 0.6f && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f);
    }

    void Shoot()
    {
        if (anim != null) anim.SetTrigger("Fire");
        PlaySound(shotClip);
        if (projectilePrefab == null || muzzle == null) return;
        Vector3 dir = (aimPoint - muzzle.position).normalized;
        EnemyProjectile p = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
        p.Launch(dir * projectileSpeed, damage, transform);
    }

    // атқан ойыншыны бірден «естиді» — сол жаққа бұрылып, қуады
    protected override void OnHit()
    {
        lastKnown = player.position;
        if (Current == State.Patrol || Current == State.Search)
        {
            FacePlayer(720f);
            Enter(State.Chase);
        }
    }

    protected override void Die()
    {
        if (laser != null) laser.enabled = false;
        base.Die();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, fireRange);
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
            if (waypoints[i] != null && waypoints[(i + 1) % waypoints.Length] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[(i + 1) % waypoints.Length].position);
    }
}
