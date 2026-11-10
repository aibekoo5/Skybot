using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools → Sky Bot → Add Rifle To Hero
// Қарапайым пішіндерден автомат құрастырады (Assets/Prefabs/Rifle.prefab),
// оны үш деңгейдегі кейіпкерге қосады және Shooter.muzzle-ды мылтықтың ұшына қояды.
// Қайта іске қосуға болады: ескі мылтық ауыстырылады.
public static class RifleBuilder
{
    const string PrefabPath = "Assets/Prefabs/Rifle.prefab";
    const string MatDir = "Assets/Materials";
    const string RifleName = "Rifle";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level 2.unity",
        "Assets/Scenes/Level3_Boss.unity",
    };

    [MenuItem("Tools/Sky Bot/Add Rifle To Hero")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        GameObject prefab = BuildPrefab();
        foreach (string s in Scenes)
            if (File.Exists(s)) AddToScene(s, prefab);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log("[Rifle] Дайын: мылтық кейіпкерге қосылды.");
    }

    static Material Mat(string name, Color color, float metallic, float smooth)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    // бөлшек: позиция, өлшем (метр), бұрылу — бәрі тұтқаға (оң қолға) қатысты; мылтық +Z бағытында
    static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 size,
                     Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // оқ пен кейіпкерге кедергі болмасын
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    static GameObject BuildPrefab()
    {
        Material metal = Mat("Rifle_Metal", new Color(0.13f, 0.13f, 0.14f), 0.7f, 0.55f);
        Material body = Mat("Rifle_Body", new Color(0.22f, 0.21f, 0.18f), 0.1f, 0.3f);

        var root = new GameObject(RifleName);
        Transform t = root.transform;

        // цилиндр Unity-де Y бойымен 2 бірлік → ұзындық = scale.y * 2, X 90° бұрып Z-ке жатқызамыз
        Part(t, "Grip",      PrimitiveType.Cube,     new Vector3(0f, -0.01f, 0f),    new Vector3(0.035f, 0.10f, 0.045f), new Vector3(15f, 0f, 0f), body);
        Part(t, "Receiver",  PrimitiveType.Cube,     new Vector3(0f, 0.06f, 0.08f),  new Vector3(0.05f, 0.08f, 0.36f),   Vector3.zero, metal);
        Part(t, "Stock",     PrimitiveType.Cube,     new Vector3(0f, 0.04f, -0.21f), new Vector3(0.045f, 0.09f, 0.24f),  Vector3.zero, body);
        Part(t, "Handguard", PrimitiveType.Cube,     new Vector3(0f, 0.055f, 0.38f), new Vector3(0.058f, 0.062f, 0.26f), Vector3.zero, body);
        Part(t, "Magazine",  PrimitiveType.Cube,     new Vector3(0f, -0.03f, 0.13f), new Vector3(0.035f, 0.14f, 0.06f),  new Vector3(12f, 0f, 0f), metal);
        Part(t, "Sight",     PrimitiveType.Cube,     new Vector3(0f, 0.115f, 0.06f), new Vector3(0.022f, 0.035f, 0.09f), Vector3.zero, metal);
        Part(t, "Barrel",    PrimitiveType.Cylinder, new Vector3(0f, 0.065f, 0.62f), new Vector3(0.024f, 0.14f, 0.024f), new Vector3(90f, 0f, 0f), metal);

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(t, false);
        muzzle.localPosition = new Vector3(0f, 0.065f, 0.78f);

        root.AddComponent<GunHold>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddToScene(string path, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var mv = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Include);
        if (mv == null) { Debug.LogWarning($"[Rifle] {path}: Movement табылмады"); return; }

        Transform player = mv.transform;
        Transform old = player.Find(RifleName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rifle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player);
        rifle.name = RifleName;
        // Player масштабталған болса да, мылтық нақты өлшемде болсын
        Vector3 ls = player.lossyScale;
        rifle.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);

        var hold = rifle.GetComponent<GunHold>();
        hold.body = mv.anim;
        EditorUtility.SetDirty(hold);

        var shooter = player.GetComponent<Shooter>();
        if (shooter != null)
        {
            shooter.muzzle = rifle.transform.Find("Muzzle");
            EditorUtility.SetDirty(shooter);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Rifle] {path}: мылтық қосылды");
    }
}
