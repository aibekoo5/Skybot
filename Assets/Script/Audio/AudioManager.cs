using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ойынның бүкіл дыбыс қабатының «орталығы». Бір ғана дана, сахна ауысқанда ЖОЙЫЛМАЙДЫ
// (DontDestroyOnLoad), сондықтан музыка деңгей жүктелген сайын басынан басталмайды.
//
// Не істейді:
//  1) Музыка: екі AudioSource (A/B). Трек ауысқанда 1 с crossfade (cos/sin қисығы — дыбыс
//     қаттылығы түспейді, жаңа трек 0-ден басталады → «шырт» етпейді). Сол трек болса — ештеңе.
//  2) Дыбыс деңгейі: save.json-дағы 0..1 слайдер → децибел (Log10 × 20) → микшердің exposed параметрі.
//  3) Пауза: Paused снапшот (музыка тыныштау + lowpass). SFX-ті AudioListener.pause тоқтатады,
//     ал музыка мен UI көздерінде ignoreListenerPause = true — олар ойнай береді.
//  4) Әр сахнада: барлық Button/Toggle-ға click дыбысы, шығысы жоқ AudioSource-тарды SfxBus-қа бағыттайды.
//  5) Win / Lose / UI — 2D, сахна жүктелсе де соңына дейін ойналады.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    SoundBank bank;
    readonly AudioSource[] music = new AudioSource[2];
    int active;                       // қазір қайсысы негізгі (0 немесе 1)
    AudioSource ui;                   // UI / win / lose
    Coroutine fade;
    bool paused;

    public SoundBank Bank => bank;
    public AudioClip CurrentMusic => music[active] != null ? music[active].clip : null;
    public bool IsPaused => paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        SoundBank b = Resources.Load<SoundBank>("SoundBank");
        if (b == null) { Debug.LogWarning("[Audio] Resources/SoundBank not found — run Tools → Cloud Strike → Build Sound Pass"); return; }

        var go = new GameObject("[AudioManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>().Init(b);
    }

    void Init(SoundBank b)
    {
        Instance = this;
        bank = b;
        for (int i = 0; i < 2; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.outputAudioMixerGroup = bank.musicGroup;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 0;                    // музыка ешқашан «ығыстырылмайды»
            s.ignoreListenerPause = true;      // пауза кезінде де естіледі (снапшот тыныштатады)
            music[i] = s;
        }
        ui = gameObject.AddComponent<AudioSource>();
        ui.outputAudioMixerGroup = bank.sfxGroup;
        ui.playOnAwake = false;
        ui.spatialBlend = 0f;
        ui.ignoreListenerPause = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // AudioMixer.SetFloat Awake-те жұмыс істемейді (микшер әлі дайын емес) — Start-та қоямыз
    void Start() => ApplyVolumes();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetPaused(false);
        RouteSceneSources(scene);
        HookUiClicks(scene);
        PlayMusic(IsMenuScene(scene.name) ? bank.menuMusic : bank.levelMusic);
        ApplyVolumes();
    }

    bool IsMenuScene(string sceneName)
    {
        foreach (string s in bank.menuScenes) if (s == sceneName) return true;
        return false;
    }

    // ---------------- музыка ----------------

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource cur = music[active];
        if (cur.clip == clip && cur.isPlaying) return;       // сол трек — басынан бастамаймыз

        AudioSource next = music[1 - active];
        if (next.clip != clip || !next.isPlaying)            // қайта ауыссақ (fade ортасында) — жалғастырамыз
        {
            next.clip = clip;
            next.volume = 0f;
            next.Play();
        }
        active = 1 - active;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Crossfade(cur, next, bank.crossfadeTime));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float time)
    {
        float fromStart = from.volume, toStart = to.volume;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)   // пауза (timeScale = 0) кезінде де жүреді
        {
            float k = t / time;
            from.volume = fromStart * Mathf.Cos(k * Mathf.PI * 0.5f);
            to.volume = Mathf.Lerp(toStart, 1f, Mathf.Sin(k * Mathf.PI * 0.5f));
            yield return null;
        }
        from.volume = 0f;
        from.Stop();
        to.volume = 1f;
        fade = null;
    }

    // ---------------- дыбыс деңгейі ----------------

    // слайдер 0..1 сызықты, ал құлақ — логарифмдік: 0.5 → −6 dB, 0.1 → −20 dB, 0 → −80 dB (тыныш)
    public static float ToDecibels(float linear)
    {
        return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }

    public void ApplyVolumes()
    {
        if (bank == null || bank.mixer == null) return;
        SettingsData s = SaveSystem.Settings;
        bank.mixer.SetFloat(bank.musicParam, s.musicOn ? ToDecibels(s.musicVolume) : -80f);
        bank.mixer.SetFloat(bank.sfxParam, ToDecibels(s.sfxVolume));
    }

    // ---------------- пауза ----------------

    public void SetPaused(bool value)
    {
        if (bank == null || paused == value) return;
        paused = value;
        // микшер UnscaledTime режимінде — timeScale = 0 болса да ауысу жүреді
        UnityEngine.Audio.AudioMixerSnapshot snap = value ? bank.pausedSnapshot : bank.normalSnapshot;
        if (snap != null) snap.TransitionTo(value ? 0.3f : 0.2f);
    }

    // ---------------- 2D дыбыстар ----------------

    public void PlayClick()
    {
        if (bank.uiClick == null || bank.uiClick.Length == 0) return;
        PlayUi(bank.uiClick[Random.Range(0, bank.uiClick.Length)], bank.uiVolume, true);
    }

    public void PlayWin(AudioClip fallback = null) => PlayUi(bank.win != null ? bank.win : fallback, 1f, false);
    public void PlayLose() => PlayUi(bank.lose, 1f, false);

    void PlayUi(AudioClip clip, float volume, bool vary)
    {
        if (clip == null) return;
        ui.pitch = vary ? RandomPitch() : 1f;
        ui.PlayOneShot(clip, volume);
    }

    // PlayClipAtPoint-тың орнына: уақытша көз, бірақ SfxBus арқылы (микшер мен слайдер әсер етеді)
    public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("OneShot " + clip.name);
        go.transform.position = pos;
        AudioSource s = go.AddComponent<AudioSource>();
        if (Instance != null) s.outputAudioMixerGroup = Instance.bank.sfxGroup;
        s.spatialBlend = 0.8f;
        s.minDistance = 6f;
        s.maxDistance = 60f;
        s.pitch = RandomPitch();
        s.PlayOneShot(clip, volume);
        Destroy(go, clip.length / s.pitch + 0.2f);
    }

    // ---------------- вариация ----------------

    public static float RandomPitch()
    {
        if (Instance == null) return Random.Range(0.92f, 1.08f);
        return Random.Range(Instance.bank.pitchMin, Instance.bank.pitchMax);
    }

    // қайталанатын дыбыс: кездейсоқ биіктік (0.92–1.08) + екі клиптің бірі
    public static void PlayVaried(AudioSource src, AudioClip a, AudioClip b = null, float volume = 1f)
    {
        if (src == null) return;
        AudioClip clip = b != null && Random.value < 0.5f ? b : a;
        if (clip == null) clip = a != null ? a : b;
        if (clip == null) return;
        src.pitch = RandomPitch();
        src.PlayOneShot(clip, volume);
    }

    // ---------------- сахна ----------------

    void RouteSceneSources(Scene scene)
    {
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (s.gameObject.scene != scene) continue;
            // ескі сахна музыкасы (бұрын әр сахнада өз музыкасы болған) — енді музыка тек осы жерде
            if (s.loop && s.clip != null && (s.clip == bank.menuMusic || s.clip == bank.levelMusic))
            {
                s.Stop();
                s.enabled = false;
                continue;
            }
            if (s.outputAudioMixerGroup == null) s.outputAudioMixerGroup = bank.sfxGroup;
        }
    }

    void HookUiClicks(Scene scene)
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include))
            if (b.gameObject.scene == scene) b.onClick.AddListener(PlayClick);
        foreach (Toggle t in FindObjectsByType<Toggle>(FindObjectsInactive.Include))
            if (t.gameObject.scene == scene) t.onValueChanged.AddListener(_ => PlayClick());
    }
}
