using UnityEngine;

public class Shooter : MonoBehaviour
{
    public Bullet bulletPrefab;
    [SerializeField] PlayerStats stats;  // ату жиілігі, оқ жылдамдығы мен зақымы
    public float muzzleDistance = 1f;    
    public float muzzleHeight = 0.5f;   
    public Transform muzzle;            // мылтықтың ұшы — бар болса, оқ сол жерден шығады
    public AudioClip shootClip;         


    public bool useMovementDirection = true;

    float nextShot;                      
    AudioSource src;

    void Start()
    {
        src = GetComponent<AudioSource>();
    }

    
    public void Shoot(Vector3 moveDir)
    {
        if (Time.time < nextShot) return;   
        nextShot = Time.time + stats.fireCooldown;

        Vector3 aimDir = useMovementDirection ? moveDir : transform.forward;

        Vector3 pos = muzzle != null
            ? muzzle.position                                   // мылтықтың ұшынан
            : transform.position + aimDir * muzzleDistance + Vector3.up * muzzleHeight;
        Bullet b = Instantiate(bulletPrefab, pos, Quaternion.LookRotation(aimDir));
        b.Launch(aimDir, stats.bulletSpeed, stats.bulletDamage, stats.bulletLifeTime);

        AudioManager.PlayVaried(src, shootClip, null, 0.8f);   // pitch 0.92–1.08: қатарынан атқанда роботтай естілмейді
    }
}