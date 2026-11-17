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

    [Header("Баланс (Assets/Data/Enemies/RiflemanStats.asset)")]
    [SerializeField] RiflemanStats stats;
    protected override EnemyStats BaseStats => stats;

    [Header("Patrol")]
    [SerializeField] Transform[] waypoints;           // LevelRunner деректен береді

    [Header("Aim / Fire")]
    [SerializeField] Transform muzzle;                // мылтықтың ұшы
    [SerializeField] LineRenderer laser;              // көздеу сызығы (ескерту)
    [SerializeField] EnemyProjectile projectilePrefab;
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
        bool canShoot = sees && FlatDistanceToPlayer() <= stats.fireRange && IsOnScreen();

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
                transform.Rotate(0f, stats.searchTurnSpeed * Time.deltaTime, 0f);   // айналып қарайды
                if (stateTime > stats.searchTime) Enter(State.Patrol);
                break;

            case State.Aim:
                if (!sees) { Enter(State.Chase); break; }   // ойыншы тығылды — атпайды, қуады
                FacePlayer(stats.aimTurnSpeed);
                aimPoint = PlayerChest;
                Telegraph(stateTime / stats.aimTime);
                DrawLaser(stateTime / stats.aimTime);
                if (stateTime >= stats.aimTime) Enter(State.Fire);
                break;

            case State.Fire:
                Shoot();
                Enter(State.Cooldown);
                break;

            case State.Cooldown:
                if (sees) FacePlayer(stats.aimTurnSpeed * 0.5f);
                if (stateTime >= stats.cooldown) Enter(canShoot ? State.Aim : State.Chase);
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
            agent.speed = s == State.Chase ? stats.chaseSpeed : stats.patrolSpeed;
        }

        if (s == State.Patrol) GoToWaypoint();
        if (s == State.Aim) PlayTelegraphSound();
    }

    void PatrolStep()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (agent.pathPending || agent.remainingDistance > 0.3f) return;
        if (waitUntil == 0f) waitUntil = Time.time + stats.waypointWait;   // нүктеде сәл тұрады
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
        p.Launch(dir * stats.projectileSpeed, stats.damage, transform);
    }

    public override void SetPatrolPoints(Transform[] points) => waypoints = points;

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
        if (stats != null) Gizmos.DrawWireSphere(transform.position, stats.fireRange);
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
            if (waypoints[i] != null && waypoints[(i + 1) % waypoints.Length] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[(i + 1) % waypoints.Length].position);
    }
}
