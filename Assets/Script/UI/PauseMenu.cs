using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// On the level Canvas. The pause button (top-right) calls Pause(),
// the pause panel has Resume, Restart and Menu.
// Escape on a keyboard / Back on Android also toggles the pause.
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;      // full-screen panel, hidden at start
    public GameObject pauseButton;     // the "II" button in the HUD
    public string menuScene = "Main menu";

    PlayerInput playerInput;
    bool paused;

    void Start()
    {
        playerInput = FindAnyObjectByType<PlayerInput>();
        SetPaused(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!paused);
    }

    public void Pause()  { SetPaused(true); }
    public void Resume() { SetPaused(false); }

    public void SetPaused(bool value)
    {
        paused = value;
        if (pausePanel != null) pausePanel.SetActive(paused);
        if (pauseButton != null) pauseButton.SetActive(!paused);

        Time.timeScale = paused ? 0f : 1f;   // physics, timers and Update movement stop
        AudioListener.pause = paused;        // sounds stop too

        // the hero must not jump or shoot while the game is paused
        if (playerInput != null)
        {
            if (paused) playerInput.DeactivateInput();
            else playerInput.ActivateInput();
        }
    }

    public void Restart()
    {
        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ToMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(menuScene);
    }

    void OnDestroy()
    {
        // never leave the game frozen when the scene changes
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
