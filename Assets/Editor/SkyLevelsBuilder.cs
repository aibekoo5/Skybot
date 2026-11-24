using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools → Cloud Strike → Build Sky Levels (қайта іске қосуға болады)
//  1) Сахналарды ойындағы ретімен атайды:
//       Level2_Outpost → Level1_Outpost, Level1 → Level2_SkyIslands,
//       Level 2 → Level3_SpikeBridge, Level3_Boss → Level4_BossArena
//     (MoveAsset — GUID сақталады; LevelData.sceneName мен Build Settings жаңарады)
//  2) Outpost: жердегі қабырғалы аула → аспандағы үш арал + көпірлер
//     (аймақтар, жабулар, жаулардың орны бұрынғыдай — Level_01 өзгермейді)
//  3) Қалған деңгейлер: аралдарға NavMesh (тек Ground layer), LevelData-ға платформалардағы жаулар
//     (тізім бос болса ғана — Enemy Preview-мен өзгерткенің өшпейді)
public static class SkyLevelsBuilder
{
    public const string L1 = "Assets/Scenes/Level1_Outpost.unity";
    public const string L2 = "Assets/Scenes/Level2_SkyIslands.unity";
    public const string L3 = "Assets/Scenes/Level3_SpikeBridge.unity";
    public const string L4 = "Assets/Scenes/Level4_BossArena.unity";
    const string LevelsDir = "Assets/Data/Levels";
    const string MatDir = "Assets/Materials/Sky";
    const int GroundLayer = 3;

    static readonly (string from, string to)[] Renames =
    {
        ("Assets/Scenes/Level2_Outpost.unity", L1),
        ("Assets/Scenes/Level1.unity", L2),
        ("Assets/Scenes/Level 2.unity", L3),
        ("Assets/Scenes/Level3_Boss.unity", L4),
        ("Assets/Scenes/Level2_Outpost_NavMesh.asset", "Assets/Scenes/Level1_Outpost_NavMesh.asset"),
    };

    [MenuItem("Tools/Cloud Strike/Build Sky Levels")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run(forceEnemies: false);
    }

