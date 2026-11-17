using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools → Sky Bot → Smoke Test Data And Saves
// Play Mode-та автоматты тексеру (batch: -executeMethod DataSavesSmokeTest.Run, -nographics-сыз):
//   1) бұзық save.json → ойын құламайды, әдепкі мәндер, мәзірде ескерту
//   2) Level Select: 1-деңгей ашық, қалғаны жабық
//   3) әр деңгей LevelData-дан құрылады (жаулар, толқындар), жау жылдамдығы asset-тен
//   4) деңгей өтілді → save.json-да келесі деңгей ашық + рекорд; файлдан қайта жүктеу
//   5) баптау өзгерді → SaveSoon → файлда бар
// Нәтиже мен скриншоттар: Logs/SmokeTest. Сенің шын save.json-ың тест соңында қайтарылады.
public static class DataSavesSmokeTest
{
    static int step;
    static double stepTime;
    static readonly List<string> errors = new List<string>();
    static readonly List<string> report = new List<string>();
    static string outDir, backup;
    static bool prevOptsEnabled;
    static EnterPlayModeOptions prevOpts;

    [MenuItem("Tools/Sky Bot/Smoke Test Data And Saves")]
    public static void Run()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/SmokeTest"));
        Directory.CreateDirectory(outDir);
        errors.Clear(); report.Clear();
        step = 0;

