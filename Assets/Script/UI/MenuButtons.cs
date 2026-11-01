using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButtons : MonoBehaviour
{
    public AudioSource bgSound;
    public GameObject mainMenuPanel;   
    public GameObject settingsPanel;
    public GameObject helpPanel;

    void Start()
    {
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        helpPanel.SetActive(false); 
    }

    public void SetVolume(float value)
    {
        bgSound.volume = value;
    }

    public void Play()
    {
        SceneManager.LoadScene("Level1");
    }

    public void Quit()
    {
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