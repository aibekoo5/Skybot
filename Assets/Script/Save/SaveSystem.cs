using System;
using System.IO;
using UnityEngine;

public enum SaveLoadResult { NewGame, Loaded, BrokenFileReset }

// Прогресс пен баптауларды JSON файлға сақтайды: Application.persistentDataPath/save.json
// (Android: /storage/emulated/0/Android/data/<package>/files/save.json).
// BinaryFormatter ЖОҚ — тек JsonUtility, файлды қолмен ашып оқуға болады.
//
// Қауіпсіз жүктеу: файл жоқ → әдепкі мәндер; файл бұзық → әдепкі мәндер + бұзық файлдың
// көшірмесі save_broken.json, ойын ЕШҚАШАН құламайды.
// Қауіпсіз жазу: алдымен save.json.tmp-ке жазамыз, сосын ауыстырамыз — жазу кезінде
// қолданба жабылса да ескі файл бүлінбейді.
//
// Қашан сақталады: деңгей біткенде (CompleteLevel), баптау өзгергенде (SaveSoon),
// қолданба фонға кеткенде/жабылғанда (SaveSystemHook → OnApplicationPause / OnApplicationQuit),
// прогресс тазаланғанда (ResetProgress).
public static class SaveSystem
{
    const string FileName = "save.json";
    const float SaveDelay = 0.5f;          // слайдерді сүйрегенде әр кадр жазбау үшін

    static SaveData data;
    static float saveDueTime = -1f;

    public static SaveLoadResult LastLoad { get; private set; }
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    static string TempPath => FilePath + ".tmp";
    static string BrokenPath => Path.Combine(Application.persistentDataPath, "save_broken.json");

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static SettingsData Settings => Data.settings;

    // ---------- жүктеу ----------

    public static void Load()
    {
        // негізгі файл жоқ, бірақ .tmp бар — алдыңғы жазу ортасында үзілген
        string path = File.Exists(FilePath) ? FilePath : File.Exists(TempPath) ? TempPath : null;

        if (path == null)
        {
            data = new SaveData();
            LastLoad = SaveLoadResult.NewGame;
            Debug.Log("[Save] no save file, new game: " + FilePath);
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);   // бұзық JSON → exception
            if (loaded == null) throw new InvalidDataException("save file is empty");
            loaded.Repair();
            data = loaded;
            LastLoad = SaveLoadResult.Loaded;
            Debug.Log("[Save] loaded " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Save] save file is broken, using defaults. " + e.Message);
            try { File.Copy(path, BrokenPath, true); } catch (Exception) { /* көшірме міндетті емес */ }
            data = new SaveData();
            LastLoad = SaveLoadResult.BrokenFileReset;
        }
    }

    // ---------- жазу ----------

    public static bool Save()
    {
        saveDueTime = -1f;
        if (data == null) return false;    // ештеңе жүктелмеген — жазатын да ештеңе жоқ
        try
        {
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(TempPath, FilePath);
            Debug.Log("[Save] saved " + FilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] could not write the save file: " + e.Message);
            return false;
        }
    }

    // баптау өзгерді: жарты секундтан кейін бір рет жазамыз
    public static void SaveSoon()
    {
        saveDueTime = Time.unscaledTime + SaveDelay;
    }

    // SaveSystemHook әр кадр шақырады
    internal static void Tick()
    {
        if (saveDueTime >= 0f && Time.unscaledTime >= saveDueTime) Save();
    }

    // ---------- прогресс ----------

    public static bool IsUnlocked(int level) => Data.unlockedLevels.Contains(level);

    public static int BestScore(int level)
    {
        LevelRecord r = Data.records.Find(x => x.level == level);
        return r != null ? r.bestScore : 0;
    }

    public static int TotalBestScore
    {
        get
        {
            int sum = 0;
            foreach (LevelRecord r in Data.records) sum += r.bestScore;
            return sum;
        }
    }

    // деңгей өтілді: келесісі ашылады, рекорд жаңарады, файл бірден жазылады.
    // Жаңа рекорд болса true қайтарады.
    public static bool CompleteLevel(int level, int score)
    {
        if (!Data.unlockedLevels.Contains(level + 1)) Data.unlockedLevels.Add(level + 1);
        Data.unlockedLevels.Sort();

        LevelRecord r = Data.records.Find(x => x.level == level);
        if (r == null) { r = new LevelRecord { level = level }; Data.records.Add(r); }
        bool newBest = score > r.bestScore;
        if (newBest) r.bestScore = score;

        Save();
        return newBest;
    }

    // прогресс нөлге түседі, баптаулар (дыбыс, аты) сақталады
    public static void ResetProgress()
    {
        SettingsData keep = Data.settings;
        data = new SaveData { settings = keep };
        Save();
    }

    // ---------- баптауларды қолдану ----------

    // музыка: сахнадағы loop дыбыстары (фон музыкасы) — дыбыс деңгейі мен mute
    public static void ApplyAudio()
    {
        SettingsData s = Settings;
        foreach (AudioSource a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!a.loop) continue;
            a.volume = s.musicVolume;
            a.mute = !s.musicOn;
        }
    }

    public static void ApplyScreen()
    {
        if (!Application.isMobilePlatform) Screen.fullScreen = Settings.fullscreen;
    }
}
