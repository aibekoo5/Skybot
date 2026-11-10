using System.Collections.Generic;
using UnityEditor.Animations;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools → Sky Bot → Build Outpost Level (Assignment 10)
// 1) Үш жау префабын жасайды: Rifleman (Swat+мылтық), MachineGun (пулемет), Brawler (Swat+бокс) (+ оқ)
// 2) Level 2-ні негізге алып (Player, HUD, камера, пауза) жаңа Level2_Outpost сахнасын құрады:
//    үш аймақ + финал, қабырғалар (көру сызығын бөгейді), NavMeshSurface
// 3) Build Settings-те Level 2 мен Boss-тың арасына қояды
// Қайта іске қосуға болады — префабтар мен сахна қайта құрылады.
public static class OutpostLevelBuilder
{
    const string ScenePath = "Assets/Scenes/Level2_Outpost.unity";
    const string SourceScene = "Assets/Scenes/Level 2.unity";
    const string PrefabDir = "Assets/Prefabs/Enemies";
    const string MatDir = "Assets/Materials/Outpost";

    static Material mGround, mWall, mCover, mDark, mOlive, mSand, mVisor, mLaser, mBolt, mDoor;
    const string SwatPath = "Assets/Models/Swat/Swat.fbx";
    const string AnimDir = "Assets/Animations/Enemies";

