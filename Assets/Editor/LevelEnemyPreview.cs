using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Жаулар LevelData asset-те тұрады, сондықтан сахнада жоқ (ойын басталғанда LevelRunner шығарады).
// Осы құрал редакторда оларды КӨРСЕТЕДІ және ӨЗГЕРТУГЕ мүмкіндік береді:
//   * LevelRunner бар сахна ашылғанда "~ Enemies (edit → Level_XX)" объектісі пайда болады — ішінде жаулар
//     мен олардың патруль нүктелері. Оларды кәдімгі Move/Rotate құралымен жылжытасың, Ctrl+D-мен көшіресің,
//     Delete-пен өшіресің, Project-тен жау prefab-ын осы объектінің ішіне сүйреп жаңа жау қосасың.
//   * Әр өзгеріс бірден LevelData asset-ке жазылады (Ctrl+S мен Play алдында да тексеріледі).
//   * Бұл объектілер сахнаға САҚТАЛМАЙДЫ (HideFlags.DontSave) — деңгей бәрібір деректен құрылады.
// Мәзір: Tools → Cloud Strike → Enemy Preview → Rebuild / Save To LevelData
[InitializeOnLoad]
public static class LevelEnemyPreview
{
    const string RootPrefix = "~ Enemies (edit → ";
    const string PointPrefix = "Patrol point ";

    static LevelEnemyPreview()
    {
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Rebuild;
        EditorSceneManager.sceneSaving += (s, path) => SaveToData();
        EditorApplication.playModeStateChanged += OnPlayMode;
        SceneView.duringSceneGui += DrawPaths;
        ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => QueueSave();   // жылжыттың → бірден asset-ке
        EditorApplication.delayCall += Rebuild;            // скрипттер қайта жүктелгенде
    }

    static void OnPlayMode(PlayModeStateChange st)
    {
        if (st == PlayModeStateChange.ExitingEditMode) { SaveToData(); Clear(); }   // Play-де екі есе жау болмасын
        if (st == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Rebuild;
    }

    static bool saveQueued;
    static void QueueSave()
    {
        if (saveQueued || Application.isPlaying || FindRoot() == null) return;
        saveQueued = true;
        EditorApplication.delayCall += () => { saveQueued = false; SaveToData(); };
    }

    static LevelData CurrentData()
    {
        if (Application.isPlaying || Object.FindFirstObjectByType<LevelRunner>() == null) return null;
        LevelList list = Resources.Load<LevelList>("LevelList");
        return list != null ? list.FindByScene(SceneManager.GetActiveScene().name) : null;
    }

    static Transform FindRoot()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name.StartsWith(RootPrefix)) return go.transform;
        return null;
    }

    static void Clear()
    {
        Transform root;
        while ((root = FindRoot()) != null) Object.DestroyImmediate(root.gameObject);
    }

    [MenuItem("Tools/Cloud Strike/Enemy Preview/Rebuild From LevelData")]
    public static void Rebuild()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Clear();
        LevelData data = CurrentData();
        if (data == null) return;

        var root = new GameObject(RootPrefix + data.name + ")");
        root.hideFlags = HideFlags.DontSave;

        foreach (EnemySpawn s in data.enemies)
        {
            if (s.prefab == null) continue;
            var e = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab.gameObject, root.scene);
            e.transform.SetParent(root.transform, false);
            e.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
            if (s.patrolPoints == null) continue;
            for (int i = 0; i < s.patrolPoints.Length; i++)
            {
                var p = new GameObject(PointPrefix + i).transform;
                p.SetParent(e.transform, false);
                p.position = s.patrolPoints[i];
            }
        }
        SetDontSave(root.transform);
    }

    static void SetDontSave(Transform t)
    {
        t.gameObject.hideFlags = HideFlags.DontSave;
        foreach (Transform c in t) SetDontSave(c);
    }

    [MenuItem("Tools/Cloud Strike/Enemy Preview/Save To LevelData")]
    public static void SaveToData()
    {
        Transform root = FindRoot();
        LevelData data = CurrentData();
        if (root == null || data == null) return;

        var list = new List<EnemySpawn>();
        foreach (Transform e in root)
        {
            var enemy = e.GetComponent<EnemyBase>();
            var prefab = enemy != null ? PrefabUtility.GetCorrespondingObjectFromSource(enemy) : null;
            if (enemy == null || prefab == null)
            {
                Debug.LogWarning($"[Sky Bot] '{e.name}' is not an enemy prefab instance — skipped", e);
                continue;
            }
            var points = new List<Vector3>();
            foreach (Transform c in e)
                if (c.name.StartsWith(PointPrefix)) points.Add(c.position);

            list.Add(new EnemySpawn
            {
                prefab = prefab,
                position = e.position,
                yaw = e.eulerAngles.y,
                patrolPoints = points.ToArray(),
            });
        }

        if (Same(data.enemies, list)) return;
        Undo.RecordObject(data, "Edit level enemies");
        data.enemies = list;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        SetDontSave(root);                                  // жаңадан қосылған (сүйрелген/көшірілген) объектілер де сақталмасын
        Debug.Log($"[Sky Bot] {list.Count} enemies saved to {data.name}");
    }

    static bool Same(List<EnemySpawn> a, List<EnemySpawn> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            EnemySpawn x = a[i], y = b[i];
            if (x.prefab != y.prefab || x.position != y.position || !Mathf.Approximately(x.yaw, y.yaw)) return false;
            int nx = x.patrolPoints?.Length ?? 0, ny = y.patrolPoints?.Length ?? 0;
            if (nx != ny) return false;
            for (int k = 0; k < nx; k++) if (x.patrolPoints[k] != y.patrolPoints[k]) return false;
        }
        return true;
    }

    // патруль жолы мен құлайтын заттар аймағы Scene терезесінде сызылады
    static void DrawPaths(SceneView view)
    {
        if (Application.isPlaying) return;
        Transform root = FindRoot();
        if (root != null)
        {
            Handles.color = new Color(1f, 0.6f, 0.1f);
            foreach (Transform e in root)
            {
                Vector3 prev = e.position, first = Vector3.zero;
                bool any = false;
                foreach (Transform c in e)
                {
                    if (!c.name.StartsWith(PointPrefix)) continue;
                    Handles.DrawDottedLine(prev, c.position, 4f);
                    Handles.SphereHandleCap(0, c.position, Quaternion.identity, 0.3f, EventType.Repaint);
                    if (!any) first = c.position;
                    prev = c.position; any = true;
                }
                Handles.Label(e.position + Vector3.up * 2.2f, e.name);
                if (any) Handles.DrawDottedLine(prev, first, 4f);
            }
        }

        LevelData data = CurrentData();
        if (data == null) return;
        Handles.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        foreach (HazardWave w in data.hazardWaves)
        {
            Vector3 c = w.center + Vector3.up * w.spawnHeight;
            Handles.DrawWireCube(c, new Vector3(w.halfSize.x * 2f, 0.05f, w.halfSize.y * 2f));
            Handles.Label(c, "Hazard wave");
        }
    }
}
