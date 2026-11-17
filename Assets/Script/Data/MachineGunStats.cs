using UnityEngine;

// Machine gun (штативтегі пулемет) балансы. Assets/Data/Enemies/MachineGunStats.asset
[CreateAssetMenu(fileName = "MachineGunStats", menuName = "Sky Bot/Enemy Stats/Machine Gun")]
public class MachineGunStats : EnemyStats
{
    [Header("Scan")]
    [Min(0f)] public float scanAngle = 90f;              // күзететін сектор
    [Min(0f)] public float scanSpeed = 0.6f;
    [Min(0f)] public float scanTurnSpeed = 60f;          // күзеткенде бас бұру (градус/с)
    [Min(0f)] public float trackTurnSpeed = 160f;        // SpinUp кезінде нысанаға бұрылу

    [Header("Fire")]
    [Min(0f)] public float spinUpTime = 0.7f;            // ескерту ұзақтығы
    [Min(1)] public int burstCount = 8;                  // бір сериядағы оқ
    [Min(0.01f)] public float burstInterval = 0.11f;
    [Min(0f)] public float burstTurnSpeed = 40f;         // ату кезінде баяу бұрылады — құтылуға болады
    [Min(0f)] public float spread = 2.5f;                // шашырау (градус)
    [Min(0f)] public float projectileSpeed = 15f;
    [Min(0f)] public float overheatTime = 2f;            // cooldown
}
