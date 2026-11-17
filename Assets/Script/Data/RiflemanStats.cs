using UnityEngine;

// Rifleman (мылтықты сарбаз) балансы. Assets/Data/Enemies/RiflemanStats.asset
[CreateAssetMenu(fileName = "RiflemanStats", menuName = "Sky Bot/Enemy Stats/Rifleman")]
public class RiflemanStats : EnemyStats
{
    [Header("Patrol")]
    [Min(0f)] public float patrolSpeed = 1.6f;
    [Min(0f)] public float waypointWait = 1.5f;          // нүктеде тұру (с)

    [Header("Chase / Search")]
    [Min(0f)] public float chaseSpeed = 2.8f;
    [Min(0f)] public float searchTime = 4f;
    [Min(0f)] public float searchTurnSpeed = 90f;        // градус/с

    [Header("Aim / Fire")]
    [Min(0f)] public float fireRange = 11f;              // осыдан жақын болса тоқтап көздейді
    [Min(0f)] public float aimTime = 0.6f;               // ескерту (лазер) ұзақтығы
    [Min(0f)] public float aimTurnSpeed = 200f;
    [Min(0f)] public float projectileSpeed = 10f;
    [Min(0f)] public float cooldown = 1.4f;              // атқаннан кейінгі үзіліс
}
