using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools → Sky Bot → Swap Hero To Y Bot
// 1) Y Bot.fbx (BOT Shooter Pack) және Y Bot@Boxing.fbx-ті Humanoid етіп импорттайды
// 2) Барлық деңгейде ойыншының Swat моделін Y Bot-қа ауыстырады (анимациялар сол күйі — Humanoid)
// Swat енді жауларда (Rifleman, Brawler) қолданылады.
public static class CharacterPacks
{
    public const string YBotPath = "Assets/Models/YBot/Y Bot.fbx";
    public const string BoxingPath = "Assets/Models/Boxing/Y Bot@Boxing.fbx";
    const string HeroName = "Hero_YBot";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level 2.unity",
        "Assets/Scenes/Level2_Outpost.unity",
        "Assets/Scenes/Level3_Boss.unity",
    };

    [MenuItem("Tools/Sky Bot/Swap Hero To Y Bot")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        Avatar avatar = ImportYBot();
        ImportBoxing();
        if (avatar == null) { Debug.LogError("[YBot] avatar жасалмады"); return; }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(YBotPath);
        foreach (string s in Scenes)
            if (File.Exists(s)) Swap(s, model, avatar);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log("[YBot] Дайын: ойыншы Y Bot-қа ауыстырылды.");
    }

    public static Avatar ImportYBot()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(YBotPath);
        if (imp == null) return null;
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = false;
        imp.SaveAndReimport();

        // материалдарды шығарып, URP Lit-ке ауыстырамыз (түсі сақталады)
        string dir = "Assets/Models/YBot/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Models/YBot", "Materials");
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(YBotPath).OfType<Material>().ToArray())
        {
            string dst = $"{dir}/{m.name}.mat";
            if (!File.Exists(dst)) AssetDatabase.ExtractAsset(m, dst);
        }
        AssetDatabase.ImportAsset(YBotPath, ImportAssetOptions.ForceUpdate);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                    : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            if (lit != null && mat.shader != lit) mat.shader = lit;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[YBot] материал {mat.name}: {c}");
        }
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAllAssetsAtPath(YBotPath).OfType<Avatar>().FirstOrDefault();
    }

    public static void ImportBoxing()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(BoxingPath);
        if (imp == null) { Debug.LogWarning("[YBot] Boxing файлы табылмады"); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = "Boxing";
            c.loopTime = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = false;   // орнында ойналады (root motion елемейміз)
            c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static void Swap(string path, GameObject model, Avatar avatar)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var mv = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Include);
        if (mv == null || mv.anim == null) { Debug.LogWarning($"[YBot] {path}: Movement/anim табылмады"); return; }

        Transform player = mv.transform;
        Animator oldAnim = mv.anim;
        Transform oldModel = oldAnim.transform;
        if (oldModel.name == HeroName) { Debug.Log($"[YBot] {path}: бұрын ауыстырылған"); return; }

        var hero = (GameObject)PrefabUtility.InstantiatePrefab(model, player);
        hero.name = HeroName;
        hero.transform.localPosition = oldModel.localPosition;
        hero.transform.localRotation = oldModel.localRotation;
        hero.transform.localScale = Vector3.one * HeightScale(oldModel.gameObject, hero);
        hero.transform.SetSiblingIndex(oldModel.GetSiblingIndex());   // PlayerProfile бірінші рендерерді бояйды

        var anim = hero.GetComponent<Animator>();
        if (anim == null) anim = hero.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        mv.anim = anim;
        EditorUtility.SetDirty(mv);

        // мылтық жаңа қолдарға бекітіледі
        foreach (var hold in player.GetComponentsInChildren<GunHold>(true))
        {
            hold.body = anim;
            EditorUtility.SetDirty(hold);
        }

        oldModel.gameObject.SetActive(false);
        if (!oldModel.name.EndsWith("(old)")) oldModel.name += " (old)";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[YBot] {path}: ойыншы Y Bot");
    }

    static float HeightScale(GameObject oldModel, GameObject hero)
    {
        float oldH = Height(oldModel), newH = Height(hero);
        if (oldH < 0.1f || newH < 0.1f) return 1f;
        float s = oldH / newH;
        return Mathf.Abs(s - 1f) < 0.05f ? 1f : s;
    }

    static float Height(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return 0f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }
}
