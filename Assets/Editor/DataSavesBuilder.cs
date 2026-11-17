using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MobileUIBuilder;

// Tools → Sky Bot → Build Data And Saves (Assignment 11)
// 1) Assets/Data-да баланс asset-терін жасайды (PlayerStats, үш EnemyStats, PatrollerStats,
//    BossStats, ItemValues) — мәндері бұрын кодта/prefab-та тұрған сандармен бірдей.
// 2) Төрт LevelData + Resources/LevelList жасайды.
// 3) Prefab-тар мен сахналарға asset сілтемелерін қояды; Outpost жауларын және Level1/Level 2
//    спаунерлерін сахнадан LevelData-ға көшіреді (енді оларды LevelRunner жасайды).
// 4) Main menu-ге Level Select + Reset progress (растау терезесімен) + сақтау статусын қосады.
// Қайта іске қосуға болады: бар asset-тердің мәндерін ӨЗГЕРТПЕЙДІ (сенің баптауларың қалады).
public static class DataSavesBuilder
{
    const string DataDir = "Assets/Data";
    const string MenuScene = "Assets/Scenes/Main menu.unity";

    // ескі Spawner-лердегі заттар (Bomb, Rock, Heart) — GUID бойынша
    static readonly string[] WaveHazardGuids =
    {
        "7199024f64feff841887f4d99ea7bcd3",   // BombHazard 1
        "7d3ee687cd822524bbe6f9fd4c6a2f2a",   // Rock
        "35ffb05ada7ecfb4a90bfc19896b144c",   // Heart 1
    };

    static PlayerStats player;
    static ItemValues items;
    static PatrollerStats patroller;
    static BossStats boss;

    [MenuItem("Tools/Sky Bot/Build Data And Saves (Assignment 11)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Folder("Assets", "Data");
        Folder(DataDir, "Enemies");
        Folder(DataDir, "Levels");
        Folder("Assets", "Resources");

        // ---------- 1) баланс asset-тері ----------
        player = Asset<PlayerStats>(DataDir + "/PlayerStats.asset");
        items = Asset<ItemValues>(DataDir + "/ItemValues.asset");
        patroller = Asset<PatrollerStats>(DataDir + "/Enemies/PatrollerStats.asset");
        boss = Asset<BossStats>(DataDir + "/BossStats.asset");

        var rifleman = Asset<RiflemanStats>(DataDir + "/Enemies/RiflemanStats.asset");
        var gun = Asset<MachineGunStats>(DataDir + "/Enemies/MachineGunStats.asset", s =>
        {
            s.maxHp = 4; s.viewDistance = 16f; s.viewAngle = 140f; s.scoreValue = 200;   // prefab-тағы мәндер
        });
        var brawler = Asset<BrawlerStats>(DataDir + "/Enemies/BrawlerStats.asset", s => { s.maxHp = 2; });

        // ---------- prefab-тар ----------
        SetOnPrefab("Assets/Prefabs/Enemies/Rifleman.prefab", typeof(Rifleman), "stats", rifleman);
        SetOnPrefab("Assets/Prefabs/Enemies/MachineGun.prefab", typeof(MachineGunTurret), "stats", gun);
        SetOnPrefab("Assets/Prefabs/Enemies/Brawler.prefab", typeof(Brawler), "stats", brawler);
        foreach (string p in new[] { "Assets/Prefabs/Rock.prefab", "Assets/Prefabs/Heart 1.prefab",
                                     "Assets/Prefabs/BombHazard 1.prefab", "Assets/Prefabs/BossFireball.prefab" })
            SetOnPrefab(p, typeof(Hazard), "items", items);

        // ---------- 2) деңгейлер ----------
        LevelData l1 = Level("Level_01", 1, "Outpost", "Level2_Outpost", LevelGoal.DefeatAllEnemies,
                             "Defeat every enemy - the gate opens", 0f);
        LevelData l2 = Level("Level_02", 2, "Sky Islands", "Level1", LevelGoal.CollectCoins,
                             "Collect every coin and reach the gate", 150f);
        LevelData l3 = Level("Level_03", 3, "Spike Bridge", "Level 2", LevelGoal.CollectCoins,
                             "Collect every coin - watch the spikes", 150f);
        LevelData l4 = Level("Level_04", 4, "Boss Arena", "Level3_Boss", LevelGoal.DefeatBoss,
                             "Defeat the boss", 0f);

        LevelList list = Asset<LevelList>("Assets/Resources/LevelList.asset");
        list.levels = new[] { l1, l2, l3, l4 };
        EditorUtility.SetDirty(list);

        AssetDatabase.SaveAssets();   // сахна ашылғанда жаңа asset-тер жадтан түсіп қалмасын

        // ---------- 3) сахналар ----------
        MigrateLevelScene("Level_01", null);
        MigrateLevelScene("Level_02", new HazardWave { halfSize = new Vector2(12f, 2f), spawnHeight = 6f, firstDelay = 1f, interval = 2f });
        MigrateLevelScene("Level_03", new HazardWave { halfSize = new Vector2(1f, 5f), spawnHeight = 12f, firstDelay = 1f, interval = 1.4f });
        MigrateLevelScene("Level_04", null);

        // ---------- 4) мәзір ----------
        BuildMenu();

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScene);
        Debug.Log("[Sky Bot] Data and saves built: Assets/Data (stats), 4 LevelData, Resources/LevelList, level select in Main menu.");
    }

