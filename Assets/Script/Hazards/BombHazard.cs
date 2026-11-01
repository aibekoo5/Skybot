using UnityEngine;


public class BombHazard : Hazard
{
    public float fuse = 2f;            
    public GameObject explosionPrefab;     
    public AudioClip explodeClip;         
    public float blastRadius = 0f;        
    public int blastDamage = 2;

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
            Invoke(nameof(Explode), fuse);
        }
    }

    protected override void OnHitPlayer(Health player)
    {
        player.TakeDamage(2);            
    }

    void Explode()
    {
        if (blastRadius > 0f)              
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
            foreach (Collider col in hits)
            {
                if (!col.CompareTag("Player")) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h != null) { h.TakeDamage(blastDamage); break; }
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
        if (explodeClip != null)
            AudioSource.PlayClipAtPoint(explodeClip, transform.position);
    }
}
