# Sky Bot — Script map, Week 12 (Sound Pass)

## AudioMixer — `Assets/Audio/SkyBotMixer.mixer`

```
Master            exposed: MasterVolume (0 dB)
├─ Music          exposed: MusicVolume  ← Settings "Music volume" slider (dB)
│   └─ MusicBus   snapshot Normal: −8 dB, Lowpass 22000 Hz   ← AudioManager music sources A/B
│                 snapshot Paused: −16 dB, Lowpass   700 Hz
└─ SFX            exposed: SfxVolume    ← Settings "Effects volume" slider (dB)
    └─ SfxBus     −3 dB (both snapshots)                    ← every other AudioSource
```

Why two levels per branch: an exposed parameter is no longer controlled by snapshots. So the
player's slider sits on **Music / SFX**, and the mix level + the Paused snapshot sit on
**MusicBus / SfxBus**. The slider never breaks the pause effect, and the pause never resets the slider.

Update mode = **Unscaled Time**: the Paused snapshot fades in while `Time.timeScale = 0`.

## Sound events

| # | Event | Clip(s) | Who plays it | Variation |
|---|---|---|---|---|
| 1 | Jump | `jump.wav`, `SFX/jump_2.wav` | `Movement.OnJump` | 2 clips + pitch 0.92–1.08 |
| 2 | Hit (player hurt) | `SFX/driken5482-retro-hurt-2…mp3` | `Health.TakeDamage` | pitch |
| 2b | Hit (enemy hit by bullet) | `SFX/enemy_hit_1/2.wav` | `EnemyBase.TakeHit` | 2 clips + pitch |
| 3 | Pickup (coin / heart) | `SFX/lost-money…mp3`, `SFX/pickup_2.wav` / `SFX/freesound_community-heal-up…mp3` | `Movement.OnTriggerEnter`, `Health.Heal` | 2 clips + pitch |
| 4 | **Enemy wind-up** | `SFX/windup_rifle.wav` (0.6 s), `SFX/windup_mg.wav` (0.7 s), `SFX/windup_brawler.wav` (0.6 s) | `EnemyBase.PlayTelegraphSound` on the **first frame** of Aim / SpinUp / WindUp | fixed pitch (the player learns the sound) |
| 5 | Death (enemy / boss) | `SFX/enemy_death.wav`, boss: explosion | `EnemyBase.Die`, `Boss.Die` → `AudioManager.PlayAt` | pitch |
| 6 | UI click | `SFX/ui_click.wav`, `SFX/ui_click_2.wav` | `AudioManager` adds it to every Button / Toggle on scene load | 2 clips + pitch |
| 7 | Win | `win.wav` | `LevelRunner.Complete` → `Movement.PlayWinSound` → `AudioManager.PlayWin` | — |
| 8 | Lose | `SFX/lose.wav` | `Health.GameOver`, `LevelRunner` time's up → `AudioManager.PlayLose` | — |
| + | Shots | `Enemies/rifle_shot.wav`, `Enemies/mg_shot.wav`, player rifle | `Rifleman`, `MachineGunTurret`, `Shooter` | pitch |
| + | Bomb | explosion mp3 | `BombHazard` → `AudioManager.PlayAt` | pitch |

Enemy AudioSources: spatial blend 0.75, linear roll-off 6 → 45 m, so the wind-up is heard
before the enemy is on screen ("ears test").

## Who talks to whom

```
Always alive (created before the first scene, DontDestroyOnLoad)
  AudioManager ◀── Resources/SoundBank.asset (mixer, groups, snapshots, music, UI/win/lose clips)
     ├─ sceneLoaded ─▶ menu scene? menuMusic : levelMusic ─▶ PlayMusic()
     │                   same clip already playing → nothing (no restart)
     │                   other clip → 1 s crossfade A↔B (cos/sin curve, new track starts at volume 0 → no click)
     ├─ sceneLoaded ─▶ every Button/Toggle.onClick += PlayClick
     ├─ sceneLoaded ─▶ AudioSource without output → SfxBus; old scene music sources → disabled
     ├─ ApplyVolumes() ─▶ mixer.SetFloat("MusicVolume"/"SfxVolume", Log10(v) × 20)
     └─ SetPaused(bool) ─▶ Paused / Normal snapshot .TransitionTo()

Main menu
  ProfileSettings  Music slider ─▶ SetMusicVolume ─┐
                   SFX slider   ─▶ SetSfxVolume   ─┼─▶ SaveSystem.Settings ─▶ SaveSoon() (save.json)
                   Music toggle ─▶ SetMusic       ─┘   └─▶ SaveSystem.ApplyAudio() ─▶ AudioManager.ApplyVolumes()
  SaveSystemHook (start of the game) ─▶ SaveSystem.Load() ─▶ sliders + mixer get the saved values

Level
  PauseMenu.SetPaused ─▶ Time.timeScale = 0, AudioListener.pause (SFX stop)
                      ─▶ AudioManager.SetPaused (music keeps playing: ignoreListenerPause + Paused snapshot)
  Rifleman/MachineGunTurret/Brawler.Enter(Aim|SpinUp|WindUp) ─▶ EnemyBase.PlayTelegraphSound()
  Health.GameOver ─▶ AudioManager.PlayLose() + "You lose!" ─▶ reload after 1.6 s (the 2D lose sound survives the reload)
```

## Settings in save.json

```json
"settings": { "musicVolume": 0.8, "sfxVolume": 0.8, "musicOn": true, "fullscreen": true, "playerName": "", "playerColor": 0 }
```
The slider is stored linear (0..1); the mixer gets decibels: 1 → 0 dB, 0.5 → −6 dB, 0.25 → −12 dB, 0 → −80 dB.

## Tools

* `Tools → Cloud Strike → Build Sound Pass (Assignment 12)` — mixer, SoundBank, routes every AudioSource, enemy clips, SFX slider (re-run safe).
* `Tools → Cloud Strike → Smoke Test Sound Pass` — play-mode test, report in `Logs/SmokeTest/sound_report.txt`.
* `python Tools/gen_sounds.py` — re-creates every generated sound and the level music.
