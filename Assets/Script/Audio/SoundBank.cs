using UnityEngine;
using UnityEngine.Audio;

// Дыбыстың барлық «баптауы» бір asset-те: Assets/Resources/SoundBank.asset.
// AudioManager оны Resources-тен өзі жүктейді, сондықтан сахнаға ештеңе қоюдың керегі жоқ.
// Микшер: Master → Music → MusicBus (музыка көздері)
//                → SFX   → SfxBus   (барлық эффект көздері)
// Music / SFX — ойыншының слайдері (exposed параметр), MusicBus / SfxBus — микс деңгейі мен
// снапшоттар (Normal / Paused). Слайдер снапшотты бұзбайды, снапшот слайдерді бұзбайды.
[CreateAssetMenu(menuName = "Sky Bot/Sound Bank", fileName = "SoundBank")]
public class SoundBank : ScriptableObject
{
    [Header("Микшер")]
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;       // MusicBus
    public AudioMixerGroup sfxGroup;         // SfxBus
    public AudioMixerSnapshot normalSnapshot;
    public AudioMixerSnapshot pausedSnapshot;

    [Tooltip("exposed параметрлердің аттары (микшерде дәл осылай аталады)")]
    public string masterParam = "MasterVolume";
    public string musicParam = "MusicVolume";
    public string sfxParam = "SfxVolume";

    [Header("Музыка")]
    public AudioClip menuMusic;              // Main menu + Win
    public AudioClip levelMusic;             // барлық деңгейлер
    [Min(0.1f)] public float crossfadeTime = 1f;
    [Tooltip("осы сахналарда мәзір музыкасы, қалғанында — деңгей музыкасы")]
    public string[] menuScenes = { "Main menu", "Win" };

    [Header("2D дыбыстар (сахна ауысса да үзілмейді)")]
    public AudioClip[] uiClick;
    public AudioClip win;
    public AudioClip lose;
    [Range(0f, 1f)] public float uiVolume = 0.7f;

    [Header("Вариация")]
    public float pitchMin = 0.92f;
    public float pitchMax = 1.08f;
}
