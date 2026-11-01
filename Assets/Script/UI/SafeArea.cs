using UnityEngine;

// Put this on a full-screen panel inside the Canvas.
// The panel shrinks to Screen.safeArea, so buttons and text never go
// under a notch, a camera hole, rounded corners or the home bar.
// Hierarchy: Canvas -> Background (stretch) + SafeArea (this script) -> HUD
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform panel;
    Rect lastSafeArea;
    Vector2Int lastScreen;

    void Awake()
    {
        panel = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        // the phone was rotated or the Device Simulator switched devices
        if (Screen.safeArea != lastSafeArea ||
            Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            Apply();
    }

    void Apply()
    {
        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = lastSafeArea.position;
        Vector2 max = lastSafeArea.position + lastSafeArea.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;

        panel.anchorMin = min;      // the panel's corners follow
        panel.anchorMax = max;      // the visible part of the screen
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }
}
