using UnityEngine;

// Үш жауға ортақ баланс: HP, көру, зақым, ұпай.
// Әр жау түрінің өз asset-і бар (RiflemanStats, MachineGunStats, BrawlerStats) — олар осыдан мұра алады.
public abstract class EnemyStats : ScriptableObject
{
    [Header("Денсаулық")]
    [Min(1)] public int maxHp = 3;

    [Header("Көру")]
    [Min(0f)] public float viewDistance = 14f;           // көру қашықтығы (м)
    [Range(10f, 360f)] public float viewAngle = 120f;    // көру бұрышы (градус)

    [Header("Шабуыл")]
    [Min(0)] public int damage = 1;                      // ойыншыдан алатын жүрек саны

    [Header("Ұпай")]
    [Min(0)] public int scoreValue = 150;                // өлтіргенде берілетін ұпай
}
