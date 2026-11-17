using System.Collections.Generic;
using UnityEngine;

// save.json-ның ішіндегі барлық нәрсе. JsonUtility тек [Serializable] класс пен
// List/массивті түсінеді (Dictionary-ді ЕЛЕМЕЙДІ) — сондықтан рекордтар List<LevelRecord>.
//
// Мысал:
// { "version": 1, "unlockedLevels": [1, 2], "records": [ { "level": 1, "bestScore": 1250 } ],
//   "settings": { "musicVolume": 0.8, "musicOn": true, "fullscreen": true, "playerName": "Aibek", "playerColor": 0 } }
[System.Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<int> unlockedLevels = new List<int> { 1 };      // ашық деңгейлердің нөмірлері
    public List<LevelRecord> records = new List<LevelRecord>();  // әр деңгейдің ең жақсы ұпайы
    public SettingsData settings = new SettingsData();

    // файл қолмен өзгертілген/ескі болса да — ойын құламауы үшін мәндерді жөндейміз
    public void Repair()
    {
        if (unlockedLevels == null) unlockedLevels = new List<int>();
        unlockedLevels.RemoveAll(n => n < 1);
        if (!unlockedLevels.Contains(1)) unlockedLevels.Add(1);   // бірінші деңгей әрқашан ашық
        unlockedLevels.Sort();

        if (records == null) records = new List<LevelRecord>();
        records.RemoveAll(r => r == null || r.level < 1);
        foreach (LevelRecord r in records) r.bestScore = Mathf.Max(0, r.bestScore);

        if (settings == null) settings = new SettingsData();
        settings.Repair();

        version = CurrentVersion;
    }
}

[System.Serializable]
public class LevelRecord
{
    public int level;
    public int bestScore;
}

[System.Serializable]
public class SettingsData
{
    public float musicVolume = 0.8f;
    public bool musicOn = true;
    public bool fullscreen = true;
    public string playerName = "";
    public int playerColor = 0;       // Settings-тегі dropdown индексі

    public void Repair()
    {
        musicVolume = float.IsNaN(musicVolume) ? 0.8f : Mathf.Clamp01(musicVolume);
        if (playerName == null) playerName = "";
        if (playerColor < 0) playerColor = 0;
    }
}
