# Sky Bot

A 3D mobile action platformer made in Unity 6 (6000.6.0f1) for Android, landscape.
A small robot runs, jumps and shoots across floating sky islands, collects every coin
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
- `Tools → Sky Bot → Rebuild Mobile UI` re-applies the layout to all scenes

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

## Docs
- Game Design Document: [`Docs/SkyBot_GDD.pdf`](Docs/SkyBot_GDD.pdf)
- AI use: [`AI_LOG.txt`](AI_LOG.txt)
