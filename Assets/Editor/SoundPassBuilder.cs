using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Assignment 12 — Sound Pass. Tools → Cloud Strike → Build Sound Pass (қайта іске қосуға болады).
//  1) аудио файлдарды ретке келтіреді (Assets/Audio/SFX, Assets/Audio/Music) + импорт баптаулары
//  2) AudioMixer жасайды: Master → Music → MusicBus, Master → SFX → SfxBus,
//     exposed: MasterVolume / MusicVolume / SfxVolume, снапшоттар Normal / Paused, MusicBus-та Lowpass
//  3) Resources/SoundBank.asset — AudioManager-дің баптаулары
//  4) prefab-тар мен сахналардағы БАРЛЫҚ AudioSource → SfxBus, жауларға wind-up / hit / death клиптері
//  5) Main menu: Effects volume слайдері + dB жазулары
//
// Unity-де AudioMixer жасайтын ашық API жоқ (тек Create → Audio Mixer мәзірі), сондықтан мәзір
// өзі қолданатын ішкі UnityEditor.Audio.AudioMixerController класын reflection арқылы шақырамыз.
public static class SoundPassBuilder
{
    const string AudioDir = "Assets/Audio";
    const string SfxDir = "Assets/Audio/SFX";
    const string MusicDir = "Assets/Audio/Music";
    const string MixerPath = "Assets/Audio/SkyBotMixer.mixer";
    const string BankPath = "Assets/Resources/SoundBank.asset";
    const string MenuScene = "Assets/Scenes/Main menu.unity";

    // микс деңгейлері (Normal снапшот), dB
    const float MusicMixDb = -8f, SfxMixDb = -3f;
    // Paused снапшот: музыка тыныштау және «қабырғаның арғы жағынан» естілгендей
    const float MusicPausedDb = -16f, LowpassNormalHz = 22000f, LowpassPausedHz = 700f;

    [MenuItem("Tools/Cloud Strike/Build Sound Pass (Assignment 12)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        OrganizeFiles();
        ImportSettings();
        AudioMixer mixer = BuildMixer();
        SoundBank bank = BuildBank(mixer);
        SetupPrefabs(bank);
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.enabled) SetupScene(s.path, bank);

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScene);
        Debug.Log("[Sound] Sound Pass built. Mixer: " + MixerPath);
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    // =====================================================================
    // 1) файлдар
    // =====================================================================
    static void OrganizeFiles()
    {
        Folder("Assets", "Resources");
        Folder(AudioDir, "SFX");
        Folder(AudioDir, "Music");

        // бұрын Assets түбінде жатқан mp3-тер. MoveAsset GUID-ті сақтайды — сілтемелер үзілмейді.
        Move("Assets/Scenes/the_mountain-retro-game-593063.mp3", MusicDir);
        Move("Assets/driken5482-retro-hurt-2-236675.mp3", SfxDir);
        Move("Assets/explosion-in-tight-space-gamemaster-audio-no-reverb-tail-1-00-00.mp3", SfxDir);
        Move("Assets/freesound_community-bullet-hit-metal-84818.mp3", SfxDir);
        Move("Assets/freesound_community-heal-up-39285.mp3", SfxDir);
        Move("Assets/lost-money-on-the-game-account.mp3", SfxDir);
        AssetDatabase.Refresh();
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void Move(string from, string toDir)
    {
        if (AssetDatabase.LoadAssetAtPath<AudioClip>(from) == null) return;
        string to = toDir + "/" + System.IO.Path.GetFileName(from);
        string err = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(err)) Debug.LogWarning("[Sound] move " + from + ": " + err);
    }

