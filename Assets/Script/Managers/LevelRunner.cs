using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// БАРЛЫҚ деңгейде бірдей код. Деңгейдің не екенін LevelData asset айтады:
//   * таймер (timeLimit), мақсат (goal), атауы
//   * жаулар: қай prefab, қай нүктеде, патруль жолы → Awake-те пайда болады
//   * құлайтын заттар толқындары → Spawner жасалады
// Деңгей біткенде ұпайды санап, SaveSystem-ге береді (келесі деңгей ашылады, рекорд сақталады).
//
// Кім шақырады:  Movement → OnCoinCollected(), CanExit(), Complete()  (монета, шығыс қақпасы)
//                Boss     → AddScore(), Complete()                    (бастық өлді)
//                EnemyBase.Died оқиғасы → ұпай + «барлық жау өлді» мақсаты
public class LevelRunner : MonoBehaviour
{
    [SerializeField] ItemValues items;                 // ұпай ережелері
    [SerializeField] float introTime = 2.5f;           // деңгей атауы экранда тұрады (UI)
    [SerializeField] float endDelay = 2f;              // «Level complete» хабарынан кейін (UI)
    [SerializeField] string winScene = "Win";

    public static LevelRunner Current { get; private set; }
    public LevelData Data { get; private set; }
    public int Score { get; private set; }

    Movement movement;
    float timeLeft;
    bool finished;
    int enemiesTotal, enemiesKilled;

    void Awake()
    {
        Current = this;
        Data = GameSession.ResolveForActiveScene();
        if (Data == null)
        {
            Debug.LogError("[Sky Bot] No LevelData for scene " + SceneManager.GetActiveScene().name + " — add it to Resources/LevelList");
            enabled = false;
            return;
        }

        movement = FindFirstObjectByType<Movement>();
        timeLeft = Data.timeLimit;
        SpawnEnemies();
        StartHazardWaves();
    }

    void Start()
    {
        if (Data == null) return;

        // деректен шыққан жаулар да, сахнада тұрғандары да
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            enemiesTotal++;
            e.Died += OnEnemyDied;
        }

        // таймер жоқ деңгейде timerText-ті басқалар қолданады (Outpost: EnemyGate жау санағышы)
        if (movement != null)
            movement.ShowMessage($"{Data.levelNumber}. {Data.displayName}\n<size=70%>{Data.goalText}</size>", introTime);
        Debug.Log($"[Sky Bot] Level {Data.levelNumber} '{Data.displayName}' from {Data.name}: " +
                  $"{Data.enemies.Count} enemies, {Data.hazardWaves.Count} hazard waves, time {Data.timeLimit}s");
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ---------- деңгейді деректен құру ----------

    void SpawnEnemies()
    {
        if (Data.enemies.Count == 0) return;
        Transform root = new GameObject("Enemies (" + Data.name + ")").transform;

        foreach (EnemySpawn s in Data.enemies)
        {
            if (s.prefab == null) continue;
            EnemyBase e = Instantiate(s.prefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), root);

            if (s.patrolPoints != null && s.patrolPoints.Length > 0)
            {
                var points = new Transform[s.patrolPoints.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new GameObject($"{e.name} point {i}").transform;
                    points[i].SetParent(root, false);
                    points[i].position = s.patrolPoints[i];
                }
                e.SetPatrolPoints(points);
            }
        }
    }

    void StartHazardWaves()
    {
        foreach (HazardWave w in Data.hazardWaves)
            new GameObject("Hazard Wave").AddComponent<Spawner>().Begin(w);
    }

    // ---------- таймер ----------

    void Update()
    {
        if (finished || Data.timeLimit <= 0f) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            finished = true;
            if (movement != null) movement.EndGame("Time's up!");
            Invoke(nameof(Restart), endDelay);      // уақыт бітті — деңгей қайта басталады
        }
        if (movement != null && movement.timerText != null)
            movement.timerText.text = "Time: " + Mathf.CeilToInt(timeLeft);
    }

    // ---------- ұпай мен мақсат ----------

    public void OnCoinCollected() => Score += items.coinScore;

    public void AddScore(int amount) => Score += amount;

    void OnEnemyDied(EnemyBase e)
    {
        enemiesKilled++;
        Score += e.ScoreValue;
    }

    // шығыс қақпасына жеткенде: мақсат орындалды ма?
    public bool CanExit(bool allCoinsCollected, out string reason)
    {
        reason = "";
        switch (Data.goal)
        {
            case LevelGoal.CollectCoins:
                if (!allCoinsCollected) reason = "Collect all coins first!";
                break;
            case LevelGoal.DefeatAllEnemies:
                if (enemiesKilled < enemiesTotal) reason = "Defeat all enemies first!";
                break;
            case LevelGoal.DefeatBoss:
                reason = "Defeat the boss!";
                break;
        }
        return reason == "";
    }

    public void Complete()
    {
        if (finished) return;
        finished = true;

        Score += items.levelClearScore;
        if (Data.timeLimit > 0f) Score += Mathf.CeilToInt(timeLeft) * items.timeBonusPerSecond;

        int oldBest = SaveSystem.BestScore(Data.levelNumber);
        bool newBest = SaveSystem.CompleteLevel(Data.levelNumber, Score);   // келесі деңгей ашылады + файлға жазылады

        if (movement != null)
        {
            string best = newBest ? "NEW BEST!" : "Best: " + oldBest;
            movement.EndGame($"Level complete!\n<size=70%>Score: {Score}   {best}</size>");
            movement.PlayWinSound();
        }
        Invoke(nameof(LoadNext), endDelay);
    }

    void LoadNext()
    {
        LevelData next = LevelList.Instance != null ? LevelList.Instance.Next(Data) : null;
        if (next != null) GameSession.Play(next);
        else SceneManager.LoadScene(winScene);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
