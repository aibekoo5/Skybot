using UnityEngine;

// Атқыштың оғы: түзу ұшады, ойыншыға тисе зақым, қабырғаға тисе жоғалады.
// Физика жоқ — әр кадр алға SphereCast жасаймыз (жылдам оқ қабырғадан өтіп кетпейді).
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float radius = 0.08f;
    [SerializeField] float lifeTime = 4f;

    Vector3 velocity;
    int damage;
    Transform owner;

    public void Launch(Vector3 vel, int dmg, Transform shooter)
    {
        velocity = vel;
        damage = dmg;
        owner = shooter;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        float step = velocity.magnitude * Time.deltaTime;
        if (step <= 0f) return;
        Vector3 dir = velocity.normalized;

        foreach (RaycastHit h in Physics.SphereCastAll(transform.position, radius, dir, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && h.transform.IsChildOf(owner)) continue;
            Health hp = h.collider.GetComponentInParent<Health>();
            if (hp != null) hp.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        transform.position += dir * step;
    }
}
