using UnityEngine;

// Құлайтын/тұрақты заттардың негізі. Зақым мен өмір уақыты — ItemValues asset-те.
public class Hazard : MonoBehaviour
{
    [SerializeField] protected ItemValues items;
    [HideInInspector] public float lifeTimeOverride = -1f;   // бастықтың тастары қысқа өмір сүреді

    protected virtual void Start()
    {
        Destroy(gameObject, lifeTimeOverride > 0f ? lifeTimeOverride : items.hazardLifeTime);
    }

    protected virtual void OnCollisionEnter(Collision c)
    {
        if (!c.gameObject.CompareTag("Player")) return;

        Health h = c.gameObject.GetComponent<Health>();
        if (h == null) h = c.gameObject.GetComponentInParent<Health>();
        if (h != null) OnHitPlayer(h);

        Destroy(gameObject);
    }

    protected virtual void OnHitPlayer(Health player)
    {
        player.TakeDamage(items.rockDamage);
    }
}
