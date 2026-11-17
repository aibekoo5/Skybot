using UnityEngine;
using TMPro;

public class MenuButtons : MonoBehaviour
{
    public AudioSource bgSound;
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject helpPanel;
    public LevelSelectMenu levelSelect;          // PLAY → деңгей таңдау
    public TextMeshProUGUI saveStatusText;       // «Progress loaded» / «Save file was damaged...»

    void Start()
    {
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        helpPanel.SetActive(false);
        if (levelSelect != null) levelSelect.gameObject.SetActive(false);
        ShowSaveStatus();
    }

    // сақтау файлы қалай жүктелгенін мәзірде көрсетеміз (бейнедегі «бұзық файл» тесті үшін)
    void ShowSaveStatus()
    {
        if (saveStatusText == null) return;
        switch (SaveSystem.LastLoad)
        {
            case SaveLoadResult.Loaded:
                saveStatusText.text = $"Progress loaded: {SaveSystem.Data.unlockedLevels.Count} level(s) unlocked, total best {SaveSystem.TotalBestScore}";
                saveStatusText.color = Color.white;
                break;
            case SaveLoadResult.NewGame:
                saveStatusText.text = "New game - no save file yet";
                saveStatusText.color = Color.white;
                break;
            case SaveLoadResult.BrokenFileReset:
                saveStatusText.text = "Save file was damaged - started with default values";
                saveStatusText.color = new Color(1f, 0.6f, 0.4f);
                break;
        }
    }

    // Settings-тегі слайдер
    public void SetVolume(float value)
    {
        SaveSystem.Settings.musicVolume = value;
        SaveSystem.ApplyAudio();
        SaveSystem.SaveSoon();                   // баптау өзгерді → сақтаймыз
    }

    public void Play()
    {
        if (levelSelect != null) levelSelect.Open();
        else GameSession.PlayFirst();
    }

    public void Quit()
    {
        SaveSystem.Save();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void Settings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void Back()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        SaveSystem.Save();                       // баптаулар терезесі жабылды — бірден жазамыз
    }

    public void OpenHelp()
    {
        mainMenuPanel.SetActive(false);
        helpPanel.SetActive(true);
    }

    public void CloseHelp()
    {
        helpPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}
