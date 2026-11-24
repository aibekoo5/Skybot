using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Level 3 бастығы: HP, екі шабуыл (оқ желпуіші + тас жаңбыры), екінші фаза.
// Барлық сандар — BossStats asset-те. Asset-ті ӨЗГЕРТПЕЙМІЗ: Start-та өз айнымалыларымызға
// көшіреміз, екінші фазада тек сол көшірме өзгереді.
public class Boss : MonoBehaviour
{
    [Header("Баланс (Assets/Data/BossStats.asset)")]
    [SerializeField] BossStats stats;

    [Header("HP")]
    public Slider hpBar;                    // экрандағы HP жолағы

    [Header("Шабуыл")]
    public Transform player;
    public BossProjectile fireballPrefab;   // желпуіш оғы
    public Hazard rockPrefab;               // жоғарыдан түсетін тас
    public GameObject warningPrefab;        // қызыл белгі "!" (collider-сіз)
    public LayerMask groundMask;            // тас түсетін еден (Ground layer)

    [Header("Дыбыс")]
    public AudioClip shootClip;
    public AudioClip hitClip;
    public AudioClip deathClip;

    // ағымдағы мәндер (екінші фазада өзгереді)
    float pause;
    int fanCount, rockCount;
    float fanAngle;

    int hp;
    bool phase2;
    bool dead;
    AudioSource src;

    void Start()
    {
        hp = stats.maxHp;
        pause = stats.pause;
        fanCount = stats.fanCount;
        fanAngle = stats.fanAngle;
        rockCount = stats.rockCount;
        src = GetComponent<AudioSource>();

        if (hpBar != null)
        {
            hpBar.maxValue = stats.maxHp;
            hpBar.value = hp;
        }

        StartCoroutine(AttackLoop());
    }

    void Update()
    {
        if (dead || player == null) return;

        // бастық әрқашан ойыншыға қарап тұрады
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    // шабуыл → үзіліс → шабуыл → үзіліс ...
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(stats.startDelay);

        while (!dead)
        {
            FireFan();
            yield return new WaitForSeconds(pause);

            yield return StartCoroutine(RockRain());
            yield return new WaitForSeconds(pause);
        }
    }

    // алдыға бірнеше оқ желпуіш сияқты шашырайды
    void FireFan()
    {
        if (fireballPrefab == null) return;

        for (int i = 0; i < fanCount; i++)
        {
            // -fanAngle/2 ... +fanAngle/2 аралығына тең бөлеміз
            float t = fanCount > 1 ? (float)i / (fanCount - 1) : 0.5f;
            float angle = Mathf.Lerp(-fanAngle / 2f, fanAngle / 2f, t);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            // әр оқ өз бағытымен шығады (бір-біріне соғылмайды), ойыншының биіктігінде
            Vector3 pos = transform.position + dir * 2.5f;
            pos.y = player.position.y + 0.5f;
            BossProjectile p = Instantiate(fireballPrefab, pos, Quaternion.LookRotation(dir));
            p.Launch(dir, stats.fireballSpeed);
        }

        if (src != null && shootClip != null) src.PlayOneShot(shootClip);
    }

    // алдымен қызыл белгі шығады, сосын сол жерге тас түседі
    IEnumerator RockRain()
    {
        if (rockPrefab == null || player == null) yield break;

        Vector3[] points = new Vector3[rockCount];
        GameObject[] warnings = new GameObject[rockCount];

        for (int i = 0; i < rockCount; i++)
        {
            // ойыншының айналасынан кездейсоқ нүкте, еденді жоғарыдан сәулемен табамыз
            float r = stats.rockRadius;
            Vector3 p = player.position + new Vector3(Random.Range(-r, r), 5f, Random.Range(-r, r));
            if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 20f, groundMask)) continue;

            points[i] = hit.point;
            if (warningPrefab != null)
                warnings[i] = Instantiate(warningPrefab, hit.point + Vector3.up * 0.02f, Quaternion.identity);
        }

        yield return new WaitForSeconds(stats.warningTime);   // ойыншыға қашуға уақыт

        for (int i = 0; i < rockCount; i++)
        {
            if (warnings[i] == null) continue;          // бұл нүктеде еден табылмады
            Destroy(warnings[i]);
            Hazard rock = Instantiate(rockPrefab, points[i] + Vector3.up * 8f, Quaternion.identity);
            rock.lifeTimeOverride = stats.rockLifeTime;  // арена тастарға толып кетпесін
        }
    }

    // Bullet бастыққа тигенде шақырады
    public void TakeHit(int dmg)
    {
        if (dead) return;

        hp -= dmg;
        if (hp < 0) hp = 0;
        if (hpBar != null) hpBar.value = hp;
        AudioManager.PlayVaried(src, hitClip);
        Debug.Log("BOSS HP: " + hp);

        // екінші фаза: жартысынан төмен — жиірек атады, желпуіш кеңірек
        if (!phase2 && hp <= stats.maxHp * stats.phase2AtHpFraction)
        {
            phase2 = true;
            pause *= stats.phase2PauseMultiplier;
            fanCount += stats.phase2ExtraFan;
            fanAngle += stats.phase2ExtraFanAngle;
            rockCount += stats.phase2ExtraRocks;
            Debug.Log("BOSS: PHASE 2");
        }

        if (hp <= 0) Die();
    }

    void Die()
    {
        dead = true;
        StopAllCoroutines();
        AudioManager.PlayAt(deathClip, transform.position);

        // деңгей бітті: ұпай → save.json → Win (LevelRunner мен LevelList шешеді)
        if (LevelRunner.Current != null)
        {
            LevelRunner.Current.AddScore(stats.scoreValue);
            LevelRunner.Current.Complete();
        }
        else SceneManager.LoadScene("Win");
    }
}
