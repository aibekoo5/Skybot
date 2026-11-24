using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    [SerializeField] PlayerStats stats;        // maxHp, қорғаныс уақыты — asset-те
    public int hp;
    public int MaxHp => stats.maxHp;

    public AudioClip hurtClip;
    public AudioClip healClip;

    bool invincible;
    Renderer[] renderers;
    AudioSource src;
    HUD hud;                                   

    void Start()
    {
        hp = stats.maxHp;

        renderers = GetComponentsInChildren<Renderer>();
        src = GetComponent<AudioSource>();

        hud = FindAnyObjectByType<HUD>();     
        if (hud != null) hud.SetHearts(hp);     

        Debug.Log("HP: " + hp);
    }

    void Update()
    {
        if (transform.position.y < -10f)
            GameOver();
    }

    public void TakeDamage(int dmg)
    {
        if (invincible) return;

        hp -= dmg;
        if (hp < 0) hp = 0;
        Debug.Log("OUCH! HP: " + hp);
        AudioManager.PlayVaried(src, hurtClip);

        if (hud != null) hud.SetHearts(hp);     

        if (hp <= 0)
        {
            GameOver();
            return;
        }
        StartCoroutine(Invincibility());
    }

    public void Heal(int amount)
    {
        hp = Mathf.Min(hp + amount, stats.maxHp);
        Debug.Log("HEAL! HP: " + hp);
        AudioManager.PlayVaried(src, healClip);

        if (hud != null) hud.SetHearts(hp);    
    }

    IEnumerator Invincibility()
    {
        invincible = true;
        int blinks = Mathf.CeilToInt(stats.invincibleTime / stats.blinkInterval);
        for (int i = 0; i < blinks; i++)
        {
            SetVisible(i % 2 == 1);
            yield return new WaitForSeconds(stats.blinkInterval);
        }
        SetVisible(true);
        invincible = false;
    }

    void SetVisible(bool v)
    {
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = v;
    }

    bool gameOverStarted;

    // жеңіліс: lose дыбысы (2D, AudioManager — сахна қайта жүктелсе де соңына дейін естіледі),
    // экранда хабар, сосын деңгей қайта басталады
    void GameOver()
    {
        if (gameOverStarted) return;
        gameOverStarted = true;
        invincible = true;
        Debug.Log("GAME OVER");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLose();
        Movement mv = GetComponent<Movement>();
        if (mv != null) mv.EndGame("You lose!");
        StartCoroutine(ReloadAfter(1.6f));
    }

    IEnumerator ReloadAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}