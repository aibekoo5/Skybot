using UnityEngine;

// Заттар мен қауіптердің мәндері + ұпай ережелері. Assets/Data/ItemValues.asset
[CreateAssetMenu(fileName = "ItemValues", menuName = "Sky Bot/Item Values")]
public class ItemValues : ScriptableObject
{
    [Header("Қауіптер")]
    [Min(0.1f)] public float hazardLifeTime = 15f;       // құлаған тас/жүрек қанша жатады (с)
    [Min(0)] public int rockDamage = 1;
    [Min(0)] public int spikeDamage = 1;
    [Min(0)] public int enemyContactDamage = 1;          // жауға соғылса

    [Header("Бомба")]
    [Min(0)] public int bombHitDamage = 2;               // тікелей тисе
    [Min(0f)] public float bombFuse = 2f;
    [Min(0f)] public float bombBlastRadius = 0f;         // 0 = жарылыс толқыны жоқ
    [Min(0)] public int bombBlastDamage = 2;

    [Header("Пайдалы заттар")]
    [Min(0)] public int heartHeal = 1;

    [Header("Ұпай")]
    [Min(0)] public int coinScore = 100;
    [Min(0)] public int levelClearScore = 500;
    [Min(0)] public int timeBonusPerSecond = 10;         // қалған әр секунд үшін
}
