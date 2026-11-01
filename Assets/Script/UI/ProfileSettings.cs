using UnityEngine;
using TMPro;
using UnityEngine.UI;     

public class ProfileSettings : MonoBehaviour
{
    public TMP_InputField nameField;
    public Toggle fullscreenToggle;  // inside the class
    public Toggle musicToggle;
    public AudioSource bgSound;

    void Start()
    {
        nameField.text = PlayerPrefs.GetString("PlayerName", "");
        colorDropdown.value = PlayerPrefs.GetInt("PlayerColor", 0);
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        musicToggle.isOn = PlayerPrefs.GetInt("Music", 1) == 1;
    }

    public void SaveName(string value)
    {
        PlayerPrefs.SetString("PlayerName", value);
    }
    public TMP_Dropdown colorDropdown;   // класс ішінде

    public void SaveColor(int index)
    {
        PlayerPrefs.SetInt("PlayerColor", index);
    }

     public void SetFullscreen(bool isOn)
    {
        Screen.fullScreen = isOn;
        PlayerPrefs.SetInt("Fullscreen", isOn ? 1 : 0);
    }

    public void SetMusic(bool isOn)
    {
        bgSound.mute = !isOn;
        PlayerPrefs.SetInt("Music", isOn ? 1 : 0);
    }
}