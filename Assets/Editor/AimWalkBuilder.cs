using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools → Sky Bot → Add Aim Walk Layer
// SwatAnimator-ға «AimLocomotion» қабатын қосады: атып жатқанда кейіпкер экран жаққа қарайды,
// ал жүру бағыты кез келген болуы мүмкін — сондықтан жүгіру емес, 2D blend tree ойналады:
// алға жүру / артқа жүру / солға-оңға strafe / орнында көздеу.
// Қабаттың салмағын Movement басқарады (атып жатқанда 1, әйтпесе 0).
// Қабат ретi: Base → AimLocomotion → UpperBody (ату кезіндегі қолдар бәрінің үстінде).
public static class AimWalkBuilder
{
    public const string LayerName = "AimLocomotion";
    const string ControllerPath = "Assets/Animations/SwatAnimator.controller";
    const string Folder = "Assets/Models/Swat";

    [MenuItem("Tools/Sky Bot/Add Aim Walk Layer")]
    public static void Run()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("[AimWalk] SwatAnimator.controller табылмады"); return; }
        Build(ctrl);
        Debug.Log("[AimWalk] Дайын: атып жүргендегі анимация қосылды.");
    }

    [MenuItem("Tools/Sky Bot/Remove Shoot Animation")]
    public static void RemoveShootAnimation()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("[AimWalk] SwatAnimator.controller табылмады"); return; }

        for (int i = ctrl.layers.Length - 1; i >= 0; i--)
            if (ctrl.layers[i].name == "UpperBody") ctrl.RemoveLayer(i);
        foreach (var p in ctrl.parameters.Where(x => x.name == "Shoot").ToArray())
            ctrl.RemoveParameter(p);
        AssetDatabase.DeleteAsset("Assets/Animations/UpperBody.mask");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[AimWalk] Ату анимациясы (UpperBody, Shoot) алынып тасталды.");
    }

    static AnimationClip Clip(string file)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{Folder}/{file}.fbx")
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    // клип қай бағытқа жүреді (root motion бойынша) — strafe-тің қайсысы сол, қайсысы оң екенін анықтаймыз
    static Vector2 Dir(AnimationClip c, Vector2 fallback)
    {
        if (c == null) return fallback;
        Vector3 v = c.averageSpeed;
        Vector2 d = new Vector2(v.x, v.z);
        return d.magnitude > 0.05f ? d.normalized : fallback;
    }

    public static void Build(AnimatorController ctrl)
    {
        foreach (string p in new[] { "MoveX", "MoveZ" })
            if (!ctrl.parameters.Any(x => x.name == p))
                ctrl.AddParameter(p, AnimatorControllerParameterType.Float);

        // бұрынғы нұсқасы болса — өшіреміз
        for (int i = ctrl.layers.Length - 1; i >= 0; i--)
            if (ctrl.layers[i].name == LayerName) ctrl.RemoveLayer(i);

        ctrl.AddLayer(LayerName);
        int index = ctrl.layers.Length - 1;

        ctrl.CreateBlendTreeInController("AimMove", out BlendTree tree, index);
        tree.name = "AimMove";
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveZ";
        tree.useAutomaticThresholds = false;

        AnimationClip idle = Clip("rifle aiming idle");
        AnimationClip fwd = Clip("walking");
        AnimationClip back = Clip("walking backwards");
        AnimationClip s1 = Clip("strafe");
        AnimationClip s2 = Clip("strafe (2)");

        tree.AddChild(idle, Vector2.zero);
        if (fwd != null) tree.AddChild(fwd, new Vector2(0f, 1f));
        if (back != null) tree.AddChild(back, new Vector2(0f, -1f));

        // strafe: бағытын клиптің өзінен аламыз
        Vector2 d1 = Dir(s1, Vector2.left), d2 = Dir(s2, Vector2.right);
        if (Mathf.Sign(d1.x) == Mathf.Sign(d2.x)) d2 = -d1;   // екеуі бір жаққа шықса — екіншісін керісінше
        if (s1 != null) tree.AddChild(s1, new Vector2(Mathf.Sign(d1.x), 0f));
        if (s2 != null) tree.AddChild(s2, new Vector2(Mathf.Sign(d2.x), 0f));
        Debug.Log($"[AimWalk] strafe → {(d1.x < 0 ? "солға" : "оңға")}, strafe (2) → {(d2.x < 0 ? "солға" : "оңға")}");

        // қабатты UpperBody-дің астына жылжытамыз, салмағы 0 (Movement басқарады)
        var layers = ctrl.layers.ToList();
        var aim = layers[index];
        aim.defaultWeight = 0f;
        aim.blendingMode = AnimatorLayerBlendingMode.Override;
        layers.RemoveAt(index);
        int upper = layers.FindIndex(l => l.name == "UpperBody");
        layers.Insert(upper >= 0 ? upper : layers.Count, aim);   // UpperBody жоқ болса — соңына
        ctrl.layers = layers.ToArray();

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }
}