        // шын сақтауды қорғаймыз, сосын әдейі бұзамыз
        string save = SaveSystem.FilePath;
        backup = save + ".smoketest-backup";
        if (File.Exists(save)) File.Copy(save, backup, true);
        File.WriteAllText(save, "{ \"version\": 1, \"unlockedLevels\": [1, 2, BROKEN");

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        prevOptsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        prevOpts = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/Scenes/Main menu.unity");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"[step {step}] {msg}\n{stack}");
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) errors.Add("CHECK FAILED: " + what);
    }

    static LevelData L(int i) => LevelList.Instance.levels[i];

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { stepTime = EditorApplication.timeSinceStartup; return; }
        double now = EditorApplication.timeSinceStartup;
        if (now - stepTime < 3.0) return;
        stepTime = now;

        switch (step)
        {
            case 0: // мәзір, бұзық файл
                Check(SaveSystem.LastLoad == SaveLoadResult.BrokenFileReset, "broken save.json -> BrokenFileReset, no crash");
                Check(SaveSystem.Data.unlockedLevels.Count == 1 && SaveSystem.IsUnlocked(1), "defaults after broken file: only level 1 unlocked");
                Check(File.Exists(Path.Combine(Application.persistentDataPath, "save_broken.json")), "broken copy kept as save_broken.json");
                Capture("01_menu_broken_save");
                Object.FindFirstObjectByType<MenuButtons>().Play();
                break;
            case 1:
                var sel = Object.FindFirstObjectByType<LevelSelectMenu>();
                Check(sel != null && sel.gameObject.activeInHierarchy, "PLAY opens level select");
                var cards = GameObject.Find("CardRow");
                int on = 0, off = 0;
                foreach (Transform c in cards.transform)
                    if (c.gameObject.activeSelf) { if (c.GetComponent<UnityEngine.UI.Button>().interactable) on++; else off++; }
                Check(on == 1 && off == 3, $"level select: 1 open, 3 locked (got {on}/{off})");
                Capture("02_level_select_new");
                GameSession.Play(L(0));
                break;
            case 2: // Outpost — жаулар деректен
                Check(LevelRunner.Current != null && LevelRunner.Current.Data == L(0), "Outpost: LevelRunner uses Level_01");
                var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                Check(enemies.Length == L(0).enemies.Count && enemies.Length == 6, $"Outpost: {enemies.Length} enemies spawned from data");
                var rifle = Object.FindFirstObjectByType<Rifleman>();
                var stats = AssetDatabase.LoadAssetAtPath<RiflemanStats>("Assets/Data/Enemies/RiflemanStats.asset");
                var agent = rifle != null ? rifle.GetComponent<NavMeshAgent>() : null;
                Check(agent != null && Mathf.Approximately(agent.speed, stats.patrolSpeed), "Rifleman speed comes from RiflemanStats.asset");
                Capture("03_outpost");
                LevelRunner.Current.AddScore(300);
                LevelRunner.Current.Complete();
                break;
            case 3: // Complete → келесі деңгей + файл
                Check(SceneManager.GetActiveScene().name == L(1).sceneName, "after Complete: next level from LevelList loaded");
                string json = File.ReadAllText(SaveSystem.FilePath);
                File.WriteAllText(Path.Combine(outDir, "save_after_level1.json"), json);
                Check(json.Contains("\"level\": 1") && SaveSystem.IsUnlocked(2), "save.json: level 2 unlocked + level 1 record");
                Check(GameObject.Find("Hazard Wave") != null, "Sky Islands: hazard wave created from data");
                Capture("04_sky_islands");
                // «қолданбаны жауып ашу»: жадтағы деректі тастап, файлдан қайта оқимыз
                int best = SaveSystem.BestScore(1);
                SaveSystem.Load();
                Check(SaveSystem.LastLoad == SaveLoadResult.Loaded && SaveSystem.BestScore(1) == best && best > 0,
                      $"reload from disk: best score {SaveSystem.BestScore(1)} and unlocks survive");
                GameSession.Play(L(2));
                break;
            case 4:
                Check(LevelRunner.Current.Data == L(2) && Object.FindFirstObjectByType<Patrol>() != null, "Spike Bridge: Level_03 + patrollers");
                Capture("05_spike_bridge");
                GameSession.Play(L(3));
                break;
            case 5:
                Check(LevelRunner.Current.Data == L(3) && Object.FindFirstObjectByType<Boss>() != null, "Boss Arena: Level_04 + boss");
                Capture("06_boss");
                // баптау
                SaveSystem.Settings.musicVolume = 0.3f;
                SaveSystem.SaveSoon();
                break;
            case 6:
                Check(File.ReadAllText(SaveSystem.FilePath).Contains("\"musicVolume\": 0.3"), "settings change saved (SaveSoon)");
                SceneManager.LoadScene("Main menu");
                break;
            case 7:
                Object.FindFirstObjectByType<MenuButtons>().Play();
                break;
            case 8:
                Capture("07_level_select_progress");
                var s = Object.FindFirstObjectByType<LevelSelectMenu>();
                s.AskReset();
                break;
            case 9:
                Capture("08_reset_confirm");
                Object.FindFirstObjectByType<LevelSelectMenu>().ConfirmReset();
                Check(!SaveSystem.IsUnlocked(2) && SaveSystem.BestScore(1) == 0 && Mathf.Approximately(SaveSystem.Settings.musicVolume, 0.3f),
                      "reset progress: levels locked again, settings kept");
                Finish();
                break;
        }
        step++;
    }

    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) { report.Add("no camera for " + name); return; }
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        foreach (var c in canvases) if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        File.WriteAllText(Path.Combine(outDir, "report.txt"),
            string.Join("\n", report) + "\n\n" + (errors.Count == 0 ? "OK - no errors" : string.Join("\n\n", errors)));
        Debug.Log($"[SmokeTest] finished: {report.Count} checks, {errors.Count} errors");
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = prevOptsEnabled;
        EditorSettings.enterPlayModeOptions = prevOpts;

        // шын сақтауды қайтарамыз
        string save = SaveSystem.FilePath;
        if (File.Exists(backup)) { File.Copy(backup, save, true); File.Delete(backup); }
        else if (File.Exists(save)) File.Delete(save);
        string broken = Path.Combine(Application.persistentDataPath, "save_broken.json");
        if (File.Exists(broken)) File.Delete(broken);

        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }
}
