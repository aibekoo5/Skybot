# DECISIONS.md — Sky Bot

## Week 10 — Three Enemy Types

2026-11-9 Asked for a mouse/touch orbit camera and a fixed centre crosshair. Shots first went along the camera pitch into the ground; changed to "raycast from screen centre, bullet flies from muzzle to that point" + over-the-shoulder offset so the hero does not cover the crosshair.
2026-11-9 Lowered jump 6 → 5 (≈1.8 m → 1.27 m). Level steps were too high after that, so I re-heighted them (max rise ≤ 0.7 m). Also: ramp blocked jumping because the code required velocity.y ≤ 0.1 — replaced with a 0.25 s jump cooldown.
2026-11-10 Shooting while running bowed the head and raised the rifle (firing clip over a leaning run). Removed the firing clip, added an aim-walk blend tree (walk/back/strafe) and a chest aim correction toward the crosshair.
2026-11-10 Asked for three enemies with FSMs: Patroller (Patrol/Chase/Search/WindUp/Attack/Cooldown), Shooter (Guard/Aim/Fire/Cooldown/Retreat), Charger (Idle/Track/WindUp/Charge/Stunned/Recover). Shared EnemyBase only senses (distance + angle + Raycast); behaviour is separate per enemy. Attacks start only when the enemy is on screen.
2026-11-10 Changed the design myself: the first version had only one enemy that shoots. Now: Rifleman (Swat skin, walks, chases and shoots), Machine gun (cannot move, bursts of 8, turns slowly while firing so you can run sideways), Brawler (Swat skin + Boxing clip, runs 6.5 m/s vs Rifleman 2.8, punches up close). My hero is now Y Bot so enemies are easy to tell apart.

## Playtest (Android phone, 2 testers)

| Tester | Tries | What killed them most |
|---|---|---|
| Tester 1 (friend) | 3 | Brawler — the punch came too fast, no time to step back |
| Tester 2 (friend) | 4 | Machine gun — could not escape the burst by running sideways |

Changed numbers after playtest (old → new):
- Brawler windUp 0.45 s → 0.6 s — testers saw the red flash but could not react in time. Now stepping back works.
- Machine gun burstTurnSpeed 55°/s → 40°/s — the burst followed the player too well, so running sideways did not help. Now the dodge works.

Result: both testers finished the level on the next try and said the enemies felt fair.
