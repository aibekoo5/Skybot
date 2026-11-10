using UnityEngine;
using UnityEngine.UI;

// Экрандағы нысана (crosshair). Main Camera-ға қойылады, UI-ді өзі жасайды.
// Әрқашан экранның дәл ортасында тұрады; оқ мылтықтың ұшынан осы нүктеге ұшады
// (CameraFollow.GetAimPoint). Жау немесе бастық нысанада болса — қызыл түске боялады.
[RequireComponent(typeof(CameraFollow))]
public class Crosshair : MonoBehaviour
{
    public float size = 26f;                 // пиксель (1920x1080 экранға шаққанда)
    public float thickness = 3f;
    public float gap = 6f;                   // ортадағы бос орын
    public Color normalColor = new Color(1f, 1f, 1f, 0.9f);
    public Color enemyColor = new Color(1f, 0.25f, 0.2f, 1f);

    CameraFollow follow;
    RectTransform root;
    Image[] parts;
    Canvas canvas;

    void Start()
    {
        follow = GetComponent<CameraFollow>();
        Build();
    }

    void Build()
    {
        var go = new GameObject("CrosshairCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;           // HUD батырмалары үстінде қалады
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        root = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(go.transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);   // экран ортасы, қозғалмайды
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        float len = (size - gap) * 0.5f;
        float off = gap * 0.5f + len * 0.5f;
        parts = new[]
        {
            Bar(new Vector2(0, off),  new Vector2(thickness, len)),   // жоғары
            Bar(new Vector2(0, -off), new Vector2(thickness, len)),   // төмен
            Bar(new Vector2(-off, 0), new Vector2(len, thickness)),   // сол
            Bar(new Vector2(off, 0),  new Vector2(len, thickness)),   // оң
            Bar(Vector2.zero,         new Vector2(thickness, thickness)), // нүкте
        };
    }

    Image Bar(Vector2 pos, Vector2 sz)
    {
        var rt = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sz;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = normalColor;
        img.raycastTarget = false;           // саусақты/тінтуірді бөгемейді
        var sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.6f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);
        return img;
    }

    void LateUpdate()
    {
        if (follow == null || follow.target == null || root == null) return;

        bool show = Time.timeScale > 0f;
        canvas.enabled = show;
        if (!show) return;

        // экран ортасы жауға қарап тұр ма?
        follow.GetAimPoint(out Collider hit);
        bool onEnemy = hit != null && (hit.CompareTag("Enemy") || hit.GetComponentInParent<Boss>() != null
                                        || hit.GetComponentInParent<EnemyBase>() != null);

        Color c = onEnemy ? enemyColor : normalColor;
        foreach (var p in parts) p.color = c;
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
