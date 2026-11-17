using UnityEngine;


// Ойыншының оғы. Жылдамдық, зақым, өмір уақыты — Shooter береді (PlayerStats asset-тен).
public class Bullet : MonoBehaviour
{
    int damage;

    public void Launch(Vector3 direction, float speed, int dmg, float lifeTime)
    {
        damage = dmg;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }

    // оқтың collider-і Is Trigger — сондықтан OnTriggerEnter
    void OnTriggerEnter(Collider other)
    {
        if (HitBoss(other.gameObject)) return;

        // жау (Patroller / Shooter / Charger)
        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeHit(damage);
            Destroy(gameObject);
            return;
        }

        // қабырға/жер: оқ өтіп кетпейді (ойыншының өзі мен триггерлер — монета, шығыс — есепке алынбайды)
        if (!other.isTrigger && !other.CompareTag("Player") && other.GetComponentInParent<Movement>() == null)
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        HitBoss(c.gameObject);
    }

    // бастыққа тисе — оның HP-ы азаяды, оқ жоғалады
    bool HitBoss(GameObject target)
    {
        Boss boss = target.GetComponentInParent<Boss>();
        if (boss == null) return false;
        boss.TakeHit(damage);
        Destroy(gameObject);
        return true;
    }
}