    [MenuItem("Tools/Cloud Strike/Build Sky Levels (reset enemies 2-4)")]
    public static void RunForce()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run(forceEnemies: true);
    }

    public static void RunBatch()
    {
        try { Run(forceEnemies: false); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Run(bool forceEnemies)
    {
        RenameScenes();
        Materials();

        BuildOutpost();
        PrepareLevel(L2, "Level_02", forceEnemies, SkyIslandsEnemies);
        PrepareLevel(L3, "Level_03", forceEnemies, SpikeBridgeEnemies);
        PrepareLevel(L4, "Level_04", forceEnemies, BossArenaEnemies);

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(L1);
        Debug.Log("[Sky] Sky levels built");
    }

    // =====================================================================
    // 1) атаулар
    // =====================================================================
    static void RenameScenes()
    {
        foreach (var (from, to) in Renames)
        {
            if (!System.IO.File.Exists(from) || System.IO.File.Exists(to)) continue;
            string err = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(err)) throw new System.Exception($"[Sky] rename {from}: {err}");
            Debug.Log($"[Sky] renamed {from} → {to}");
        }

        SetScene("Level_01", L1);
        SetScene("Level_02", L2);
        SetScene("Level_03", L3);
        SetScene("Level_04", L4);

        // Build Settings: мәзір, 1..4 деңгей, Win
        var order = new[] { "Assets/Scenes/Main menu.unity", L1, L2, L3, L4, "Assets/Scenes/Win.unity" };
        EditorBuildSettings.scenes = order.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
    }

    static LevelData Level(string file) => AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsDir}/{file}.asset");

    static void SetScene(string file, string scenePath)
    {
        LevelData d = Level(file);
        d.sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        EditorUtility.SetDirty(d);
    }

    // =====================================================================
    // материалдар
    // =====================================================================
    static Material mTop, mRock, mBridge, mWall, mCover;

    static Material Mat(string name, Color c, float smooth)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Sky");
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Materials()
    {
        mTop = Mat("IslandTop", new Color(0.47f, 0.60f, 0.38f), 0.15f);    // шөпті тас
        mRock = Mat("IslandRock", new Color(0.40f, 0.34f, 0.29f), 0.1f);   // аралдың астыңғы жағы
        mBridge = Mat("Bridge", new Color(0.55f, 0.40f, 0.25f), 0.2f);     // ағаш көпір
        mWall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Outpost/Wall.mat");
        mCover = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Outpost/Cover.mat");
    }

    // =====================================================================
    // 2) Outpost — бұлттардың үстіндегі аралдар
    // =====================================================================
    static Transform root;

    static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, bool ground = false, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true;
        if (ground) { go.layer = GroundLayer; go.tag = "Ground"; }
        return go;
    }

    // Y = 0 — аралдың беті. Биіктігі h қабырға жерден басталады
    static void Wall(string name, float x0, float x1, float z0, float z1, float h, Material mat = null)
        => Box(name, new Vector3((x0 + x1) * 0.5f, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, h, z1 - z0), mat ?? mWall);

    // аралдың беті (жүруге болады) + астындағы кішірейетін тас қабаттар (тек көрініс, коллайдерсіз)
    static void Island(string name, float x0, float x1, float z0, float z1)
    {
        float w = x1 - x0, d = z1 - z0;
        Vector3 c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
        Box(name, c + Vector3.down * 0.5f, new Vector3(w, 1f, d), mTop, ground: true);
        Box(name + " rock 1", c + Vector3.down * 2f, new Vector3(w * 0.88f, 2f, d * 0.88f), mRock, collider: false);
        Box(name + " rock 2", c + Vector3.down * 4f, new Vector3(w * 0.62f, 2f, d * 0.62f), mRock, collider: false);
        Box(name + " rock 3", c + Vector3.down * 6.2f, new Vector3(w * 0.32f, 2.4f, d * 0.32f), mRock, collider: false);
    }

    static void BuildOutpost()
    {
        Scene scene = EditorSceneManager.OpenScene(L1);
        Materials();

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Outpost");
        if (old != null) Object.DestroyImmediate(old);
        foreach (GameObject g in scene.GetRootGameObjects().Where(g => g.name == "Clouds").ToArray())
            Object.DestroyImmediate(g);

        root = new GameObject("Outpost").transform;
        BuildOutpostIslands();

        // NavMesh: аралдар мен көпірлер (қабырғалар — кедергі). Бұлттарда коллайдер жоқ.
        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        Bake(surface, "Assets/Scenes/Level1_Outpost_NavMesh.asset");

        ApplySky();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Sky] Outpost is now on sky islands");
    }

    // Аймақтар бұрынғыдай (Level_01-дің жаулары сол орында қалады):
    //   A z −4..18 (Rifleman), B z 21.5..37.5 (пулемет + Brawler), C z 40.5..62 (үшеуі, қақпа)
    // OutpostLevelBuilder (week 10) да осыны шақырады — Outpost-тың геометриясы бір жерде
    public static void BuildOutpostIslandsUnder(Transform parent)
    {
        Materials();
        root = parent;
        BuildOutpostIslands();
    }

    static void BuildOutpostIslands()
    {
        Island("Island A", -11f, 11f, -4f, 18f);
        Island("Island B", -11f, 11f, 21.5f, 37.5f);
        Island("Island C", -11f, 11f, 40.5f, 62f);

        // көпірлер (ені 4 м) — бұрынғы қабырғадағы өткелдің орнында
        Box("Bridge AB", new Vector3(0f, -0.15f, 19.75f), new Vector3(4f, 0.3f, 3.6f), mBridge, ground: true);
        Box("Bridge BC", new Vector3(0f, -0.15f, 39f), new Vector3(4f, 0.3f, 3.1f), mBridge, ground: true);
        foreach (var (z, len) in new[] { (19.75f, 3.6f), (39f, 3.1f) })
            foreach (float x in new[] { -2.1f, 2.1f })
                Box($"Rope {z} {x}", new Vector3(x, 0.55f, z), new Vector3(0.12f, 0.12f, len), mBridge, collider: false);

        // аралдың шетіндегі бекініс қабырғалары: көпірдің екі жағында биік (бір аймақтың жауы
        // келесісін көрмейді — бұрынғы бөлгіш қабырғаның рөлі), қалған шеттерде аласа парапет
        const float H = 3f, P = 0.6f;
        Wall("Fort Wall A L", -11f, -2f, 17.4f, 18f, H); Wall("Fort Wall A R", 2f, 11f, 17.4f, 18f, H);
        Wall("Fort Wall B L", -11f, -2f, 36.9f, 37.5f, H); Wall("Fort Wall B R", 2f, 11f, 36.9f, 37.5f, H);
        // парапеттер
        Wall("Parapet A S", -11f, 11f, -4f, -3.6f, P);
        Wall("Parapet A W", -11f, -10.6f, -4f, 17.4f, P); Wall("Parapet A E", 10.6f, 11f, -4f, 17.4f, P);
        Wall("Parapet B S L", -11f, -2f, 21.5f, 21.9f, P); Wall("Parapet B S R", 2f, 11f, 21.5f, 21.9f, P);
        Wall("Parapet B W", -11f, -10.6f, 21.5f, 36.9f, P); Wall("Parapet B E", 10.6f, 11f, 21.5f, 36.9f, P);
        Wall("Parapet C S L", -11f, -2f, 40.5f, 40.9f, P); Wall("Parapet C S R", 2f, 11f, 40.5f, 40.9f, P);
        Wall("Parapet C W", -11f, -10.6f, 40.5f, 62f, P); Wall("Parapet C E", 10.6f, 11f, 40.5f, 62f, P);
        Wall("Parapet C N", -11f, 11f, 61.6f, 62f, P);

        // жабулар — бұрынғы орындарында
        Wall("Cover A1", -5f, -3f, 9f, 11f, 2.4f, mCover);
        Wall("Cover A2", 3.5f, 5.5f, 13f, 15f, 2.4f, mCover);
        Wall("Cover A3", -9.5f, -6.5f, 15.5f, 16.5f, 2.4f, mCover);
        Box("Gun Deck", new Vector3(-7f, 0.5f, 34f), new Vector3(6f, 1f, 5f), mCover, ground: true);
        Wall("Cover B1", -4.5f, -1.5f, 24.5f, 25.5f, 2.2f, mCover);
        Wall("Cover B2", 2f, 5f, 27.5f, 28.5f, 2.2f, mCover);
        Wall("Cover B3", -2.5f, -1.5f, 30f, 33f, 2.2f, mCover);
        Wall("Pillar B4", 7.25f, 8.75f, 32.25f, 33.75f, 3f, mCover);
        Wall("Pillar C1", -5.75f, -4.25f, 43.25f, 44.75f, 3f, mCover);
        Wall("Pillar C2", 4.25f, 5.75f, 43.25f, 44.75f, 3f, mCover);
        Wall("Pillar C3", -5.75f, -4.25f, 51.25f, 52.75f, 3f, mCover);
        Wall("Pillar C4", 4.25f, 5.75f, 51.25f, 52.75f, 3f, mCover);
        Box("Gate Tower", new Vector3(8.5f, 0.5f, 57f), new Vector3(5f, 1f, 4f), mCover, ground: true);
        // шығыс қабырғасы: ортасында 4 м қақпа (EnemyGate-тің Door-ы)
        Wall("Gate Wall L", -11f, -2f, 58.5f, 59.5f, 3.5f); Wall("Gate Wall R", 2f, 11f, 58.5f, 59.5f, 3.5f);
    }

    // =====================================================================
    // 3) қалған деңгейлер: NavMesh + платформалардағы жаулар
    // =====================================================================
    delegate void EnemyLayout(List<EnemySpawn> list);

    static void PrepareLevel(string scenePath, string levelFile, bool force, EnemyLayout layout)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath);

        // NavMesh тек Ground layer-дегі аралдар/платформалар бойынша — жау шеттен құламайды
        GameObject navGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "NavMesh");
        if (navGo == null) navGo = new GameObject("NavMesh");
        var surface = navGo.GetComponent<NavMeshSurface>();
        if (surface == null) surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << GroundLayer;
        Bake(surface, scenePath.Replace(".unity", "_NavMesh.asset"));

        // аспан — барлық деңгей бір стильде (Cloud Strike); бұрынғы бұлт объектілері болса — өшіреміз
        foreach (GameObject g in scene.GetRootGameObjects().Where(g => g.name == "Clouds").ToArray())
            Object.DestroyImmediate(g);
        ApplySky();

        LevelData data = Level(levelFile);
        if (force || data.enemies.Count == 0)
        {
            data.enemies.Clear();
            layout(data.enemies);
            EditorUtility.SetDirty(data);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Sky] {scenePath}: NavMesh baked, {data.enemies.Count} enemies in {levelFile}");
    }

    // процедуралық аспан: көкжиектен төмен қоңыр «жер» емес — ақшыл бұлт теңізі
    static void ApplySky()
    {
        string path = $"{MatDir}/SkyAboveClouds.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.9f));
        m.SetColor("_GroundColor", new Color(0.62f, 0.74f, 0.92f));
        m.SetFloat("_AtmosphereThickness", 0.9f);
        m.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(m);
        RenderSettings.skybox = m;
    }

    static void Bake(NavMeshSurface surface, string assetPath)
    {
        surface.RemoveData();
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        EditorUtility.SetDirty(surface);
    }

    static EnemyBase Pf(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab").GetComponent<EnemyBase>();

    static void Add(List<EnemySpawn> list, string prefab, Vector3 pos, float yaw, params Vector3[] patrol)
        => list.Add(new EnemySpawn { prefab = Pf(prefab), position = pos, yaw = yaw, patrolPoints = patrol });

    // 2. Sky Islands: ұзын жолақ (x −4.9..1.1, z −41..9), биік платформа мен тастар, соңында Plane 2 (x −41..−11)
    static void SkyIslandsEnemies(List<EnemySpawn> l)
    {
        Add(l, "Rifleman", new Vector3(-1.9f, 0f, -14f), 180f,
            new Vector3(-3.8f, 0f, -17f), new Vector3(0f, 0f, -17f), new Vector3(0f, 0f, -9f), new Vector3(-3.8f, 0f, -9f));
        // пулемет жолақтың соңында, ойыншыға қарап тұр; алдындағы тас (Cube 2) — жасырынатын жер
        Add(l, "MachineGun", new Vector3(-1.9f, 0f, 7.8f), 180f);
        Add(l, "Brawler", new Vector3(-18f, 0f, 18f), 90f);
        Add(l, "Rifleman", new Vector3(-30f, 0f, 18f), 90f, new Vector3(-34f, 0f, 18f), new Vector3(-22f, 0f, 18f));
    }

    // 3. Spike Bridge: Island 1 (z 9..17), Island 2 (z 29..35, y 2.5), Island 3 (x −16.5..−9.5), Final (z 48..58)
    static void SpikeBridgeEnemies(List<EnemySpawn> l)
    {
        Add(l, "Rifleman", new Vector3(1.5f, 0f, 13f), 180f,
            new Vector3(-2f, 0f, 10f), new Vector3(2f, 0f, 10f), new Vector3(2f, 0f, 16f), new Vector3(-2f, 0f, 16f));
        Add(l, "Brawler", new Vector3(0f, 2.5f, 33f), 180f);
        Add(l, "MachineGun", new Vector3(-15f, 2.5f, 29.5f), 60f);
        Add(l, "Rifleman", new Vector3(-13f, 2.5f, 52f), 180f,
            new Vector3(-16f, 2.5f, 50f), new Vector3(-10f, 2.5f, 50f), new Vector3(-10f, 2.5f, 55f), new Vector3(-16f, 2.5f, 55f));
    }

    // 4. Boss Arena: бастықтан бөлек екі бүйір платформада (биіктігі 1 м) күзет
    static void BossArenaEnemies(List<EnemySpawn> l)
    {
        Add(l, "MachineGun", new Vector3(-9f, 1f, -2f), 120f);
        Add(l, "Rifleman", new Vector3(9f, 1f, -2f), 240f);
    }
}
