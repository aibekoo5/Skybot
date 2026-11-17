using UnityEngine;
using TMPro;

// Кейіпкердің аты мен түсі — save.json-дағы баптаулардан
public class PlayerProfile : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public Color[] colors = { Color.red, Color.blue, Color.green }; // Dropdown ретімен
    [Range(0f, 1f)] public float tintStrength = 0.35f;              // 0 = текстура өзгермейді, 1 = толық бояу

    void Start()
    {
        SettingsData s = SaveSystem.Settings;
        if (nameText != null) nameText.text = string.IsNullOrWhiteSpace(s.playerName) ? "Player" : s.playerName;

        int colorIndex = s.playerColor;
        // the hero model is a child object, so look for the renderer in children
        Renderer body = GetComponentInChildren<Renderer>();
        if (body != null && colorIndex >= 0 && colorIndex < colors.Length)
            // Swat-та текстура бар: толық бояу емес, жеңіл реңк береміз
            body.material.color = Color.Lerp(Color.white, colors[colorIndex], tintStrength);
    }
}
