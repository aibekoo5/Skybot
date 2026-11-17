using UnityEngine;
using UnityEngine.SceneManagement;

// Қазір қай деңгей (LevelData) ойналып жатыр. Level select → GameSession.Play(level) →
// сахна жүктеледі → LevelRunner GameSession.Current-тен деңгейдің деректерін алады.
public static class GameSession
{
    public static LevelData Current { get; private set; }

    public static void Play(LevelData level)
    {
        if (level == null) { Debug.LogError("[Sky Bot] GameSession.Play: level is null"); return; }
        Current = level;
        SceneManager.LoadScene(level.sceneName);
    }

    public static void PlayFirst()
    {
        LevelList list = LevelList.Instance;
        if (list != null) Play(list.First);
    }

    // Editor-да сахнаны тікелей Play басып ашсаң, Current бос — сахна атымен табамыз
    public static LevelData ResolveForActiveScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (Current != null && Current.sceneName == scene) return Current;
        LevelList list = LevelList.Instance;
        Current = list != null ? list.FindByScene(scene) : null;
        return Current;
    }
}
