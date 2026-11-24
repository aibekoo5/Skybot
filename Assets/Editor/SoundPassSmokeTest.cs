using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools → Cloud Strike → Smoke Test Sound Pass
// Play Mode-та автоматты тексеру (batch: -executeMethod SoundPassSmokeTest.Run, -nographics-сыз):
//   1) мәзірде мәзір музыкасы, Settings-те Music + SFX слайдерлері, слайдер → dB (Log10 × 20)
//   2) деңгейге өткенде деңгей музыкасына crossfade, сахна ауысса музыка басынан басталмайды
//   3) сахнадағы барлық AudioSource микшерге бағытталған, жауларда wind-up / hit / death клиптері
//   4) пауза: Paused снапшот, SFX тоқтайды, музыка ойнай береді
//   5) жеңіліс: lose дыбысы + деңгей қайта жүктеледі
//   6) дыбыс баптауы save.json-да, «қолданбаны қайта ашқанда» сол мән
// Нәтиже: Logs/SmokeTest/sound_report.txt + PNG. Шын save.json тест соңында қайтарылады.
public static class SoundPassSmokeTest
{
    static int step;
    static double stepTime;
    static readonly List<string> errors = new List<string>();
    static readonly List<string> report = new List<string>();
    static string outDir, backup;
    static bool prevOptsEnabled;
    static EnterPlayModeOptions prevOpts;
    static float musicTimeBefore;
    static int waits;

    [MenuItem("Tools/Cloud Strike/Smoke Test Sound Pass")]
    public static void Run()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/SmokeTest"));
        Directory.CreateDirectory(outDir);
        errors.Clear(); report.Clear();
        step = 0;

        string save = SaveSystem.FilePath;
        backup = save + ".soundtest-backup";
        if (File.Exists(save)) { File.Copy(save, backup, true); File.Delete(save); }

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
    static AudioManager AM => AudioManager.Instance;

    static float MixerDb(string param)
    {
        AM.Bank.mixer.GetFloat(param, out float v);
        return v;
    }

    static AudioSource[] MusicSources()
    {
        var list = new List<AudioSource>();
        foreach (AudioSource s in AM.GetComponents<AudioSource>())
            if (s.loop) list.Add(s);
        return list.ToArray();
    }

    static AudioSource PlayingMusic()
    {
        AudioSource best = null;
        foreach (AudioSource s in MusicSources())
            if (s.isPlaying && (best == null || s.volume > best.volume)) best = s;
        return best;
    }

    static int PlayingMusicCount()
    {
        int n = 0;
        foreach (AudioSource s in MusicSources()) if (s.isPlaying) n++;
        return n;
    }

    static object Field(object o, string name)
    {
        for (System.Type t = o.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(o);
        }
        return null;
    }

