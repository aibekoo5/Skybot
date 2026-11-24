# Cloud Strike

A 3D mobile action platformer made in Unity 6 (6000.6.0f1) for Android, landscape.
A soldier runs, jumps and shoots across floating islands above the clouds, collects every coin
to open the gate and finally defeats the boss.

Course project: Game Development Fundamentals, Narxoz University (weeks 9–13).
Author: Kemel Aibek.

## Screens
- **Main Menu**: title, Play, Settings, How to play, Quit
- **HUD**: hearts, coins (score), timer / boss bar, pause button, joystick, JUMP and FIRE
- **Pause**: Resume, Restart, Menu (time stops)
- **Win**: Play again, Menu

## Mobile UI
- Canvas Scaler: Scale With Screen Size, 1920 × 1080, Match = 1 (landscape)
- Every element anchored to its corner / edge / center; full-screen panels stretched
- Buttons and text inside a `SafeArea` panel (`Assets/Script/UI/SafeArea.cs`)
- Touch targets ≥ 120 × 120 px, text ≥ 36 px
- `Tools → Cloud Strike → Rebuild Mobile UI` re-applies the layout to all scenes

## How to run
1. Open the folder in Unity Hub (Unity 6000.6.0f1).
2. Open `Assets/Scenes/Main menu.unity` and press Play.
3. Test devices: Game view → **Simulator** (Window → General → Device Simulator).

## Controls
| Control | Action |
|---|---|
| Joystick (bottom-left) / WASD | Move |
| JUMP (bottom-right) / Space | Jump |
| FIRE / mouse, X | Shoot |
| II (top-right) / Esc / Android Back | Pause |

## Levels
| # | Level | Scene | Enemies (on the platforms) |
|---|---|---|---|
| 1 | Outpost | `Level1_Outpost` | 2 Rifleman, 2 Machine gun, 2 Brawler, on three sky islands joined by bridges |
| 2 | Sky Islands | `Level2_SkyIslands` | 2 Rifleman, Machine gun, Brawler |
| 3 | Spike Bridge | `Level3_SpikeBridge` | 2 Rifleman, Machine gun, Brawler |
| 4 | Boss Arena | `Level4_BossArena` | Boss + Machine gun and Rifleman on the side platforms |

Scene names follow the play order. Enemies come from `Assets/Data/Levels/Level_0N` and walk on a NavMesh that covers
only the platforms (Ground layer), so they do not walk off the edge.
`Tools → Cloud Strike → Build Sky Levels` (islands, sky, NavMesh, enemies), `Tools → Cloud Strike → Smoke Test Sky Levels`.

## Data and saves (week 11)
- Balance numbers live in `Assets/Data` (PlayerStats, Enemies/*Stats, BossStats, ItemValues), not in scripts
- Levels: `Assets/Data/Levels/Level_01..04` (LevelData) + order in `Assets/Resources/LevelList`; `LevelRunner` builds any level from its asset
- Progress + settings: `save.json` in `Application.persistentDataPath` (JsonUtility, broken file → defaults)
- `Tools → Cloud Strike → Build Data And Saves` (re-run safe), `Tools → Cloud Strike → Smoke Test Data And Saves`
- Script map: [`Docs/SCRIPT_MAP_Week11.md`](Docs/SCRIPT_MAP_Week11.md)

## Sound (week 12)
- AudioMixer `Assets/Audio/SkyBotMixer.mixer`: Master → Music → MusicBus, Master → SFX → SfxBus; snapshots **Normal** and **Paused** (music −16 dB + lowpass 700 Hz)
- Every AudioSource goes to a mixer group; music is played only by `AudioManager` (DontDestroyOnLoad, 1 s crossfade, no restart between levels)
- Settings: Music and Effects sliders in dB (Log10 × 20), saved in `save.json` (`musicVolume`, `sfxVolume`)
- Enemy attacks are heard before they land: wind-up sound on the first frame of Aim / SpinUp / WindUp
- `Tools → Cloud Strike → Build Sound Pass (Assignment 12)`, `Tools → Cloud Strike → Smoke Test Sound Pass`
- Script map: [`Docs/SCRIPT_MAP_Week12.md`](Docs/SCRIPT_MAP_Week12.md)

### Sound sources and licenses

**Made by us (CC0).** These are synthesised from math (oscillators, noise and envelopes) by

| File | Used for |
|---|---|
| `Assets/Audio/Music/music_level.wav` | level music (132 BPM chiptune loop, 29 s) |
| `Assets/Audio/SFX/ui_click.wav`, `ui_click_2.wav` | UI click |
| `Assets/Audio/SFX/lose.wav` | lose |
| `Assets/Audio/SFX/windup_rifle.wav`, `windup_mg.wav`, `windup_brawler.wav` | enemy wind-ups |
| `Assets/Audio/SFX/enemy_hit_1.wav`, `enemy_hit_2.wav` | enemy hit |
| `Assets/Audio/SFX/enemy_death.wav` | enemy death |
| `Assets/Audio/SFX/jump_2.wav`, `pickup_2.wav` | second jump / coin clip (variation) |
| `Assets/Audio/Enemies/rifle_shot.wav`, `mg_shot.wav` | Rifleman / machine gun shots (synthesised with Python + numpy in week 10) |

**Downloaded from [Pixabay](https://pixabay.com/sound-effects/)** under the [Pixabay Content License](https://pixabay.com/service/license-summary/) (free to use in games, no attribution required):

| File | Author on Pixabay | Used for |
|---|---|---|
| `Assets/Audio/Music/the_mountain-retro-game-593063.mp3` | The_Mountain | menu music |
| `Assets/Audio/SFX/driken5482-retro-hurt-2-236675.mp3` | driken5482 | player hurt |
| `Assets/Audio/SFX/explosion-in-tight-space-gamemaster-audio-no-reverb-tail-1-00-00.mp3` | Gamemaster Audio | bomb, boss death |
| `Assets/Audio/SFX/freesound_community-bullet-hit-metal-84818.mp3` | freesound_community | boss shot / hit |
| `Assets/Audio/SFX/freesound_community-heal-up-39285.mp3` | freesound_community | heart pickup |
| `Assets/Audio/SFX/lost-money-on-the-game-account.mp3` | freesound_community | coin pickup |
| `Assets/Audio/Enemies/surprised-male-scream.mp3` | freesound_community | Brawler shout when he sees you |


## Docs
- Game Design Document: [`Docs/SkyBot_GDD.pdf`](Docs/SkyBot_GDD.pdf)
- AI use: [`AI_LOG.txt`](AI_LOG.txt)
