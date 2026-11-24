using UnityEngine;

// Бомба: ойыншыға тисе — бірден зақым; жерге түссе — фитиль, сосын жарылыс.
// Зақым, фитиль, жарылыс радиусы — ItemValues asset-те.
public class BombHazard : Hazard
{
    public GameObject explosionPrefab;
    public AudioClip explodeClip;

    bool armed;

    protected override void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag("Player"))
        {
            base.OnCollisionEnter(c);
            PlayFx();
            return;
        }

        if (!armed)
        {
            armed = true;
            Invoke(nameof(Explode), items.bombFuse);
        }
    }

    protected override void OnHitPlayer(Health player)
    {
        player.TakeDamage(items.bombHitDamage);
    }

    void Explode()
    {
        if (items.bombBlastRadius > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, items.bombBlastRadius);
            foreach (Collider col in hits)
            {
                if (!col.CompareTag("Player")) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h != null) { h.TakeDamage(items.bombBlastDamage); break; }
            }
        }
        PlayFx();
        Destroy(gameObject);
    }

    void PlayFx()
    {
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }
        AudioManager.PlayAt(explodeClip, transform.position);   // SfxBus арқылы, биіктігі сәл кездейсоқ
    }
}