    static void ImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            bool music = path.StartsWith(MusicDir);
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            if (music)
            {
                // ұзын трек: жадқа толық ашпай, ағынмен ойнатамыз (телефон жады үшін)
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                imp.loadInBackground = true;
            }
            else
            {
                // қысқа эффект: бірден ойнау үшін жадқа ашылған күйде
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                imp.forceToMono = true;
            }
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    // =====================================================================
    // 2) микшер (reflection — Unity-дің ішкі редактор класы)
    // =====================================================================
    const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static Type EdType(string fullName)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = a.GetType(fullName);
            if (t != null) return t;
        }
        throw new Exception("[Sound] type not found: " + fullName);
    }

    static object Call(object target, string method, params object[] args)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        for (Type tt = t; tt != null; tt = tt.BaseType)
            foreach (MethodInfo m in tt.GetMethods(BF | BindingFlags.DeclaredOnly))
                if (m.Name == method && m.GetParameters().Length == args.Length)
                    return m.Invoke(inst, args);
        throw new MissingMethodException(t.Name, method);
    }

    static object Prop(object o, string name) => o.GetType().GetProperty(name, BF).GetValue(o);
    static void SetProp(object o, string name, object v) => o.GetType().GetProperty(name, BF).SetValue(o, v);

    static AudioMixer BuildMixer()
    {
        AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null && existing.FindMatchingGroups("MusicBus").Length > 0 && existing.FindSnapshot("Paused") != null)
        {
            Debug.Log("[Sound] mixer already exists, keeping it (delete " + MixerPath + " to rebuild)");
            existing.updateMode = AudioMixerUpdateMode.UnscaledTime;
            EditorUtility.SetDirty(existing);
            return existing;
        }
        if (existing != null) AssetDatabase.DeleteAsset(MixerPath);

        Type ctrlT = EdType("UnityEditor.Audio.AudioMixerController");
        Type effT = EdType("UnityEditor.Audio.AudioMixerEffectController");
        Type pathT = EdType("UnityEditor.Audio.AudioGroupParameterPath");

        object ctrl = Call(ctrlT, "CreateMixerControllerAtPath", MixerPath);
        object master = Prop(ctrl, "masterGroup");

        object music = NewGroup(ctrl, "Music", master);
        object musicBus = NewGroup(ctrl, "MusicBus", music);
        object sfx = NewGroup(ctrl, "SFX", master);
        object sfxBus = NewGroup(ctrl, "SfxBus", sfx);

        // MusicBus-қа Lowpass (Paused снапшотта жабылады)
        object lowpass = Activator.CreateInstance(effT, BF, null, new object[] { "Lowpass" }, null);
        Call(ctrl, "AddNewSubAsset", lowpass, false);
        Call(lowpass, "PreallocateGUIDs");
        Call(musicBus, "InsertEffect", lowpass, 1);
        string cutoffName = FindParam(lowpass, "cutoff");

        // снапшоттар: бастапқысы → Normal, оның көшірмесі → Paused
        Array snaps = (Array)Prop(ctrl, "snapshots");
        object normal = snaps.GetValue(0);
        ((Object)normal).name = "Normal";
        Call(ctrl, "CloneNewSnapshotFromTarget", false);
        snaps = (Array)Prop(ctrl, "snapshots");
        object paused = snaps.GetValue(snaps.Length - 1);
        ((Object)paused).name = "Paused";

        SetVolume(ctrl, normal, musicBus, MusicMixDb);
        SetVolume(ctrl, paused, musicBus, MusicPausedDb);
        SetVolume(ctrl, normal, sfxBus, SfxMixDb);
        SetVolume(ctrl, paused, sfxBus, SfxMixDb);
        Call(lowpass, "SetValueForParameter", ctrl, normal, cutoffName, LowpassNormalHz);
        Call(lowpass, "SetValueForParameter", ctrl, paused, cutoffName, LowpassPausedHz);

        // Audio Mixer терезесіндегі «View» — қай топтар көрінеді (жаңа asset-те view жоқ)
        SetView(ctrl, master, music, musicBus, sfx, sfxBus);

        SetProp(ctrl, "TargetSnapshot", normal);
        SetProp(ctrl, "startSnapshot", normal);

        // ойыншының слайдерлері: Master / Music / SFX топтарының дыбыс деңгейі (exposed)
        Expose(ctrl, pathT, master, "MasterVolume");
        Expose(ctrl, pathT, music, "MusicVolume");
        Expose(ctrl, pathT, sfx, "SfxVolume");

        AudioMixer mixer = (AudioMixer)ctrl;
        mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;   // пауза (timeScale = 0) кезінде снапшот ауысуы жүру үшін
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath);
        Debug.Log("[Sound] mixer created: " + MixerPath + " (lowpass parameter: " + cutoffName + ")");
        return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
    }

    static object NewGroup(object ctrl, string name, object parent)
    {
        object g = Call(ctrl, "CreateNewGroup", name, false);
        Call(ctrl, "AddChildToParent", g, parent);
        return g;
    }

    static void SetView(object ctrl, params object[] groups)
    {
        Type viewT = EdType("UnityEditor.Audio.MixerGroupView");
        Type guidT = Prop(groups[0], "groupID").GetType();
        Array guids = Array.CreateInstance(guidT, groups.Length);
        for (int i = 0; i < groups.Length; i++) guids.SetValue(Prop(groups[i], "groupID"), i);
        object view = Activator.CreateInstance(viewT);
        viewT.GetField("guids", BF).SetValue(view, guids);
        viewT.GetField("name", BF).SetValue(view, "View");
        Array views = Array.CreateInstance(viewT, 1);
        views.SetValue(view, 0);
        try { SetProp(ctrl, "views", views); }
        catch (Exception e) { Debug.LogWarning("[Sound] could not set mixer view: " + e.Message); }
    }

    static void SetVolume(object ctrl, object snapshot, object group, float db)
    {
        Call(group, "SetValueForVolume", ctrl, snapshot, db);
    }

    static string FindParam(object effect, string contains)
    {
        // эффект параметрлерінің аттары native жақта — MixerEffectDefinitions-тан сұраймыз
        Type defs = EdType("UnityEditor.Audio.MixerEffectDefinitions");
        Array ps = (Array)Call(defs, "GetEffectParameters", Prop(effect, "effectName"));
        var names = new List<string>();
        foreach (object p in ps) names.Add((string)p.GetType().GetField("name", BF).GetValue(p));
        string found = names.FirstOrDefault(n => n.ToLowerInvariant().Contains(contains));
        if (found == null) throw new Exception("[Sound] no '" + contains + "' parameter, have: " + string.Join(", ", names));
        return found;
    }

    static void Expose(object ctrl, Type pathT, object group, string name)
    {
        object guid = Call(group, "GetGUIDForVolume");
        object path = Activator.CreateInstance(pathT, BF, null, new[] { group, guid }, null);
        Call(ctrl, "AddExposedParameter", path);

        // жаңа параметрдің аты «MyExposedParam» — өз атымызға ауыстырамыз
        Array arr = (Array)Prop(ctrl, "exposedParameters");
        for (int i = 0; i < arr.Length; i++)
        {
            object p = arr.GetValue(i);
            FieldInfo gf = p.GetType().GetField("guid", BF), nf = p.GetType().GetField("name", BF);
            if (!gf.GetValue(p).Equals(guid)) continue;
            nf.SetValue(p, name);
            arr.SetValue(p, i);
        }
        SetProp(ctrl, "exposedParameters", arr);
        try { Call(ctrl, "OnChangedExposedParameter"); } catch (MissingMethodException) { }
    }

    // =====================================================================
    // 3) SoundBank
    // =====================================================================
    static AudioClip Clip(string path)
    {
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning("[Sound] clip not found: " + path);
        return c;
    }

    static SoundBank BuildBank(AudioMixer mixer)
    {
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.mixer = mixer;
        bank.musicGroup = mixer.FindMatchingGroups("MusicBus")[0];
        bank.sfxGroup = mixer.FindMatchingGroups("SfxBus")[0];
        bank.normalSnapshot = mixer.FindSnapshot("Normal");
        bank.pausedSnapshot = mixer.FindSnapshot("Paused");
        bank.menuMusic = Clip(MusicDir + "/the_mountain-retro-game-593063.mp3");
        bank.levelMusic = Clip(MusicDir + "/music_level.wav");
        bank.uiClick = new[] { Clip(SfxDir + "/ui_click.wav"), Clip(SfxDir + "/ui_click_2.wav") };
        bank.win = Clip(AudioDir + "/win.wav");
        bank.lose = Clip(SfxDir + "/lose.wav");
        EditorUtility.SetDirty(bank);
        return bank;
    }

    // =====================================================================
    // 4) prefab-тар
    // =====================================================================
    static void SetupPrefabs(SoundBank bank)
    {
        AudioClip[] hits = { Clip(SfxDir + "/enemy_hit_1.wav"), Clip(SfxDir + "/enemy_hit_2.wav") };
        AudioClip death = Clip(SfxDir + "/enemy_death.wav");
        var windUps = new Dictionary<string, AudioClip>
        {
            { "Rifleman", Clip(SfxDir + "/windup_rifle.wav") },
            { "MachineGun", Clip(SfxDir + "/windup_mg.wav") },
            { "Brawler", Clip(SfxDir + "/windup_brawler.wav") },
        };

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            foreach (AudioSource s in root.GetComponentsInChildren<AudioSource>(true))
                changed |= Route(s, bank);

            EnemyBase enemy = root.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                var so = new SerializedObject(enemy);
                if (windUps.TryGetValue(root.name, out AudioClip w)) so.FindProperty("telegraphClip").objectReferenceValue = w;
                SerializedProperty hp = so.FindProperty("hitClips");
                hp.arraySize = hits.Length;
                for (int i = 0; i < hits.Length; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = hits[i];
                so.FindProperty("deathClip").objectReferenceValue = death;
                so.ApplyModifiedPropertiesWithoutUndo();

                // жаудың дыбысы 3D, бірақ алыстан да естілетіндей: wind-up-ты экранға кірмей тұрып естиміз
                AudioSource src = root.GetComponent<AudioSource>();
                if (src == null) src = root.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.75f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 6f;
                src.maxDistance = 45f;
                src.dopplerLevel = 0f;
                src.priority = 64;             // жау шабуылы — маңызды дыбыс
                Route(src, bank);
                changed = true;
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // AudioSource-ты SfxBus-қа бағыттайды (музыка болса — MusicBus). Өзгерсе true.
    static bool Route(AudioSource s, SoundBank bank)
    {
        AudioMixerGroup want = IsMusic(s, bank) ? bank.musicGroup : bank.sfxGroup;
        if (s.outputAudioMixerGroup == want) return false;
        s.outputAudioMixerGroup = want;
        EditorUtility.SetDirty(s);
        return true;
    }

    static bool IsMusic(AudioSource s, SoundBank bank) =>
        s.clip != null && (s.clip == bank.menuMusic || s.clip == bank.levelMusic);

    // =====================================================================
    // 5) сахналар
    // =====================================================================
    static void SetupScene(string path, SoundBank bank)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);   // OpenScene-нен кейін қайта аламыз

        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (IsMusic(s, bank))
            {
                // сахнаның өз музыкасы енді керек емес — AudioManager ойнайды (crossfade, үзіліссіз)
                s.playOnAwake = false;
                s.enabled = false;
            }
            Route(s, bank);
        }

        Movement mv = Object.FindAnyObjectByType<Movement>(FindObjectsInactive.Include);
        if (mv != null)
        {
            mv.jumpSound2 = Clip(SfxDir + "/jump_2.wav");
            mv.coinSound2 = Clip(SfxDir + "/pickup_2.wav");
            EditorUtility.SetDirty(mv);
        }

        // сахнаға тікелей қойылған жаулар (prefab instance) prefab-тан алады; prefab емес болса — қолмен
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include))
            if (!PrefabUtility.IsPartOfPrefabInstance(e))
                Debug.LogWarning("[Sound] enemy is not a prefab instance, check its clips: " + e.name, e);

        if (path == MenuScene) BuildSfxSlider(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Sound] scene ready: " + path);
    }

    static void BuildSfxSlider(Scene scene)
    {
        ProfileSettings ps = Object.FindAnyObjectByType<ProfileSettings>(FindObjectsInactive.Include);
        Canvas canvas = MobileUIBuilder.FindCanvas(scene);
        RectTransform root = canvas.transform as RectTransform;
        RectTransform panel = MobileUIBuilder.FindDeep(root, "SettingsPanel");
        if (ps == null || panel == null) { Debug.LogError("[Sound] Main menu: ProfileSettings or SettingsPanel not found"); return; }

        RectTransform musicSlider = MobileUIBuilder.FindDeep(panel, "Slider");
        RectTransform musicLabel = MobileUIBuilder.FindDeep(panel, "MusicVolume");
        Transform rowParent = musicSlider.parent;

        // музыка слайдері: ескі MenuButtons.SetVolume сілтемесін алып тастаймыз — енді ProfileSettings
        Slider ms = musicSlider.GetComponent<Slider>();
        for (int i = ms.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(ms.onValueChanged, i);
        ms.minValue = 0f; ms.maxValue = 1f; ms.wholeNumbers = false;

        RectTransform sfxSlider = MobileUIBuilder.FindDeep(panel, "SliderSfx");
        if (sfxSlider == null)
        {
            sfxSlider = (RectTransform)Object.Instantiate(musicSlider.gameObject, rowParent).transform;
            sfxSlider.name = "SliderSfx";
        }
        Slider ss = sfxSlider.GetComponent<Slider>();
        for (int i = ss.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(ss.onValueChanged, i);
        ss.minValue = 0f; ss.maxValue = 1f; ss.wholeNumbers = false; ss.value = 0.8f;

        RectTransform sfxLabel = MobileUIBuilder.FindDeep(panel, "SfxVolume");
        if (sfxLabel == null)
        {
            sfxLabel = (RectTransform)Object.Instantiate(musicLabel.gameObject, rowParent).transform;
            sfxLabel.name = "SfxVolume";
        }

        TextMeshProUGUI musicDb = DbText(rowParent, "MusicDb");
        TextMeshProUGUI sfxDb = DbText(rowParent, "SfxDb");

        ps.volumeSlider = ms;
        ps.sfxSlider = ss;
        ps.musicDbText = musicDb;
        ps.sfxDbText = sfxDb;
        EditorUtility.SetDirty(ps);

        MobileUIBuilder.BuildSettingsPanel(root);   // қатарларды қайта орналастырады (SFX қатары бар)
    }

    static TextMeshProUGUI DbText(Transform parent, string name)
    {
        RectTransform rt = MobileUIBuilder.FindDeep(parent, name) ?? MobileUIBuilder.NewRect(name, parent);
        TextMeshProUGUI t = MobileUIBuilder.Ensure<TextMeshProUGUI>(rt);
        t.text = "-2 dB";
        return t;
    }
}
