using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Level 3 бастығы: HP, екі шабуыл (оқ желпуіші + тас жаңбыры), екінші фаза
public class Boss : MonoBehaviour
{
    [Header("HP")]
    public int maxHp = 10;
    public Slider hpBar;                    // экрандағы HP жолағы

    [Header("Шабуыл")]
    public Transform player;
    public BossProjectile fireballPrefab;   // желпуіш оғы
    public Hazard rockPrefab;               // жоғарыдан түсетін тас
    public GameObject warningPrefab;        // қызыл белгі "!" (collider-сіз)
    public float pause = 2f;                // шабуыл арасындағы үзіліс — ойыншы ату үшін
    public int fanCount = 3;                // желпуіштегі оқ саны
    public float fanAngle = 40f;            // желпуіштің ені (градус)
    public int rockCount = 3;               // бір жаңбырдағы тас саны
    public float warningTime = 1f;          // белгіден кейін тас түскенше уақыт
    public LayerMask groundMask;            // тас түсетін еден (Ground layer)

    [Header("Дыбыс")]
    public AudioClip shootClip;
    public AudioClip hitClip;
    public AudioClip deathClip;

    int hp;
    bool phase2;
    bool dead;
    AudioSource src;

    void Start()
    {
        hp = maxHp;
        src = GetComponent<AudioSource>();

        if (hpBar != null)
        {
            hpBar.maxValue = maxHp;
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
        yield return new WaitForSeconds(2f);

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
            p.Launch(dir);
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
            Vector3 p = player.position + new Vector3(Random.Range(-3f, 3f), 5f, Random.Range(-3f, 3f));
            if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 20f, groundMask)) continue;

            points[i] = hit.point;
            if (warningPrefab != null)
                warnings[i] = Instantiate(warningPrefab, hit.point + Vector3.up * 0.02f, Quaternion.identity);
        }

        yield return new WaitForSeconds(warningTime);   // ойыншыға қашуға уақыт

        for (int i = 0; i < rockCount; i++)
        {
            if (warnings[i] == null) continue;          // бұл нүктеде еден табылмады
            Destroy(warnings[i]);
            Hazard rock = Instantiate(rockPrefab, points[i] + Vector3.up * 8f, Quaternion.identity);
            rock.lifeTime = 3f;                         // арена тастарға толып кетпесін
        }
    }

    // Bullet бастыққа тигенде шақырады
    public void TakeHit(int dmg)
    {
        if (dead) return;

        hp -= dmg;
        if (hp < 0) hp = 0;
        if (hpBar != null) hpBar.value = hp;
        if (src != null && hitClip != null) src.PlayOneShot(hitClip);
        Debug.Log("BOSS HP: " + hp);

        // екінші фаза: жартысынан төмен — жиірек атады, желпуіш кеңірек
        if (!phase2 && hp <= maxHp / 2)
        {
            phase2 = true;
            pause *= 0.5f;
            fanCount += 2;
            fanAngle += 30f;
            rockCount += 2;
            Debug.Log("BOSS: PHASE 2");
        }

        if (hp <= 0) Die();
    }

    void Die()
    {
        dead = true;
        StopAllCoroutines();
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, transform.position);
        Invoke(nameof(LoadWin), 2f);
    }

    void LoadWin()
    {
        SceneManager.LoadScene("Win");
    }
}