    [MenuItem("Tools/Sky Bot/Build Outpost Level (Assignment 10)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Folder("Assets/Prefabs", "Enemies");
        Folder("Assets/Materials", "Outpost");
        Materials();

        CharacterPacks.ImportBoxing();
        foreach (string old in new[] { "Patroller", "Shooter", "Charger" })   // бірінші нұсқаның префабтары
            AssetDatabase.DeleteAsset($"{PrefabDir}/{old}.prefab");

        var projectile = BuildProjectile();
        var rifleman = BuildRifleman(projectile, RiflemanController());
        var turret = BuildTurret(projectile);
        var brawler = BuildBrawler(BrawlerController());

        BuildScene(rifleman, turret, brawler);
        AddToBuild();

        Debug.Log("[Outpost] Дайын: Level2_Outpost құрылды (3 жау түрі, NavMesh).");
    }

    // ================= материалдар =================

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    static Material Mat(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emission = null, bool unlit = false)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        if (!unlit)
        {
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            // ескерту (telegraph) жарқылы үшін emission қосулы тұрады
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission ?? Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Materials()
    {
        mGround = Mat("Ground", new Color(0.58f, 0.55f, 0.48f));
        mWall = Mat("Wall", new Color(0.42f, 0.44f, 0.47f));
        mCover = Mat("Cover", new Color(0.30f, 0.33f, 0.36f));
        mDoor = Mat("Door", new Color(0.75f, 0.55f, 0.15f), 0.5f, 0.6f);
        mDark = Mat("Metal", new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.7f);
        mOlive = Mat("Olive", new Color(0.30f, 0.34f, 0.20f), 0.35f, 0.4f);
        mSand = Mat("Sandbag", new Color(0.62f, 0.55f, 0.40f), 0.1f);
        mVisor = Mat("Visor", new Color(0.05f, 0.05f, 0.06f), 0.9f, 0f, new Color(0.2f, 1.2f, 1.6f));
        mLaser = Mat("Laser", new Color(1f, 0.1f, 0.05f, 1f), unlit: true);
        mBolt = Mat("Bolt", new Color(1f, 0.5f, 0.1f), 0.5f, 0f, new Color(3f, 1.2f, 0.2f));
    }

    // ================= бөлшектер =================

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
                           Material mat, Vector3 euler = default, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Transform Empty(string name, Transform parent, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static TextMeshPro Label(Transform parent, float y)
    {
        var go = new GameObject("StateLabel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, y, 0);
        var t = go.AddComponent<TextMeshPro>();
        t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = 3f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.outlineWidth = 0.2f;
        t.outlineColor = Color.black;
        t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        t.text = "State";
        return t;
    }

    static LineRenderer Line(Transform parent, string name, float width, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = width;
        lr.sharedMaterial = mLaser;
        lr.startColor = lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    static GameObject Root(string name, float radius, float height, float speed)
    {
        var root = new GameObject(name);
        var col = root.AddComponent<CapsuleCollider>();
        col.radius = radius;
        col.height = height;
        col.center = new Vector3(0, height * 0.5f, 0);
        var ag = root.AddComponent<NavMeshAgent>();
        ag.radius = radius;
        ag.height = height;
        ag.speed = speed;
        ag.angularSpeed = 360f;
        ag.acceleration = 14f;
        ag.stoppingDistance = 0.2f;
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 6f;
        src.maxDistance = 45f;
        return root;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject Save(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ================= префабтар =================

    static GameObject BuildProjectile()
    {
        var root = Prim(PrimitiveType.Sphere, "EnemyBolt", null, Vector3.zero, new Vector3(0.09f, 0.09f, 0.22f), mBolt);   // кішкентай, ұзынша оқ
        root.AddComponent<EnemyProjectile>();
        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.color = new Color(1f, 0.6f, 0.2f);
        light.range = 1.5f;
        light.intensity = 1.5f;
        return Save(root, "EnemyBolt");
    }

    static void SetRendererArray(Object target, Renderer[] rs)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty("bodyRenderers");
        p.arraySize = rs.Length;
        for (int i = 0; i < rs.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static readonly Color RedTint = new Color(1f, 0.45f, 0.4f);   // Rifleman мен Brawler — қызыл скин

    static AudioClip Sfx(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Enemies/{name}.wav");

    static AnimationClip Clip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }
    static AnimationClip Swat(string name) => Clip($"Assets/Models/Swat/{name}.fbx");

    static AnimatorStateTransition To(AnimatorState from, AnimatorState to, string param, bool value, float dur = 0.15f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = dur;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        return t;
    }

    static AnimatorController NewController(string name, out AnimatorStateMachine sm)
    {
        Folder("Assets/Animations", "Enemies");
        string path = $"{AnimDir}/{name}.controller";
        AssetDatabase.DeleteAsset(path);
        var c = AnimatorController.CreateAnimatorControllerAtPath(path);
        sm = c.layers[0].stateMachine;
        return c;
    }

    static void AddDeath(AnimatorController c, AnimatorStateMachine sm)
    {
        c.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var die = sm.AddState("Die");
        die.motion = Swat("walking to dying");
        var t = sm.AddAnyStateTransition(die);
        t.AddCondition(AnimatorConditionMode.If, 0, "Die");
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
    }

    // Rifleman: Locomotion (тұру/жүру/жүгіру) ⇄ Aim, Fire (оқ) → Aim, Die
    static AnimatorController RiflemanController()
    {
        var c = NewController("RiflemanAnimator", out var sm);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
        c.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

        var loco = c.CreateBlendTreeInController("Locomotion", out BlendTree bt, 0);
        bt.blendParameter = "Speed";
        bt.useAutomaticThresholds = false;
        bt.AddChild(Swat("rifle aiming idle"), 0f);
        bt.AddChild(Swat("walking"), 1.6f);
        bt.AddChild(Swat("rifle run"), 4f);
        sm.defaultState = loco;

        var aim = sm.AddState("Aim");
        aim.motion = Swat("rifle aiming idle");
        var fire = sm.AddState("Fire");
        fire.motion = Swat("firing rifle");

        To(loco, aim, "Aiming", true);
        To(aim, loco, "Aiming", false);
        To(fire, loco, "Aiming", false);
        var f = aim.AddTransition(fire);
        f.hasExitTime = false; f.duration = 0.05f;
        f.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        var back = fire.AddTransition(aim);
        back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;

        AddDeath(c, sm);
        EditorUtility.SetDirty(c);
        return c;
    }

    // Brawler: Locomotion (тұру/жүгіру — мылтықсыз) ⇄ Boxing, Die
    static AnimatorController BrawlerController()
    {
        var c = NewController("BrawlerAnimator", out var sm);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Punching", AnimatorControllerParameterType.Bool);

        var loco = c.CreateBlendTreeInController("Locomotion", out BlendTree bt, 0);
        bt.blendParameter = "Speed";
        bt.useAutomaticThresholds = false;
        bt.AddChild(Clip("Assets/Models/X Bot@Idle.fbx"), 0f);
        bt.AddChild(Clip("Assets/Models/X Bot@Running.fbx"), 6.5f);
        sm.defaultState = loco;

        var box = sm.AddState("Boxing");
        box.motion = Clip(CharacterPacks.BoxingPath);
        box.speed = 1.3f;
        To(loco, box, "Punching", true, 0.08f);
        To(box, loco, "Punching", false, 0.2f);

        AddDeath(c, sm);
        EditorUtility.SetDirty(c);
        return c;
    }

    // Swat моделі + Animator; түбірінде collider, NavMeshAgent
    static GameObject Humanoid(string name, float speed, AnimatorController ctrl, out Animator anim)
    {
        var root = Root(name, 0.35f, 1.8f, speed);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwatPath), root.transform);
        model.name = "Model";
        anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(SwatPath).OfType<Avatar>().FirstOrDefault();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        return root;
    }

    // RIFLEMAN — Swat + мылтық: жүреді, қуады, көздеп атады
    static GameObject BuildRifleman(GameObject projectile, AnimatorController ctrl)
    {
        var root = Humanoid("Rifleman", 1.6f, ctrl, out Animator anim);
        var t = root.transform;
        var riflePf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rifle.prefab");
        Transform muzzle = null;
        if (riflePf != null)
        {
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(riflePf, t);
            rifle.name = "Rifle";
            var hold = rifle.GetComponent<GunHold>();
            hold.body = anim;
            muzzle = rifle.transform.Find("Muzzle");
        }
        var eye = Empty("Eye", t, new Vector3(0, 1.6f, 0.2f));
        var laser = Line(t, "Laser", 0.02f, Color.red);
        var label = Label(t, 2.2f);

        var e = root.AddComponent<Rifleman>();
        Set(e, "anim", anim);
        Set(e, "eye", eye);
        Set(e, "muzzle", muzzle);
        Set(e, "laser", laser);
        Set(e, "stateLabel", label);
        Set(e, "projectilePrefab", projectile.GetComponent<EnemyProjectile>());
        SetRendererArray(e, anim.GetComponentsInChildren<SkinnedMeshRenderer>());
        Set(e, "shotClip", Sfx("rifle_shot"));
        var so = new SerializedObject(e);
        so.FindProperty("bodyTint").colorValue = RedTint;   // Brawler сияқты қызыл
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "Rifleman");
    }

    // BRAWLER — Swat, мылтықсыз, бокс: өте жылдам жүгіреді, жақыннан ұрады
    static GameObject BuildBrawler(AnimatorController ctrl)
    {
        var root = Humanoid("Brawler", 6.5f, ctrl, out Animator anim);
        var t = root.transform;
        var ag = root.GetComponent<NavMeshAgent>();
        ag.acceleration = 30f;
        ag.stoppingDistance = 1.2f;
        var eye = Empty("Eye", t, new Vector3(0, 1.6f, 0.2f));
        var label = Label(t, 2.2f);

        var e = root.AddComponent<Brawler>();
        Set(e, "anim", anim);
        Set(e, "eye", eye);
        Set(e, "stateLabel", label);
        SetRendererArray(e, anim.GetComponentsInChildren<SkinnedMeshRenderer>());
        var so = new SerializedObject(e);
        so.FindProperty("bodyTint").colorValue = RedTint;
        so.FindProperty("shoutClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Enemies/surprised-male-scream.mp3");
        so.FindProperty("maxHp").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "Brawler");
    }

    // MACHINE GUN — штатив, қапшықтар, айналмалы үш ұңғы, қалқан. NavMeshAgent ЖОҚ — қозғалмайды.
    static GameObject BuildTurret(GameObject projectile)
    {
        var root = new GameObject("MachineGun");
        var col = root.AddComponent<CapsuleCollider>();
        col.radius = 0.55f; col.height = 1.6f; col.center = new Vector3(0, 0.8f, 0);
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f; src.minDistance = 6f; src.maxDistance = 45f;
        var t = root.transform;

        // қапшықтар (жартылай шеңбер, алдында) — тек көрініс, collider жоқ
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Lerp(-80f, 80f, i / 6f);
            Vector3 p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.22f, 1.15f);
            Prim(PrimitiveType.Capsule, $"Sandbag {i}", t, p, new Vector3(0.45f, 0.42f, 0.45f), mSand, new Vector3(0, a, 90));
        }
        // штатив
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Vector3 p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.45f, 0.22f);
            Prim(PrimitiveType.Cylinder, $"Leg {i}", t, p, new Vector3(0.07f, 0.48f, 0.07f), mDark, new Vector3(25, a, 0));
        }
        Prim(PrimitiveType.Cylinder, "Post", t, new Vector3(0, 0.85f, 0), new Vector3(0.12f, 0.15f, 0.12f), mDark);

        // бұрылатын бас
        var yaw = Empty("YawPivot", t, new Vector3(0, 1.05f, 0));
        var body = Prim(PrimitiveType.Cube, "GunBody", yaw, new Vector3(0, 0, 0.05f), new Vector3(0.32f, 0.3f, 0.75f), mOlive);
        var ammo = Prim(PrimitiveType.Cube, "AmmoBox", yaw, new Vector3(-0.3f, -0.05f, 0f), new Vector3(0.25f, 0.28f, 0.35f), mOlive);
        Prim(PrimitiveType.Cube, "Belt", yaw, new Vector3(-0.17f, 0.02f, 0.12f), new Vector3(0.08f, 0.04f, 0.25f), mBolt);
        var shield = Prim(PrimitiveType.Cube, "Shield", yaw, new Vector3(0, 0.05f, 0.42f), new Vector3(0.95f, 0.6f, 0.05f), mOlive);
        Prim(PrimitiveType.Cube, "Handle", yaw, new Vector3(0, 0.05f, -0.4f), new Vector3(0.3f, 0.08f, 0.08f), mDark);

        // ұңғылар: айналатын топ (Z осі бойымен)
        var barrels = Empty("Barrels", yaw, new Vector3(0, 0.02f, 0.45f));
        Renderer shroud = Prim(PrimitiveType.Cylinder, "Shroud", barrels, new Vector3(0, 0, 0.12f), new Vector3(0.2f, 0.14f, 0.2f), mDark, new Vector3(90, 0, 0)).GetComponent<Renderer>();
        for (int i = 0; i < 3; i++)
        {
            Vector3 off = Quaternion.Euler(0, 0, i * 120f) * new Vector3(0, 0.055f, 0);
            Prim(PrimitiveType.Cylinder, $"Barrel {i}", barrels, off + new Vector3(0, 0, 0.42f), new Vector3(0.045f, 0.4f, 0.045f), mDark, new Vector3(90, 0, 0));
        }
        var muzzle = Empty("Muzzle", yaw, new Vector3(0, 0.02f, 1.3f));
        var eye = Empty("Eye", t, new Vector3(0, 1.25f, 0.3f));
        var laser = Line(t, "Laser", 0.03f, Color.red);
        var label = Label(t, 2.1f);

        var e = root.AddComponent<MachineGunTurret>();
        Set(e, "yawPivot", yaw);
        Set(e, "barrels", barrels);
        Set(e, "muzzle", muzzle);
        Set(e, "laser", laser);
        Set(e, "barrelRenderer", shroud);
        Set(e, "eye", eye);
        Set(e, "stateLabel", label);
        Set(e, "projectilePrefab", projectile.GetComponent<EnemyProjectile>());
        SetRendererArray(e, new[] { body.GetComponent<Renderer>(), ammo.GetComponent<Renderer>(), shield.GetComponent<Renderer>() });
        var so = new SerializedObject(e);
        so.FindProperty("maxHp").intValue = 4;
        so.FindProperty("shotClip").objectReferenceValue = Sfx("mg_shot");
        so.FindProperty("viewAngle").floatValue = 140f;   // бас бұрыла алатын сектор
        so.FindProperty("viewDistance").floatValue = 16f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "MachineGun");
    }

    // ================= сахна =================

    static Transform outpost;

    static GameObject Block(string name, Vector3 center, Vector3 size, Material mat, bool ground = false)
    {
        var go = Prim(PrimitiveType.Cube, name, outpost, center, size, mat, default, keepCollider: true);
        go.isStatic = true;
        if (ground) { go.layer = 3; go.tag = "Ground"; }   // Movement секіру үшін Ground layer
        return go;
    }

    // Y — жоғарғы бетінің биіктігі; блок жерден басталады
    static GameObject Wall(string name, float x, float z, float sx, float sz, float h, Material mat = null)
        => Block(name, new Vector3(x, h * 0.5f, z), new Vector3(sx, h, sz), mat ?? mWall);

    static void BuildScene(GameObject riflemanPf, GameObject turretPf, GameObject brawlerPf)
    {
        // Level 2-нің көшірмесі: Player, Canvas (HUD, пауза), камера, жарық сақталады
        var src = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(src, ScenePath, true);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // монетаның үлгісі
        GameObject coinTemplate = null;
        var coinsRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Coins");
        if (coinsRoot != null && coinsRoot.transform.childCount > 0)
        {
            coinTemplate = Object.Instantiate(coinsRoot.transform.GetChild(0).gameObject);
            coinTemplate.name = "CoinTemplate";
        }

        // Level 2-нің картасын өшіреміз
        string[] remove = { "Spawner", "Coins", "ExitGate", "Level2_Map", "Enemys", "Level2_Hazards", "PlatformRoot", "Heart", "Outpost", "Enemies", "Pickups", "EnemyGate" };
        foreach (var g in scene.GetRootGameObjects())
            if (remove.Contains(g.name)) Object.DestroyImmediate(g);

        outpost = new GameObject("Outpost").transform;

        // ---------- еден мен сыртқы қабырғалар ----------
        // ені x: −14..14, ұзындығы z: −4..62
        Block("Floor", new Vector3(0, -0.5f, 29f), new Vector3(28f, 1f, 66f), mGround, ground: true);
        Wall("Wall W", -14.5f, 29f, 1f, 66f, 3.5f);
        Wall("Wall E", 14.5f, 29f, 1f, 66f, 3.5f);
        Wall("Wall S", 0f, -4.5f, 30f, 1f, 3.5f);
        Wall("Wall N", 0f, 62.5f, 30f, 1f, 3.5f);

        // аймақтарды бөлетін қабырғалар (ортасында 4 м өткел) — бір аймақтың жауы келесісін көрмейді
        foreach (float z in new[] { 20f, 38f })
        {
            Wall($"Divider {z} L", -8.5f, z, 11f, 1f, 3f);
            Wall($"Divider {z} R", 8.5f, z, 11f, 1f, 3f);
        }

        // ---------- 1-аймақ (z 0..20): Rifleman — үйрену ----------
        Wall("Cover A1", -4f, 10f, 2f, 2f, 2.4f, mCover);
        Wall("Cover A2", 4.5f, 14f, 2f, 2f, 2.4f, mCover);
        Wall("Cover A3", -8f, 16f, 3f, 1f, 2.4f, mCover);

        // ---------- 2-аймақ (z 20..38): пулемет + Brawler — жасырыну, қашу ----------
        Block("Gun Deck", new Vector3(-7f, 0.5f, 34f), new Vector3(6f, 1f, 5f), mCover, ground: true);   // биіктігі 1 м — секіріп шығуға болады
        Wall("Cover B1", -3f, 25f, 3f, 1f, 2.2f, mCover);
        Wall("Cover B2", 3.5f, 28f, 3f, 1f, 2.2f, mCover);
        Wall("Cover B3", -2f, 31.5f, 1f, 3f, 2.2f, mCover);
        Wall("Pillar B4", 8f, 33f, 1.5f, 1.5f, 3f, mCover);

        // ---------- 3-аймақ (z 38..62): үш түрі бірге — финал ----------
        Wall("Pillar C1", -5f, 44f, 1.5f, 1.5f, 3f, mCover);
        Wall("Pillar C2", 5f, 44f, 1.5f, 1.5f, 3f, mCover);
        Wall("Pillar C3", -5f, 52f, 1.5f, 1.5f, 3f, mCover);
        Wall("Pillar C4", 5f, 52f, 1.5f, 1.5f, 3f, mCover);
        Block("Gate Tower", new Vector3(8.5f, 0.5f, 57f), new Vector3(5f, 1f, 4f), mCover, ground: true);
        // шығыс қабырғасы: ортасында 4 м қақпа (Door), айналып өтуге болмайды
        Wall("Gate Wall L", -8f, 59f, 12f, 1f, 3.5f);
        Wall("Gate Wall R", 8f, 59f, 12f, 1f, 3.5f);

        // ---------- NavMesh ----------
        var surface = outpost.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.BuildNavMesh();
        string navPath = "Assets/Scenes/Level2_Outpost_NavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        // ---------- жаулар ----------
        var enemies = new GameObject("Enemies").transform;
        var wpRoot = new GameObject("Waypoints").transform;

        // 1-аймақ: Rifleman — үйрену (жүреді, көрсе атады, қашсаң қуады)
        Place(riflemanPf, enemies, "Rifleman A", new Vector3(0f, 0, 12f), 90f,   // бүйірімен тұрады — бірден көрмейді
              Waypoints(wpRoot, "A", new Vector3(-6, 0, 7), new Vector3(6, 0, 7), new Vector3(6, 0, 17), new Vector3(-1, 0, 17)));

        // 2-аймақ: пулемет (платформада, қозғалмайды) + Brawler (жылдам, жақыннан ұрады)
        Place(turretPf, enemies, "MachineGun A", new Vector3(-7f, 1f, 35f), 160f);
        Place(brawlerPf, enemies, "Brawler A", new Vector3(7f, 0, 30f), 200f,
              Waypoints(wpRoot, "B", new Vector3(8, 0, 25), new Vector3(9, 0, 35)));

        // 3-аймақ — үш түрі бірге
        Place(brawlerPf, enemies, "Brawler B", new Vector3(-6f, 0, 48f), 160f);
        Place(turretPf, enemies, "MachineGun B", new Vector3(8.5f, 1f, 57.5f), 210f);
        Place(riflemanPf, enemies, "Rifleman B", new Vector3(-8f, 0, 55f), 150f,
              Waypoints(wpRoot, "C", new Vector3(-10, 0, 56), new Vector3(-10, 0, 46), new Vector3(-2, 0, 56)));

        // ---------- монеталар мен жүректер ----------
        var pickups = new GameObject("Pickups").transform;
        if (coinTemplate != null)
        {
            var coins = new GameObject("Coins").transform;
            coins.SetParent(pickups, false);
            foreach (var p in new[] { new Vector3(-8f, 0.8f, 12f), new Vector3(-7f, 1.8f, 33f), new Vector3(0f, 0.8f, 44f) })
            {
                var c = Object.Instantiate(coinTemplate, coins);
                c.name = "Coin";
                c.transform.position = p;
            }
            Object.DestroyImmediate(coinTemplate);
        }
        var heartPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Heart 1.prefab");
        if (heartPf != null)
            foreach (var p in new[] { new Vector3(0f, 0.8f, 21f), new Vector3(10f, 0.8f, 40f) })
            {
                var h = (GameObject)PrefabUtility.InstantiatePrefab(heartPf, pickups);
                h.transform.position = p;
            }

        // ---------- қақпа мен шығыс ----------
        var gate = new GameObject("EnemyGate");
        var door = Prim(PrimitiveType.Cube, "Door", gate.transform, new Vector3(0, 1.75f, 59f), new Vector3(4f, 3.5f, 0.6f), mDoor, default, keepCollider: true);
        var exit = Prim(PrimitiveType.Cube, "Exit", gate.transform, new Vector3(0, 1.5f, 61f), new Vector3(4f, 3f, 1.5f), mVisor, default, keepCollider: true);
        exit.tag = "Exit";
        exit.GetComponent<Collider>().isTrigger = true;

        // ---------- ойыншы ----------
        var mv = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Include);
        var eg = gate.AddComponent<EnemyGate>();
        Set(eg, "door", door);
        Set(eg, "exitTrigger", exit);
        if (mv != null)
        {
            mv.transform.SetPositionAndRotation(new Vector3(0, 0.05f, 0f), Quaternion.identity);
            var so = new SerializedObject(mv);
            so.FindProperty("timeLimit").floatValue = 0f;               // таймер жоқ — оның орнына жау санағышы
            so.FindProperty("gateAnimator").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(eg, "counterText", mv.timerText);
            Set(eg, "messageText", mv.winText);
        }
        var camGo = Camera.main;
        if (camGo != null) camGo.transform.SetPositionAndRotation(new Vector3(0.6f, 2.2f, -3.5f), Quaternion.Euler(8f, 0, 0));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Outpost] {ScenePath}: сақталды");
    }

    static Transform[] Waypoints(Transform root, string id, params Vector3[] pts)
    {
        var list = new List<Transform>();
        for (int i = 0; i < pts.Length; i++)
        {
            var t = new GameObject($"WP {id}{i + 1}").transform;
            t.SetParent(root, false);
            t.position = pts[i];
            list.Add(t);
        }
        return list.ToArray();
    }

    static void Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw, Transform[] waypoints = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        if (waypoints != null)
        {
            var e = go.GetComponent<EnemyBase>();
            var so = new SerializedObject(e);
            var p = so.FindProperty("waypoints");
            p.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void AddToBuild()
    {
        var list = EditorBuildSettings.scenes.ToList();
        list.RemoveAll(s => s.path == ScenePath);
        int i = list.FindIndex(s => s.path == "Assets/Scenes/Main menu.unity");   // мәзірден кейін — бірінші деңгей
        list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
