using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools → Cloud Strike → Smoke Test Sky Levels
// Әр деңгейді (LevelList ретімен) ашып, 3 с күтеді де тексереді:
//   * сахна атауы деңгейдің нөміріне сай (LevelN_...)
//   * LevelData-дағы барлық жау пайда болды, ешқайсысы платформадан құламады
//   * жүретін жаулар (NavMeshAgent) NavMesh-те тұр, әр жаудың астында Ground бар
// Скриншот: ойын камерасы + жоғарыдан шолу (Logs/SmokeTest/sky_*.png), есеп sky_report.txt
public static class SkyLevelsSmokeTest
{
    static int level;
    static double stepTime;
    static bool waitingLoad;
    static readonly List<string> errors = new List<string>();
    static readonly List<string> report = new List<string>();
    static string outDir;
    static bool prevOptsEnabled;
    static EnterPlayModeOptions prevOpts;

    [MenuItem("Tools/Cloud Strike/Smoke Test Sky Levels")]
    public static void Run()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/SmokeTest"));
        Directory.CreateDirectory(outDir);
        errors.Clear(); report.Clear();
        level = 0; waitingLoad = false;

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
            errors.Add($"[level {level}] {msg}\n{stack}");
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) errors.Add("CHECK FAILED: " + what);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { stepTime = EditorApplication.timeSinceStartup; return; }
        double now = EditorApplication.timeSinceStartup;
        if (now - stepTime < 3.5) return;
        stepTime = now;

        var levels = LevelList.Instance.levels;
        if (!waitingLoad)
        {
            if (level >= levels.Length) { Finish(); return; }
            GameSession.Play(levels[level]);
            waitingLoad = true;
            return;
        }
        waitingLoad = false;
        CheckLevel(levels[level]);
        level++;
    }

    static void CheckLevel(LevelData d)
    {
        string scene = SceneManager.GetActiveScene().name;
        report.Add($"--- {d.levelNumber}. {d.displayName} ({scene})");
        Check(scene == d.sceneName && scene.StartsWith("Level" + d.levelNumber + "_"), $"scene name '{scene}' matches level number {d.levelNumber}");
        Check(d.enemies.Count > 0, $"LevelData has {d.enemies.Count} enemies");

        var enemies = Object.FindObjectsByType<EnemyBase>();
        Check(enemies.Length == d.enemies.Count, $"all enemies spawned ({enemies.Length}/{d.enemies.Count})");
        foreach (EnemyBase e in enemies)
        {
            Vector3 p = e.transform.position;
            NavMeshAgent a = e.GetComponent<NavMeshAgent>();
            bool onMesh = a == null || a.isOnNavMesh;
            bool ground = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore)
                          && !hit.transform.IsChildOf(e.transform);
            Check(onMesh && ground && !e.IsDead, $"{e.name.Replace("(Clone)", "")} at {p.x:0.0},{p.y:0.0},{p.z:0.0}: {(a == null ? "static" : "on NavMesh " + onMesh)}, ground below {ground}");
        }
        Capture($"sky_{d.levelNumber}_game", Camera.main);
        Overview($"sky_{d.levelNumber}_overview");
    }

    // жоғарыдан шолу: Ground коллайдерлерінің шекарасы бойынша уақытша камера
    static void Overview(string name)
    {
        Bounds b = default; bool has = false;
        foreach (Collider c in Object.FindObjectsByType<Collider>())
        {
            if (c.gameObject.layer != 3 || c.isTrigger) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        if (!has) return;
        var go = new GameObject("OverviewCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 1000f;
        float size = Mathf.Max(b.size.x, b.size.z);
        go.transform.position = b.center + new Vector3(size * 0.45f, size * 0.75f, -size * 0.45f);
        go.transform.LookAt(b.center);
        Capture(name, cam);
        Object.DestroyImmediate(go);
    }

    static void Capture(string name, Camera cam)
    {
        if (cam == null) { report.Add("no camera for " + name); return; }
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
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        File.WriteAllText(Path.Combine(outDir, "sky_report.txt"),
            string.Join("\n", report) + "\n\n" + (errors.Count == 0 ? "OK - no errors" : string.Join("\n\n", errors)));
        Debug.Log($"[SmokeTest] sky levels: {report.Count} lines, {errors.Count} errors");
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = prevOptsEnabled;
        EditorSettings.enterPlayModeOptions = prevOpts;
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }
}