    static void Dump(string tag)
    {
        foreach (AudioSource s in MusicSources())
            report.Add($"      [{tag}] {(s.clip != null ? s.clip.name : "null")} playing={s.isPlaying} vol={s.volume:0.00} time={s.time:0.0} enabled={s.enabled}");
        report.Add($"      [{tag}] managers={Object.FindObjectsByType<AudioManager>().Length} unscaledTime={Time.unscaledTime:0.0} frame={Time.frameCount}");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { stepTime = EditorApplication.timeSinceStartup; return; }
        double now = EditorApplication.timeSinceStartup;
        if (now - stepTime < 3.0) return;
        stepTime = now;

        // сахна жаңа жүктелді / crossfade әлі жүріп жатыр — келесі тексеруді күтеміз (ең көбі 4 рет)
        if ((step == 1 || step == 5) && PlayingMusicCount() > 1 && waits++ < 4) return;
        waits = 0;

        switch (step)
        {
            case 0: // мәзір
            {
                Check(AM != null, "AudioManager created automatically (DontDestroyOnLoad)");
                Check(AM.CurrentMusic == AM.Bank.menuMusic && PlayingMusic() != null, "menu: menu music is playing");
                Check(PlayingMusicCount() == 1, "menu: exactly one music source playing");

                var ps = Object.FindAnyObjectByType<ProfileSettings>(FindObjectsInactive.Include);
                Check(ps != null && ps.volumeSlider != null && ps.sfxSlider != null && ps.sfxSlider != ps.volumeSlider,
                      "Settings: Music and SFX sliders are wired");
                Object.FindAnyObjectByType<MenuButtons>().Settings();
                ps.sfxSlider.value = 0.5f;          // onValueChanged → ProfileSettings.SetSfxVolume
                ps.volumeSlider.value = 0.25f;
                Check(Mathf.Abs(MixerDb("SfxVolume") - (-6.02f)) < 0.1f, $"SFX slider 0.5 -> {MixerDb("SfxVolume"):0.00} dB (expected -6.02)");
                Check(Mathf.Abs(MixerDb("MusicVolume") - (-12.04f)) < 0.1f, $"Music slider 0.25 -> {MixerDb("MusicVolume"):0.00} dB (expected -12.04)");
                Check(ps.sfxDbText != null && ps.sfxDbText.text == "-6 dB", "SFX dB label shows -6 dB");
                Capture("s01_settings_sliders");
                Object.FindAnyObjectByType<MenuButtons>().Back();
                GameSession.Play(L(0));
                break;
            }
            case 1: // Outpost
            {
                Check(AM.CurrentMusic == AM.Bank.levelMusic, "level: switched to level music");
                Dump("outpost");
                Check(PlayingMusicCount() == 1, "level: crossfade finished, old track stopped");
                int unrouted = 0, total = 0;
                foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                {
                    total++;
                    if (s.outputAudioMixerGroup == null) { unrouted++; report.Add("      not routed: " + s.name); }
                }
                Check(unrouted == 0, $"all {total} AudioSources routed to the mixer");
                var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
                bool clipsOk = enemies.Length > 0;
                foreach (EnemyBase e in enemies)
                    clipsOk &= Field(e, "telegraphClip") != null && Field(e, "deathClip") != null && ((AudioClip[])Field(e, "hitClips")).Length == 2;
                Check(clipsOk, $"{enemies.Length} enemies have wind-up, hit (2) and death clips");
                musicTimeBefore = PlayingMusic().time;
                Capture("s02_outpost");
                GameSession.Play(L(1));
                break;
            }
            case 2: // Level1 — музыка жалғасады
            {
                Dump("level1");
                float t = PlayingMusic().time;
                Check(AM.CurrentMusic == AM.Bank.levelMusic && t > musicTimeBefore + 1f,
                      $"next level: music NOT restarted ({musicTimeBefore:0.0} s -> {t:0.0} s)");
                Object.FindAnyObjectByType<PauseMenu>().Pause();
                break;
            }
            case 3: // пауза
            {
                Check(AM.IsPaused && AudioListener.pause, "pause: Paused snapshot + SFX paused");
                Check(PlayingMusic() != null && PlayingMusic().ignoreListenerPause, "pause: music keeps playing (softer, lowpass)");
                Capture("s03_paused");
                Object.FindAnyObjectByType<PauseMenu>().Resume();
                Check(!AM.IsPaused && !AudioListener.pause, "resume: Normal snapshot back");
                Object.FindAnyObjectByType<Health>().TakeDamage(99);
                break;
            }
            case 4: // жеңіліс → қайта жүктелді
            {
                Check(SceneManager.GetActiveScene().name == L(1).sceneName && Object.FindAnyObjectByType<Health>().hp > 0,
                      "lose: lose sound + level restarted");
                // «қолданбаны жауып ашу»
                SaveSystem.Load();
                Check(Mathf.Approximately(SaveSystem.Settings.sfxVolume, 0.5f) && Mathf.Approximately(SaveSystem.Settings.musicVolume, 0.25f),
                      $"save.json after reload: music {SaveSystem.Settings.musicVolume}, sfx {SaveSystem.Settings.sfxVolume}");
                File.WriteAllText(Path.Combine(outDir, "save_after_sound.json"), File.ReadAllText(SaveSystem.FilePath));
                SceneManager.LoadScene("Main menu");
                break;
            }
            case 5:
            {
                Check(AM.CurrentMusic == AM.Bank.menuMusic && PlayingMusicCount() == 1, "back to menu: crossfade to menu music");
                var ps = Object.FindAnyObjectByType<ProfileSettings>(FindObjectsInactive.Include);
                Check(Mathf.Approximately(ps.sfxSlider.value, 0.5f) && Mathf.Approximately(ps.volumeSlider.value, 0.25f),
                      "menu sliders show the saved values");
                Finish();
                break;
            }
        }
        step++;
    }

    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) { report.Add("no camera for " + name); return; }
        var canvases = Object.FindObjectsByType<Canvas>();
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
        File.WriteAllText(Path.Combine(outDir, "sound_report.txt"),
            string.Join("\n", report) + "\n\n" + (errors.Count == 0 ? "OK - no errors" : string.Join("\n\n", errors)));
        Debug.Log($"[SmokeTest] sound: {report.Count} checks, {errors.Count} errors");
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = prevOptsEnabled;
        EditorSettings.enterPlayModeOptions = prevOpts;

        string save = SaveSystem.FilePath;
        if (File.Exists(backup)) { File.Copy(backup, save, true); File.Delete(backup); }
        else if (File.Exists(save)) File.Delete(save);

        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }
}
