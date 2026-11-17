using UnityEngine;

// Ойыншының БАРЛЫҚ баланс сандары осы asset-те (Assets/Data/PlayerStats.asset).
// Movement, Health, Shooter, Bullet сандарды кодтан емес, осыдан оқиды.
// Мұндағы мәндер — тек жаңа asset жасағандағы әдепкі (default) мәндер.
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Sky Bot/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Қозғалыс")]
    [Min(0f)] public float moveSpeed = 6f;               // жүгіру жылдамдығы (м/с)
    [Min(0f)] public float turnSpeed = 6f;               // бұрылу жылдамдығы
    [Min(0f)] public float jumpImpulse = 5f;             // секіру (~1.27 м)
    [Range(0f, 1f)] public float airControl = 0.4f;      // ауадағы басқару
    [Min(0f)] public float acceleration = 30f;           // жердегі үдеу (м/с²)
    [Range(0f, 1f)] public float shootMoveMultiplier = 0.2f; // атып жатқандағы жылдамдық үлесі
    [Min(0f)] public float jumpCooldown = 0.25f;         // екі секірудің арасы (с)
    [Min(0f)] public float aimHoldTime = 0.6f;           // атқаннан кейін экранға қарап тұру (с)

    [Header("Денсаулық")]
    [Min(1)] public int maxHp = 3;                       // жүрек саны
    [Min(0f)] public float invincibleTime = 2f;          // зақымнан кейінгі қорғаныс (с)
    [Min(0.02f)] public float blinkInterval = 0.2f;      // жыпылықтау қадамы (с)

    [Header("Ату")]
    [Min(0f)] public float fireCooldown = 0.3f;          // екі оқтың арасы (с)
    [Min(0f)] public float bulletSpeed = 12f;            // оқ жылдамдығы (м/с)
    [Min(1)] public int bulletDamage = 1;                // бір оқтың зақымы
    [Min(0.1f)] public float bulletLifeTime = 3f;        // оқ қанша секунд ұшады
}
