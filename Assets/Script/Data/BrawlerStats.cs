using UnityEngine;

// Brawler (боксшы) балансы. Assets/Data/Enemies/BrawlerStats.asset
[CreateAssetMenu(fileName = "BrawlerStats", menuName = "Sky Bot/Enemy Stats/Brawler")]
public class BrawlerStats : EnemyStats
{
    [Header("Chase")]
    [Min(0f)] public float runSpeed = 6.5f;
    [Min(0f)] public float walkSpeed = 1.5f;
    [Min(0f)] public float waypointWait = 1.5f;
    [Min(0f)] public float forgetTime = 3f;              // көрмей қалғаннан кейін қанша қуады
    [Min(0f)] public float shoutCooldown = 5f;

    [Header("Punch")]
    [Min(0f)] public float attackRange = 1.7f;           // WindUp басталатын қашықтық
    [Min(0f)] public float windUp = 0.6f;                // ескерту ұзақтығы
    [Min(0f)] public float hitRange = 2f;                // соққы жетеді
    [Range(0f, 360f)] public float hitAngle = 80f;
    [Min(0f)] public float hitHeight = 1.5f;             // биіктік айырмасы осыдан аз болса тиеді
    [Min(0f)] public float knockback = 6f;
    [Min(0f)] public float knockUp = 2.5f;
    [Min(0f)] public float punchTime = 0.35f;
    [Min(0f)] public float recoverTime = 1.2f;           // cooldown
    [Min(0f)] public float recoverTurnSpeed = 120f;      // соққыдан кейін ойыншыға бұрылу (градус/с)
}
