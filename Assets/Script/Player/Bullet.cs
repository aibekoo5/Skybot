using UnityEngine;


public class Bullet : MonoBehaviour
{
    public float speed = 12f;
    public float lifeTime = 3f;      

    void Start()
    {
     
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector3 direction)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = direction.normalized * speed;

    }

    // оқтың collider-і Is Trigger — сондықтан OnTriggerEnter
    void OnTriggerEnter(Collider other)
    {
        HitBoss(other.gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        HitBoss(c.gameObject);
    }

    // бастыққа тисе — оның HP-ы азаяды, оқ жоғалады
    void HitBoss(GameObject target)
    {
        Boss boss = target.GetComponentInParent<Boss>();
        if (boss != null)
        {
            boss.TakeHit(1);
            Destroy(gameObject);
        }
    }
}
