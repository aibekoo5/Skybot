using UnityEngine;

// бастықтың оғы — Hazard-тан мұра алады (зақым — ItemValues.rockDamage, жылдамдық — BossStats)
public class BossProjectile : Hazard
{
    public void Launch(Vector3 direction, float speed)
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
