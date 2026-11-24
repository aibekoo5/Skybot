using UnityEngine;
using UnityEngine.SceneManagement;

// Көрінбейтін көмекші объект: ойын басталғанда өзі пайда болады (сахнаға қою керек емес),
// сахна ауысқанда жойылмайды. SaveSystem static класс болғандықтан Unity оқиғаларын
// (OnApplicationPause, Update) осы арқылы алады.
public class SaveSystemHook : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject("[SaveSystem]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<SaveSystemHook>();

        SaveSystem.Load();             // бірінші сахнаға дейін жүктейміз
        SaveSystem.ApplyScreen();
    }

    void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // әр сахнада дыбыс баптауын қолданамыз (микшер)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SaveSystem.ApplyAudio();

    void Update() => SaveSystem.Tick();

    // телефонда: Home басылды / басқа қолданбаға өтті — бұл соңғы сенімді сәт,
    // одан кейін Android қолданбаны ескертусіз жауып тастауы мүмкін
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveSystem.Save();
    }

    void OnApplicationQuit() => SaveSystem.Save();
}
