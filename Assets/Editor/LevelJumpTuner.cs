using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools → Sky Bot → Lower Jump + Adapt Levels
// Секіру импульсі 6 → 5 (биіктік ~1.8 м → ~1.27 м, ұзындық жүгіргенде ~6 м).
// Бір секірумен шығатын биіктік ≤ 0.7 м, аралық ≤ 3 м болатындай платформалар төмендетілді.
// Main Camera-ға Crosshair қосылады. Қайта іске қосуға болады — мәндер жай қайта қойылады.
public static class LevelJumpTuner
{
    const float JumpImpulse = 5f;

    [MenuItem("Tools/Sky Bot/Lower Jump + Adapt Levels")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        Edit("Assets/Scenes/Level1.unity", () =>
        {
            // басындағы баспалдақ: жер 0 → 0.75 → 1.4 → 2.05 (бұрын 1.33 → 2.16 → 2.5, аралық 3.7 м)
            SetY("Cube", 0.5f);
            SetYZ("Cube (1)", 1.15f, -25.0f);
            SetYZ("Cube (3)", 1.8f, -21.9f);
            SetY("Coins/Coin  (4)", 1.35f);
            SetYZ("Coins/Coin  (3)", 2.65f, -21.9f);

            // жылжымалы платформа 2.15 → 1.8 (рампаның басынан +0.5 м)
            SetLocalY("PlatformRoot", -0.35f);

            // платформадан кейінгі текшелер 2.5 → 2.15
            SetY("Cube (4)", 1.9f);
            SetY("Cube (5)", 1.9f);
            SetY("Cube (6)", 1.9f);
            SetY("Coins/Coin  (2)", 2.71f);
        });

        Edit("Assets/Scenes/Level 2.unity", () =>
        {
            // баспалдақ: 0 → 0.6 → 1.2 → 1.85 → арал 2.5 (бұрын 1.0 / 1.75 / 2.5)
            SetY("Level2_Map/Step 1", 0.35f);
            SetY("Level2_Map/Step 2", 0.95f);
            SetY("Level2_Map/Step 3", 1.6f);
            SetY("Coins/Coin  (2)", 2.0f);
        });

        Edit("Assets/Scenes/Level3_Boss.unity", () =>
        {
            // бүйір платформалар 1.5 → 1.0
            SetY("Arena/Platform L", 0.75f);
            SetY("Arena/Platform R", 0.75f);
        });

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log("[Jump] Дайын: секіру 5, деңгейлер бейімделді, crosshair қосылды.");
    }

    static void Edit(string path, System.Action changes)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        var mv = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Include);
        if (mv != null)
        {
            Undo.RecordObject(mv, "Jump");
            mv.jumpImpulse = JumpImpulse;
            EditorUtility.SetDirty(mv);
        }

        var cam = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (cam != null && cam.GetComponent<Crosshair>() == null)
            cam.gameObject.AddComponent<Crosshair>();

        changes();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Jump] {path}: сақталды");
    }

    static Transform Find(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t;
        }
        Debug.LogWarning($"[Jump] табылмады: {path}");
        return null;
    }

    static void SetY(string path, float y)
    {
        Transform t = Find(path); if (t == null) return;
        Vector3 p = t.position; p.y = y; t.position = p;
        EditorUtility.SetDirty(t);
    }

    static void SetYZ(string path, float y, float z)
    {
        Transform t = Find(path); if (t == null) return;
        Vector3 p = t.position; p.y = y; p.z = z; t.position = p;
        EditorUtility.SetDirty(t);
    }

    static void SetLocalY(string path, float y)
    {
        Transform t = Find(path); if (t == null) return;
        Vector3 p = t.localPosition; p.y = y; t.localPosition = p;
        EditorUtility.SetDirty(t);
    }
}
