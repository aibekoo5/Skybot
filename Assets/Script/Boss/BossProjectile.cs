using UnityEngine;

// бастықтың оғы — Hazard-тан мұра алады (тисе 1 зақым)
public class BossProjectile : Hazard
{
    public float speed = 8f;

    public void Launch(Vector3 direction)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = direction.normalized * speed;
    }

    protected override void OnCollisionEnter(Collision c)
    {
        base.OnCollisionEnter(c);   // ойыншыға тисе — зақым
        Destroy(gameObject);        // қабырға не жерге тисе де жоғалады
    }
}
