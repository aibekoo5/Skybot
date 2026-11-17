using UnityEngine;

// Бастық балансы. Assets/Data/BossStats.asset
// Маңызды: Boss бұл asset-ті ӨЗГЕРТПЕЙДІ (екінші фазада өз айнымалыларын өзгертеді),
// әйтпесе Play Mode-та asset-тің мәні мәңгі өзгеріп кетер еді.
[CreateAssetMenu(fileName = "BossStats", menuName = "Sky Bot/Boss Stats")]
public class BossStats : ScriptableObject
{
    [Header("HP")]
    [Min(1)] public int maxHp = 10;
    [Min(0)] public int scoreValue = 1000;

    [Header("Шабуыл циклі")]
    [Min(0f)] public float startDelay = 2f;              // бірінші шабуылға дейін
    [Min(0f)] public float pause = 2f;                   // шабуылдар арасындағы үзіліс

    [Header("Оқ желпуіші")]
    [Min(1)] public int fanCount = 3;
    [Min(0f)] public float fanAngle = 40f;
    [Min(0f)] public float fireballSpeed = 8f;

    [Header("Тас жаңбыры")]
    [Min(0)] public int rockCount = 3;
    [Min(0f)] public float rockRadius = 3f;              // ойыншының айналасы (м)
    [Min(0f)] public float warningTime = 1f;             // қызыл белгіден кейін тас түскенше
    [Min(0.1f)] public float rockLifeTime = 3f;

    [Header("Екінші фаза")]
    [Range(0f, 1f)] public float phase2AtHpFraction = 0.5f;
    [Range(0.1f, 1f)] public float phase2PauseMultiplier = 0.5f;
    [Min(0)] public int phase2ExtraFan = 2;
    [Min(0f)] public float phase2ExtraFanAngle = 30f;
    [Min(0)] public int phase2ExtraRocks = 2;
}
