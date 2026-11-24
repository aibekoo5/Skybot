using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Мәзірдегі баптаулар. Бұрын PlayerPrefs еді — енді бәрі save.json-ның "settings" бөлімінде.
// Әр өзгерістен кейін SaveSystem.SaveSoon() — жарты секундтан соң файлға жазылады.
// Week 12: Music және SFX слайдерлері. Слайдер 0..1, микшерге децибелмен (Log10 × 20) беріледі,
// жанындағы жазу сол децибелді көрсетеді.
public class ProfileSettings : MonoBehaviour
{
    public TMP_InputField nameField;
    public TMP_Dropdown colorDropdown;
    public Toggle fullscreenToggle;
    public Toggle musicToggle;
    public Slider volumeSlider;            // Music
    public Slider sfxSlider;               // SFX
    public TextMeshProUGUI musicDbText;
    public TextMeshProUGUI sfxDbText;
    public AudioSource bgSound;            // ескі (week 11), енді музыканы AudioManager ойнайды

    void Start()
    {
        // ...WithoutNotify: басында мәнді қойғанда onValueChanged іске қосылмайды (бос сақтау болмайды)
        SettingsData s = SaveSystem.Settings;
        nameField.SetTextWithoutNotify(s.playerName);
        colorDropdown.SetValueWithoutNotify(s.playerColor);
        fullscreenToggle.SetIsOnWithoutNotify(s.fullscreen);
        musicToggle.SetIsOnWithoutNotify(s.musicOn);
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(s.musicVolume);
            volumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }
        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(s.sfxVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
        ShowDb();
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
        ShowDb();
        SaveSystem.SaveSoon();
    }

    public void SetMusicVolume(float value)
    {
        SaveSystem.Settings.musicVolume = value;
        SaveSystem.ApplyAudio();
        ShowDb();
        SaveSystem.SaveSoon();
    }

    public void SetSfxVolume(float value)
    {
        SaveSystem.Settings.sfxVolume = value;
        SaveSystem.ApplyAudio();
        ShowDb();
        SaveSystem.SaveSoon();
        // слайдерді жылжытқанда SFX деңгейін бірден есту үшін — қысқа click
        if (AudioManager.Instance != null && Time.unscaledTime >= nextPreview)
        {
            AudioManager.Instance.PlayClick();
            nextPreview = Time.unscaledTime + 0.12f;
        }
    }

    float nextPreview;

    void ShowDb()
    {
        SettingsData s = SaveSystem.Settings;
        if (musicDbText != null) musicDbText.text = s.musicOn ? FormatDb(s.musicVolume) : "OFF";
        if (sfxDbText != null) sfxDbText.text = FormatDb(s.sfxVolume);
    }

    static string FormatDb(float linear)
    {
        float db = AudioManager.ToDecibels(linear);
        return db <= -79.9f ? "MUTE" : db.ToString("0") + " dB";
    }
}
