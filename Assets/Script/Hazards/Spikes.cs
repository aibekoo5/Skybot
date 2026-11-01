using UnityEngine;

// тұрақты қауіп (тікенек / лава) — Hazard-тан мұра, бірақ жойылмайды
public class Spikes : Hazard
{
    protected override void Start() { }    // lifeTime жоқ — орнында қалады

    protected override void OnCollisionEnter(Collision c)
    {
        if (!c.gameObject.CompareTag("Player")) return;

        Health h = c.gameObject.GetComponentInParent<Health>();
        if (h != null) OnHitPlayer(h);      // жыпылықтау кезінде Health өзі елемейді
    }

    // үстінде тұра берсе де зақым алады
    void OnCollisionStay(Collision c)
    {
        OnCollisionEnter(c);
    }
}
