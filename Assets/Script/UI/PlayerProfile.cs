using UnityEngine;
using TMPro;

public class PlayerProfile : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public Color[] colors = { Color.red, Color.blue, Color.green }; // Dropdown ретімен

    void Start()
    {
        nameText.text = PlayerPrefs.GetString("PlayerName", "Player");

        int colorIndex = PlayerPrefs.GetInt("PlayerColor", 0);
        // the hero model is a child object, so look for the renderer in children
        Renderer body = GetComponentInChildren<Renderer>();
        if (body != null && colorIndex >= 0 && colorIndex < colors.Length)
            body.material.color = colors[colorIndex];
    }
}