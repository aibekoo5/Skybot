using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools → Sky Bot → Swap Hero To Swat
// 1) Mixamo файлдарын Humanoid етіп импорттайды
// 2) SwatAnimator.controller жасайды (Idle / Running / Jump + жоғарғы денеде Shoot)
// 3) Level1, Level 2, Level3_Boss сахналарында X Bot-ты Swat-қа ауыстырады
// Қайта іске қосуға болады (идемпотент).
public static class SwatHeroSwapper
{
    const string Folder = "Assets/Models/Swat";
    const string ModelPath = Folder + "/Swat.fbx";
    const string ControllerPath = "Assets/Animations/SwatAnimator.controller";
    const string OldControllerPath = "Assets/Animations/PlayerAnimator.controller";
    const string MaskPath = "Assets/Animations/UpperBody.mask";
    const string HeroName = "Hero_Swat";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level 2.unity",
        "Assets/Scenes/Level3_Boss.unity",
    };

    // қайталанатын анимациялар
    static readonly string[] Loops =
    {
        "rifle aiming idle", "rifle run", "walking", "walking backwards",
        "run backwards", "strafe", "strafe (2)",
    };

    [MenuItem("Tools/Sky Bot/Swap Hero To Swat")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        Avatar avatar = ImportModel();
        if (avatar == null) { Debug.LogError("[Swat] Swat.fbx avatar жасалмады"); return; }
        ImportAnimations(avatar);
        AnimatorController ctrl = BuildController();
        AimWalkBuilder.Build(ctrl);   // атып жүргендегі анимация қабаты

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach (string s in Scenes)
            if (File.Exists(s)) SwapInScene(s, model, avatar, ctrl);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log("[Swat] Дайын: кейіпкер Swat-қа ауыстырылды.");
    }

    static Avatar ImportModel()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = false;
        imp.SaveAndReimport();
        SetupMaterials(imp);
        return AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
    }

    // Mixamo текстуралары FBX ішінде — шығарып алып, URP Lit материалдарына қосамыз
    static void SetupMaterials(ModelImporter imp)
    {
        string texDir = Folder + "/Textures";
        string matDir = Folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(texDir)) AssetDatabase.CreateFolder(Folder, "Textures");
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(Folder, "Materials");

        imp.ExtractTextures(texDir);
        AssetDatabase.Refresh();

        // normal map текстураларын дұрыс типке қоямыз
        foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            bool normal = p.ToLower().Contains("normal");
            if (normal && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        // FBX ішіндегі материалдарды бөлек .mat файлдарына шығарамыз
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray())
        {
            string dst = $"{matDir}/{m.name}.mat";
            if (File.Exists(dst)) continue;
            AssetDatabase.ExtractAsset(m, dst);
        }
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            string part = mat.name.ToLower().Contains("head") ? "head" : "Body";

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Soldier_{part}_diffuse.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Soldier_{part}_normal.png");

            if (lit != null) mat.shader = lit;
            mat.SetColor("_BaseColor", Color.white);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[Swat] материал {mat.name}: {(baseMap != null ? baseMap.name : "ТЕКСТУРА ЖОҚ")}");
        }
        AssetDatabase.SaveAssets();
    }

    static void ImportAnimations(Avatar avatar)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == ModelPath) continue;

            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = avatar;
            imp.importAnimation = true;

            string name = Path.GetFileNameWithoutExtension(path);
            bool loop = Loops.Contains(name);
            bool isJump = name.StartsWith("jump");

            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = name;
                c.loopTime = loop;
                c.loopPose = loop;
                // кейіпкерді Rigidbody жылжытады, анимация орнында ойналады
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                // XZ «Bake Into Pose» ӨШІРУЛІ: алға жылжу root motion-ға кетеді,
                // ал applyRootMotion = false болғандықтан ол еленбейді → анимация орнында
                c.lockRootPositionXZ = false;
                c.keepOriginalPositionXZ = true;
                c.lockRootHeightY = !isJump;   // секіру биіктігін физика береді
                c.keepOriginalPositionY = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    static AnimationClip Clip(string file)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{Folder}/{file}.fbx")
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AssetDatabase.CopyAsset(OldControllerPath, ControllerPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        // Base Layer: ескі күйлер сақталады, тек клиптер ауысады
        foreach (var cs in ctrl.layers[0].stateMachine.states)
        {
            switch (cs.state.name)
            {
                case "Idle": cs.state.motion = Clip("rifle aiming idle"); break;
                case "Running": cs.state.motion = Clip("rifle run"); break;
                case "Jump": cs.state.motion = Clip("jump forward"); break;
            }
        }

        // ату анимациясы (UpperBody қабаты) алынып тасталды — пайдаланушы сұрауы бойынша

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static void SwapInScene(string path, GameObject model, Avatar avatar, AnimatorController ctrl)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var mv = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Include);
        if (mv == null) { Debug.LogWarning($"[Swat] {path}: Movement табылмады"); return; }

        Transform player = mv.transform;
        Transform existing = player.Find(HeroName);
        Animator oldAnim = mv.anim;

        Transform oldModel = oldAnim != null && oldAnim.transform != player ? oldAnim.transform : null;

        GameObject hero;
        if (existing != null)
        {
            hero = existing.gameObject;   // бұрын ауыстырылған — тек баптауларды жаңартамыз
        }
        else
        {
            hero = (GameObject)PrefabUtility.InstantiatePrefab(model, player);
            hero.name = HeroName;
            if (oldModel != null)
            {
                hero.transform.localPosition = oldModel.localPosition;
                hero.transform.localRotation = oldModel.localRotation;
                hero.transform.localScale = Vector3.one * HeightScale(oldModel.gameObject, hero);
            }
            else
            {
                hero.transform.localPosition = Vector3.zero;
                hero.transform.localRotation = Quaternion.identity;
            }
        }

        var anim = hero.GetComponent<Animator>();
        if (anim == null) anim = hero.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        mv.anim = anim;
        EditorUtility.SetDirty(mv);

        // ескі X Bot өшіріледі, бірақ өшірілмейді — қайтару оңай болсын
        if (oldModel != null && oldModel.gameObject != hero)
        {
            oldModel.gameObject.SetActive(false);
            if (!oldModel.name.EndsWith("(old)")) oldModel.name += " (old)";
        }
        else if (oldAnim != null && oldAnim.transform == player)
        {
            // Animator Player-дің өзінде болса — ескі мешті жасырамыз
            oldAnim.enabled = false;
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(hero.transform)) r.enabled = false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Swat] {path}: кейіпкер ауыстырылды");
    }

    // жаңа модельді ескі X Bot-тың биіктігіне келтіреді
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
