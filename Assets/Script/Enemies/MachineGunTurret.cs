using UnityEngine;

// MACHINE GUN — штативтегі пулемет. ЕШҚАШАН ҚОЗҒАЛМАЙДЫ, тек басын (yaw) бұрады.
//
//  Scan ──көрді + экранда──▶ SpinUp (0.7 с: ұңғылар айнала бастайды, лазер, қызыл)
//    ▲                           │ көрмей қалды → Scan
//    │                           ▼
//    └────── Overheat (2 с, ұңғы қызарып суиды) ◀── Burst (8 оқ сериямен)
//
// Burst кезінде баяу бұрылады (40°/с) — ойыншы жанына жүгірсе, оқтар артында қалады.
// Қабырға оқты тоқтатады → тығылу да жұмыс істейді.
public class MachineGunTurret : EnemyBase
{
    public enum State { Scan, SpinUp, Burst, Overheat }

    [Header("Бөлшектер")]
    [SerializeField] Transform yawPivot;             // бұрылатын бас
    [SerializeField] Transform barrels;              // айналатын ұңғылар
    [SerializeField] Transform muzzle;
    [SerializeField] LineRenderer laser;
    [SerializeField] Renderer barrelRenderer;        // қызып кететін ұңғы

    [Header("Баланс (Assets/Data/Enemies/MachineGunStats.asset)")]
    [SerializeField] MachineGunStats stats;
    protected override EnemyStats BaseStats => stats;

    [Header("Fire")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] AudioClip shotClip;             // әр оқтың дыбысы

    public State Current { get; private set; }

    float stateTime, spin, nextShot;
    int shotsLeft;
    MaterialPropertyBlock heatBlock;

    protected override void Start()
    {
        base.Start();
        heatBlock = new MaterialPropertyBlock();
        Enter(State.Scan);
    }

    // пулеметтің «алды» — бұрылатын басы
    Vector3 Forward => yawPivot != null ? yawPivot.forward : transform.forward;

    void Update()
    {
        if (!Ready) return;
        stateTime += Time.deltaTime;
        bool sees = CanSeePlayer();

        switch (Current)
        {
            case State.Scan:
                float yaw = Mathf.Sin(Time.time * stats.scanSpeed) * stats.scanAngle * 0.5f;
                TurnHead(Quaternion.Euler(0, transform.eulerAngles.y + yaw, 0), stats.scanTurnSpeed);
                spin = Mathf.MoveTowards(spin, 0f, 600f * Time.deltaTime);
                if (sees && IsOnScreen()) Enter(State.SpinUp);
                break;

            case State.SpinUp:
                if (!sees) { Enter(State.Scan); break; }
                AimHead(stats.trackTurnSpeed);
                spin = Mathf.Lerp(0f, 1440f, stateTime / stats.spinUpTime);   // ұңғылар үдеп айналады (тек көрініс)
                Telegraph(stateTime / stats.spinUpTime);
                DrawLaser(stateTime / stats.spinUpTime);
                if (stateTime >= stats.spinUpTime) Enter(State.Burst);
                break;

            case State.Burst:
                AimHead(stats.burstTurnSpeed);
                spin = 1440f;
                if (Time.time >= nextShot)
                {
                    Shoot();
                    shotsLeft--;
                    nextShot = Time.time + stats.burstInterval;
                    if (shotsLeft <= 0) Enter(State.Overheat);
                }
                break;

            case State.Overheat:
                spin = Mathf.MoveTowards(spin, 0f, 900f * Time.deltaTime);
                Heat(1f - stateTime / stats.overheatTime);
                if (stateTime >= stats.overheatTime) Enter(sees && IsOnScreen() ? State.SpinUp : State.Scan);
                break;
        }

        if (barrels != null) barrels.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
    }

    void Enter(State s)
    {
        Current = s;
        stateTime = 0f;
        ShowState(s.ToString());
        if (laser != null) laser.enabled = s == State.SpinUp;
        if (s == State.SpinUp) PlayTelegraphSound();
        if (s == State.Burst) { shotsLeft = stats.burstCount; nextShot = Time.time; }
        if (s == State.Scan) Heat(0f);
    }

    // басы тек өз секторында бұрылады (артына қарай алмайды)
    void TurnHead(Quaternion target, float speed)
    {
        if (yawPivot == null) return;
        float rel = Mathf.DeltaAngle(transform.eulerAngles.y, target.eulerAngles.y);
        rel = Mathf.Clamp(rel, -viewAngle * 0.5f, viewAngle * 0.5f);
        Quaternion clamped = Quaternion.Euler(0, transform.eulerAngles.y + rel, 0);
        yawPivot.rotation = Quaternion.RotateTowards(yawPivot.rotation, clamped, speed * Time.deltaTime);
    }

    void AimHead(float speed)
    {
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.001f) TurnHead(Quaternion.LookRotation(d), speed);
    }

    void DrawLaser(float t)
    {
        if (laser == null || muzzle == null) return;
        Vector3 from = muzzle.position;
        Vector3 dir = Forward;
        dir.y = (PlayerChest - from).normalized.y;   // биіктігі ойыншыға
        float len = viewDistance;
        if (Physics.Raycast(from, dir, out RaycastHit h, len, sightBlockers, QueryTriggerInteraction.Ignore))
            len = h.distance;
        laser.SetPosition(0, from);
        laser.SetPosition(1, from + dir.normalized * len);
        laser.startWidth = laser.endWidth = Mathf.Lerp(0.02f, 0.06f, t);
        laser.enabled = !(t > 0.6f && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f);
    }

    void Shoot()
    {
        PlaySound(shotClip, 0.8f, 0.12f);
        if (projectilePrefab == null || muzzle == null) return;
        // басы қараған жаққа (ойыншыға емес!) — сондықтан жанына жүгірсе құтылады
        Vector3 dir = Forward;
        dir.y = (PlayerChest - muzzle.position).normalized.y;
        dir = Quaternion.Euler(Random.Range(-stats.spread, stats.spread), Random.Range(-stats.spread, stats.spread), 0f) * dir.normalized;
        EnemyProjectile p = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
        p.Launch(dir * stats.projectileSpeed, stats.damage, transform);
    }

    void Heat(float k)
    {
        if (barrelRenderer == null) return;
        barrelRenderer.GetPropertyBlock(heatBlock);
        heatBlock.SetColor("_EmissionColor", new Color(2.5f, 0.6f, 0.1f) * Mathf.Clamp01(k));
        barrelRenderer.SetPropertyBlock(heatBlock);
    }

    protected override void Die()
    {
        if (laser != null) laser.enabled = false;
        base.Die();
    }
}