    public static void RunBatch()
    {
        Run();
        EditorApplication.Exit(0);
    }

    // =====================================================================
    // asset-тер
    // =====================================================================
    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    // жоқ болса жасайды (init — бастапқы мәндер), бар болса тиіспейді
    static T Asset<T>(string path, System.Action<T> init = null) where T : ScriptableObject
    {
        T a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        init?.Invoke(a);
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static LevelData Level(string file, int number, string title, string scene, LevelGoal goal, string goalText, float time)
    {
        string path = $"{DataDir}/Levels/{file}.asset";
        bool isNew = AssetDatabase.LoadAssetAtPath<LevelData>(path) == null;
        LevelData l = Asset<LevelData>(path);
        if (isNew)
        {
            l.levelNumber = number; l.displayName = title; l.sceneName = scene;
            l.goal = goal; l.goalText = goalText; l.timeLimit = time;
            EditorUtility.SetDirty(l);
        }
        return l;
    }

    static void SetOnPrefab(string path, System.Type type, string field, Object value)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null) { Debug.LogWarning("[Sky Bot] prefab not found: " + path); return; }
        foreach (Component c in root.GetComponentsInChildren(type, true))
            SetRef(c, field, value);
        EditorUtility.SetDirty(root);
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[Sky Bot] {target.GetType().Name} has no field '{field}'"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // деңгей сахнасы
    // =====================================================================
    static void MigrateLevelScene(string levelFile, HazardWave oldSpawnerSettings)
    {
        string levelPath = $"{DataDir}/Levels/{levelFile}.asset";
        string sceneName = AssetDatabase.LoadAssetAtPath<LevelData>(levelPath).sceneName;
        Scene scene = EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity");

        // OpenScene жадтағы asset-терді түсіріп тастауы мүмкін — жолымен қайта аламыз
        LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(levelPath);
        player = AssetDatabase.LoadAssetAtPath<PlayerStats>(DataDir + "/PlayerStats.asset");
        items = AssetDatabase.LoadAssetAtPath<ItemValues>(DataDir + "/ItemValues.asset");
        patroller = AssetDatabase.LoadAssetAtPath<PatrollerStats>(DataDir + "/Enemies/PatrollerStats.asset");
        boss = AssetDatabase.LoadAssetAtPath<BossStats>(DataDir + "/BossStats.asset");

        // ойыншы
        foreach (Movement m in Object.FindObjectsByType<Movement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SetRef(m, "stats", player);
            SetRef(m, "items", items);
            Health h = m.GetComponent<Health>();
            if (h != null) SetRef(h, "stats", player);
            Shooter s = m.GetComponent<Shooter>();
            if (s != null) SetRef(s, "stats", player);
        }
        foreach (Patrol p in Object.FindObjectsByType<Patrol>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(p, "stats", patroller);
        foreach (Hazard h in Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(h, "items", items);
        foreach (Boss b in Object.FindObjectsByType<Boss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(b, "stats", boss);

        MoveEnemiesToData(level);
        MoveSpawnersToData(level, oldSpawnerSettings);

        // LevelRunner — барлық деңгейде бірдей
        LevelRunner runner = Object.FindFirstObjectByType<LevelRunner>(FindObjectsInactive.Include);
        if (runner == null) runner = new GameObject("LevelRunner").AddComponent<LevelRunner>();
        SetRef(runner, "items", items);

        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();                 // алдымен деректер, сосын сахна
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // сахнада қолмен қойылған жаулар → LevelData.enemies, сахнадан өшіріледі
    static void MoveEnemiesToData(LevelData level)
    {
        EnemyBase[] placed = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (placed.Length == 0) return;

        if (level.enemies.Count > 0)
        {
            Debug.LogWarning($"[Sky Bot] {level.name} already has enemies in data — scene enemies left as they are");
            return;
        }

        var toDelete = new HashSet<GameObject>();
        foreach (EnemyBase e in placed)
        {
            EnemyBase prefab = PrefabUtility.GetCorrespondingObjectFromSource(e);
            if (prefab == null) { Debug.LogWarning($"[Sky Bot] {e.name} is not a prefab instance — skipped"); continue; }

            var spawn = new EnemySpawn
            {
                prefab = prefab,
                position = e.transform.position,
                yaw = e.transform.eulerAngles.y,
            };

            SerializedProperty wps = new SerializedObject(e).FindProperty("waypoints");
            if (wps != null && wps.arraySize > 0)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i < wps.arraySize; i++)
                {
                    var t = wps.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (t == null) continue;
                    pts.Add(t.position);
                    toDelete.Add(t.gameObject);
                }
                spawn.patrolPoints = pts.ToArray();
            }
            level.enemies.Add(spawn);
            toDelete.Add(PrefabUtility.GetOutermostPrefabInstanceRoot(e) ?? e.gameObject);
        }

        var parents = new HashSet<Transform>();
        foreach (GameObject go in toDelete)
            if (go != null && go.transform.parent != null) parents.Add(go.transform.parent);
        foreach (GameObject go in toDelete)
            if (go != null) Object.DestroyImmediate(go);
        // бос қалған «Enemies» / «Waypoints» топтары
        foreach (Transform p in parents)
            if (p != null && p.childCount == 0 && p.GetComponents<Component>().Length == 1) Object.DestroyImmediate(p.gameObject);

        Debug.Log($"[Sky Bot] {level.name}: {level.enemies.Count} enemies moved from the scene into data");
    }

    // ескі Spawner (сандары кодта/сахнада еді) → LevelData.hazardWaves
    static void MoveSpawnersToData(LevelData level, HazardWave settings)
    {
        Spawner[] spawners = Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spawners.Length == 0) return;

        if (level.hazardWaves.Count == 0 && settings != null)
        {
            var hazards = new List<Hazard>();
            foreach (string guid in WaveHazardGuids)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.TryGetComponent(out Hazard h)) hazards.Add(h);
            }
            foreach (Spawner s in spawners)
            {
                level.hazardWaves.Add(new HazardWave
                {
                    hazards = hazards.ToArray(),
                    center = s.transform.position,
                    halfSize = settings.halfSize,
                    spawnHeight = settings.spawnHeight,
                    firstDelay = settings.firstDelay,
                    interval = settings.interval,
                });
            }
            Debug.Log($"[Sky Bot] {level.name}: {spawners.Length} hazard spawner(s) moved into data");
        }
        foreach (Spawner s in spawners) Object.DestroyImmediate(s);   // GameObject қалады, тек компонент
    }

    // =====================================================================
    // Main menu: Level Select + Reset progress + сақтау статусы
    // =====================================================================

    static void BuildMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScene);
        MenuButtons menu = Object.FindFirstObjectByType<MenuButtons>(FindObjectsInactive.Include);
        ProfileSettings profile = Object.FindFirstObjectByType<ProfileSettings>(FindObjectsInactive.Include);
        Canvas canvas = menu.mainMenuPanel.GetComponentInParent<Canvas>(true);
        RectTransform root = canvas.transform as RectTransform;

        // сақтау статусы — басты мәзірдің астында
        RectTransform mainSafe = Child(menu.mainMenuPanel.transform, "SafeArea") ?? (RectTransform)menu.mainMenuPanel.transform;
        TextMeshProUGUI status = MakeText(mainSafe, "SaveStatus", "Save status", 34, FontStyles.Normal, Color.white);
        Place(status.rectTransform, Anchor.BottomCenter, new Vector2(0, 30), new Vector2(1600, 60));

        // Level Select терезесі
        RectTransform panel = Child(root, "LevelSelectPanel") ?? NewRect("LevelSelectPanel", root);
        Stretch(panel);
        Image bg = Ensure<Image>(panel); bg.sprite = null; bg.color = NavySolid;
        panel.SetSiblingIndex(menu.settingsPanel.transform.GetSiblingIndex());
        RectTransform safe = SafeAreaIn(panel);
        LevelSelectMenu select = Ensure<LevelSelectMenu>(panel);

        TextMeshProUGUI title = MakeText(safe, "Title", "SELECT LEVEL", 80, FontStyles.Bold, Color.white);
        Place(title.rectTransform, Anchor.TopCenter, new Vector2(0, -50), new Vector2(900, 110));

        RectTransform row = Child(safe, "CardRow") ?? NewRect("CardRow", safe);
        Place(row, Anchor.Center, new Vector2(0, 50), new Vector2(1600, 420));
        HorizontalLayoutGroup h = Ensure<HorizontalLayoutGroup>(row);
        h.spacing = 36; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        Button card = MakeButton(row, "CardTemplate", "1\nLevel\nBest 0", Steel, Color.white, 44, Vector2.zero, new Vector2(340, 400));
        LayoutElement le = Ensure<LayoutElement>(card);
        le.preferredWidth = 340; le.preferredHeight = 400;
        ColorBlock cb = card.colors; cb.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.75f); card.colors = cb;
        TextMeshProUGUI cardText = card.GetComponentInChildren<TextMeshProUGUI>(true);
        cardText.textWrappingMode = TextWrappingModes.Normal;
        cardText.rectTransform.offsetMin = new Vector2(16, 16); cardText.rectTransform.offsetMax = new Vector2(-16, -16);
        card.gameObject.SetActive(false);

        TextMeshProUGUI total = MakeText(safe, "TotalBest", "Total best score: 0", 42, FontStyles.Bold, Amber);
        Place(total.rectTransform, Anchor.Center, new Vector2(0, -215), new Vector2(1200, 70));

        Button reset = MakeButton(safe, "Btn_ResetProgress", "RESET PROGRESS", Coral, Color.white, 40, Vector2.zero, Vector2.zero);
        Place(reset.transform as RectTransform, Anchor.BottomCenter, new Vector2(-280, 50), new Vector2(480, 130));
        Button back = MakeButton(safe, "Btn_Back", "BACK", Amber, Ink, 48, Vector2.zero, Vector2.zero);
        Place(back.transform as RectTransform, Anchor.BottomCenter, new Vector2(280, 50), new Vector2(480, 130));

        // растау терезесі (жүйелік диалог емес — ойынның өз панелі)
        RectTransform confirm = Child(panel, "ConfirmResetPanel") ?? NewRect("ConfirmResetPanel", panel);
        Stretch(confirm);
        confirm.SetAsLastSibling();
        Image dim = Ensure<Image>(confirm); dim.sprite = null; dim.color = Dim; dim.raycastTarget = true;   // артындағы батырмаларды бөгейді
        RectTransform box = Child(confirm, "Box") ?? NewRect("Box", confirm);
        Place(box, Anchor.Center, Vector2.zero, new Vector2(1100, 560));
        Image boxImg = Ensure<Image>(box); boxImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        boxImg.type = Image.Type.Sliced; boxImg.color = new Color(NavySolid.r, NavySolid.g, NavySolid.b, 1f);
        TextMeshProUGUI q = MakeText(box, "Question",
            "Reset all progress?\n<size=70%>Unlocked levels and best scores will be deleted.\nYour settings stay.</size>",
            56, FontStyles.Bold, Color.white);
        Place(q.rectTransform, Anchor.Center, new Vector2(0, 90), new Vector2(1000, 280));
        q.textWrappingMode = TextWrappingModes.Normal;
        Button yes = MakeButton(box, "Btn_Yes", "YES, RESET", Coral, Color.white, 44, new Vector2(-250, -170), new Vector2(440, 130));
        Button no = MakeButton(box, "Btn_No", "CANCEL", Steel, Color.white, 44, new Vector2(250, -170), new Vector2(440, 130));
        confirm.gameObject.SetActive(false);

        // сілтемелер мен батырмалар
        var so = new SerializedObject(select);
        so.FindProperty("mainMenuPanel").objectReferenceValue = menu.mainMenuPanel;
        so.FindProperty("cardRow").objectReferenceValue = row;
        so.FindProperty("cardTemplate").objectReferenceValue = card;
        so.FindProperty("totalText").objectReferenceValue = total;
        so.FindProperty("confirmPanel").objectReferenceValue = confirm.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        Wire(reset, select.AskReset);
        Wire(back, select.Close);
        Wire(yes, select.ConfirmReset);
        Wire(no, select.CancelReset);

        var mso = new SerializedObject(menu);
        mso.FindProperty("levelSelect").objectReferenceValue = select;
        mso.FindProperty("saveStatusText").objectReferenceValue = status;
        mso.ApplyModifiedPropertiesWithoutUndo();

        if (profile != null)
        {
            RectTransform slider = FindDeep(menu.settingsPanel.transform, "Slider");
            if (slider != null) SetRef(profile, "volumeSlider", slider.GetComponent<Slider>());
        }

        panel.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
