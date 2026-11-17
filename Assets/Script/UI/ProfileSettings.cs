using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Мәзірдегі баптаулар. Бұрын PlayerPrefs еді — енді бәрі save.json-ның "settings" бөлімінде.
// Әр өзгерістен кейін SaveSystem.SaveSoon() — жарты секундтан соң файлға жазылады.
public class ProfileSettings : MonoBehaviour
{
    public TMP_InputField nameField;
    public TMP_Dropdown colorDropdown;
    public Toggle fullscreenToggle;
    public Toggle musicToggle;
    public Slider volumeSlider;
    public AudioSource bgSound;

    void Start()
    {
        // ...WithoutNotify: басында мәнді қойғанда onValueChanged іске қосылмайды (бос сақтау болмайды)
        SettingsData s = SaveSystem.Settings;
        nameField.SetTextWithoutNotify(s.playerName);
        colorDropdown.SetValueWithoutNotify(s.playerColor);
        fullscreenToggle.SetIsOnWithoutNotify(s.fullscreen);
        musicToggle.SetIsOnWithoutNotify(s.musicOn);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(s.musicVolume);
        SaveSystem.ApplyAudio();
    }

    public void SaveName(string value)
    {
        SaveSystem.Settings.playerName = value;
        SaveSystem.SaveSoon();
    }

    public void SaveColor(int index)
    {
        SaveSystem.Settings.playerColor = index;
        SaveSystem.SaveSoon();
    }

    public void SetFullscreen(bool isOn)
    {
        SaveSystem.Settings.fullscreen = isOn;
        SaveSystem.ApplyScreen();
        SaveSystem.SaveSoon();
    }

    public void SetMusic(bool isOn)
    {
        SaveSystem.Settings.musicOn = isOn;
        SaveSystem.ApplyAudio();
        SaveSystem.SaveSoon();
    }
}
