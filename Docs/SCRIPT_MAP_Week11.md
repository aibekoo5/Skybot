# Sky Bot — Script map, Week 11 (Data and Saves)

## Data assets (balance lives here, not in code)

| Asset | Class | Who reads it |
|---|---|---|
| `Assets/Data/PlayerStats.asset` | `PlayerStats` | `Movement`, `Health`, `Shooter` → `Bullet` |
| `Assets/Data/Enemies/RiflemanStats.asset` | `RiflemanStats : EnemyStats` | `Rifleman` prefab |
| `Assets/Data/Enemies/MachineGunStats.asset` | `MachineGunStats : EnemyStats` | `MachineGunTurret` prefab |
| `Assets/Data/Enemies/BrawlerStats.asset` | `BrawlerStats : EnemyStats` | `Brawler` prefab |
| `Assets/Data/Enemies/PatrollerStats.asset` | `PatrollerStats` | `Patrol` (Spike Bridge level) |
| `Assets/Data/BossStats.asset` | `BossStats` | `Boss` → `BossProjectile` |
| `Assets/Data/ItemValues.asset` | `ItemValues` | `Hazard`, `Rock`, `Heart`, `Spikes`, `BombHazard`, `Movement` (contact damage), `LevelRunner` (score rules) |
| `Assets/Data/Levels/Level_01..04.asset` | `LevelData` | `LevelRunner` |
| `Assets/Resources/LevelList.asset` | `LevelList` | `GameSession`, `LevelRunner`, `LevelSelectMenu` |

## Who talks to whom

```
Main menu
  MenuButtons ── PLAY ──▶ LevelSelectMenu ── reads ──▶ LevelList (Resources) + SaveSystem (unlocked, best)
       │                        │ card click
       │                        ▼
       │                  GameSession.Play(LevelData) ── loads LevelData.sceneName
       │                  RESET PROGRESS ─▶ confirm panel ─▶ SaveSystem.ResetProgress()
  ProfileSettings / slider ─▶ SaveSystem.Settings + SaveSoon()

Level scene (same code in every level)
  LevelRunner.Awake ── GameSession.ResolveForActiveScene() ─▶ LevelData
       ├─ spawns EnemySpawn list (prefab + position + yaw + patrol points)
       ├─ creates a Spawner per HazardWave
       ├─ runs the timer (timeLimit) and writes Movement.timerText
       └─ listens to EnemyBase.Died (score + "defeat all enemies")
  Movement ── coin ─▶ LevelRunner.OnCoinCollected()
           ── exit ─▶ LevelRunner.CanExit(goal) ─▶ Complete()
  Boss.Die ─▶ LevelRunner.AddScore() + Complete()
  LevelRunner.Complete ─▶ SaveSystem.CompleteLevel(n, score)  (unlock n+1, best score, write file)
                       ─▶ next LevelData from LevelList, or Win scene

Always alive (created before the first scene, DontDestroyOnLoad)
  SaveSystemHook ── Update ─▶ SaveSystem.Tick() (delayed settings save)
                 ── OnApplicationPause(true) / OnApplicationQuit ─▶ SaveSystem.Save()
                 ── sceneLoaded ─▶ SaveSystem.ApplyAudio() (music volume / mute)
```

## Save file

* Path: `Application.persistentDataPath/save.json`
  (Android: `/storage/emulated/0/Android/data/<package>/files/save.json`, Windows editor:
  `%USERPROFILE%/AppData/LocalLow/<company>/<product>/save.json`).
* Format: `JsonUtility` (no BinaryFormatter). Lists, not Dictionary (JsonUtility skips Dictionary).

```json
{
    "version": 1,
    "unlockedLevels": [1, 2],
    "records": [ { "level": 1, "bestScore": 1250 } ],
    "settings": { "musicVolume": 0.8, "musicOn": true, "fullscreen": true, "playerName": "Aibek", "playerColor": 0 }
}
```

* Missing file → defaults (`SaveLoadResult.NewGame`).
* Broken file (bad JSON, empty file) → `try/catch`, defaults, broken copy kept as `save_broken.json`,
  the menu shows "Save file was damaged - started with default values".
* Bad values (negative level, volume 7, null lists) → `SaveData.Repair()` fixes them; level 1 is always unlocked.
* Writing: `save.json.tmp` first, then replace — closing the app mid-write never destroys the old save.

## When it saves

| Moment | Code |
|---|---|
| Level completed | `LevelRunner.Complete` → `SaveSystem.CompleteLevel` → `Save()` |
| Settings changed | `ProfileSettings` / `MenuButtons.SetVolume` → `SaveSoon()` (0.5 s later, once) and `Back` → `Save()` |
| App goes to background (Home, call, app switch) | `SaveSystemHook.OnApplicationPause(true)` |
| App quits | `SaveSystemHook.OnApplicationQuit`, `MenuButtons.Quit` |
| Reset progress confirmed | `SaveSystem.ResetProgress` |
