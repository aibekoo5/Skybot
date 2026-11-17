using System.Collections.Generic;
using UnityEngine;

public enum LevelGoal { CollectCoins, DefeatAllEnemies, DefeatBoss }

// Бір жау: қай prefab, қай жерде, қайда қарап тұр, патруль нүктелері
[System.Serializable]
public class EnemySpawn
{
    public EnemyBase prefab;
    public Vector3 position;
    public float yaw;
    public Vector3[] patrolPoints;
}

// Аспаннан құлайтын заттар толқыны (тас, жүрек)
[System.Serializable]
public class HazardWave
{
    public Hazard[] hazards;
    public Vector3 center;                               // аймақтың ортасы
    public Vector2 halfSize = new Vector2(8f, 8f);       // x және z бойынша ±
    public float spawnHeight = 6f;                       // қандай биіктіктен құлайды
    [Min(0f)] public float firstDelay = 1f;
    [Min(0.05f)] public float interval = 2f;
}

// БІР ДЕҢГЕЙ = БІР asset. LevelRunner (барлық деңгейде бірдей код) осыны оқып деңгейді құрады:
// таймер, мақсат, жаулар, құлайтын заттар. Деңгейдің рельефі (платформалар) сахнада қалады.
[CreateAssetMenu(fileName = "Level_00", menuName = "Sky Bot/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Деңгей")]
    [Min(1)] public int levelNumber = 1;                 // сақтауда осы нөмір қолданылады
    public string displayName = "Level";
    public string sceneName;                             // рельефі бар сахна
    public LevelGoal goal = LevelGoal.CollectCoins;
    [TextArea] public string goalText = "Collect every coin and reach the gate";
    [Min(0f)] public float timeLimit = 90f;              // 0 = таймер жоқ

    [Header("Жаулар (Start-та пайда болады)")]
    public List<EnemySpawn> enemies = new List<EnemySpawn>();

    [Header("Құлайтын заттар")]
    public List<HazardWave> hazardWaves = new List<HazardWave>();
}
