# ⚔️ Vanguard of Darkness

> **2D Pixel Art Platformer — Solo & Online Co-op**  
> Developed for *Programación de Videojuegos IEI-061* · Iquique, Chile · 2026

![Unity](https://img.shields.io/badge/Unity-6000.4.0f1-black?logo=unity)
![Netcode](https://img.shields.io/badge/Netcode_for_GameObjects-NGO-blue)
![Relay](https://img.shields.io/badge/Unity_Relay-UGS-orange)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
---

## 📖 Overview

Vanguard of Darkness is a 2D pixel art platformer with two distinct play modes:

- **Solo** — race through levels, defeat enemies, and beat your best time.
- **Co-op Online** — Player 1 fights while Player 2 plays as a **fairy companion** who possesses enemies, applies buffs, and heals — all over the internet via Unity Relay.

The cooperative mode requires zero router configuration. The Host shares a 6-character room code and the Client types it in to join instantly.

---

## 🎮 Controls

### Player 1 — Platformer

| Action | Keys |
|--------|------|
| Move | `A` / `D` |
| Jump / Double Jump | `W` · `Space` · `↑` |
| Wall Slide / Wall Jump | Push against wall + Jump |
| Dash | `Shift` or `Right Click` |
| Attack | `Left Click` or `J` |
| Interact (NPC) | `E` |
| Pause | `Escape` |

### Player 2 — Fairy (Co-op only)

| Action | Keys |
|--------|------|
| Possess enemy | `Right Click` on enemy |
| Control possessed enemy | `A` / `D` |
| Release possession | `Right Click` again |
| Speed Buff → P1 | `Q` |
| Damage Buff → P1 | `E` |
| Heal P1 | `R` |

> P2 flies alongside P1 as a kinematic fairy — she passes through walls and cannot take damage. Possessing the **Knight boss** is not allowed. After releasing a possession there is a **60-second cooldown**.

---

## 🗺️ Levels

| Scene | Name | Boss Required | Notes |
|-------|------|---------------|-------|
| 0 | Main Menu | — | Relay lobby, scoreboard, options |
| 1 | Level 1 | None | Intro level — Slimes & Fallen Angels |
| 2 | Level 2 | LongSword Knight | Defeat the Knight to unlock the exit |
| 3 | Level 3 | Boss Demon | Final level — victory screen on kill |

---

## 👹 Enemies

| Enemy | Type | Special |
|-------|------|---------|
| **Slime** (`EnemyFollow`) | Ground | Edge detection — won't fall off platforms |
| **Fallen Angel** (`EnemyFlyingShooter`) | Flying | Sinusoidal Y movement + projectile attacks |
| **LongSword Knight** (`EnemyLongSwordKnight`) | Ground Boss | Area attack with wind-up. Cannot be possessed |
| **Demon Boss** (`BossDemon`) | Final Boss | Required kill for co-op victory |

All enemies:
- Spawn at runtime via **EnemySpawner** (no manual scene placement).
- Patrol autonomously ±`patrolDistance` units from their spawn point — no waypoints needed.
- Run their AI exclusively on the server (`IsServer`-gated), with effects replicated via `ClientRpc`.
- Reward the team by **reducing the shared timer** on death.

---

## ⚡ Timer & Scoring

The timer counts **up** (elapsed time). Lower is better.

| Event | Timer Effect |
|-------|-------------|
| P1 dies | **+90 seconds** (both clients) |
| Enemy killed | **−X seconds** (configured per enemy) |
| Level complete | Time saved to **Scoreboard** if a new record |

Records are stored per level in `PlayerPrefs` and displayed in the main menu Scoreboard.

---

## 🛠️ Tech Stack

```
Unity 6 (6000.4.0f1)
├── Netcode for GameObjects (NGO)   — server-authoritative multiplayer
├── Unity Relay (UGS)               — NAT traversal, no port forwarding
├── Unity Authentication            — anonymous sign-in
├── TextMeshPro                     — all in-game UI text
└── Tilemap system                  — Rule Tiles, Composite Colliders
```

### Architecture Highlights

- **`PlayerController`** is a **partial class** split across 3 files: lifecycle/HUD/RPCs, movement physics, and combat/buffs.
- **`EnemyBase`** centralises health (`NetworkVariable<int>`), the autonomous patrol system, knockback, stun, death fade-out, and despawn — subclasses override `SetFacing()` and `CheckPatrolGroundAhead()`.
- **`CoopManager`** uses `DontDestroyOnLoad` and cannot be a `NetworkObject`. It relays messages to the Client through `PlayerController` (which *is* spawned), via methods like `MostrarVictoriaClientRpc` and `CargarSiguienteNivelClientRpc`.
- **Timer sync** — `GameManager` runs independently on each machine. Events (P1 death, enemy death) call `ModificarTiempo()` locally on every client through existing `ClientRpc` paths.
- **P2 HUD** is discovered at runtime with `GameObject.Find()` because `Player2Controller` is a network prefab spawned at runtime — Inspector drag-and-drop is not possible.
- **Enemy spawn delay** — in Relay mode, players spawn after 1.5 s and enemies after 2.5 s (`WaitForSecondsRealtime`) to ensure the client NGO context is ready before receiving spawn messages.

---

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── PlayerController.cs           # NGO lifecycle, HUD, RPCs
│   │   ├── PlayerControllerMovement.cs   # Physics, dash, jump, wall-jump
│   │   ├── PlayerControllerCombat.cs     # Attack, knockback, co-op buffs
│   │   ├── Player2Controller.cs          # Fairy: possession + buffs
│   │   ├── PlayerSoundController.cs
│   │   ├── PlayerEffectsController.cs
│   │   └── PlayerTargetFinder.cs         # Static P1 cache for enemies
│   ├── Enemy/
│   │   ├── EnemyBase.cs                  # Base class + patrol system
│   │   ├── EnemyFollow.cs                # Slime
│   │   ├── EnemyLongSwordKnight.cs       # Knight boss
│   │   ├── EnemyFallenAngel.cs           # Flying shooter
│   │   ├── EnemyProjectile.cs
│   │   ├── EnemySpawner.cs               # Runtime network spawning
│   │   └── SpawnPointConfig.cs           # Per-point enemy type config
│   ├── Managers/
│   │   ├── GameManager.cs                # Timer, pause, win/lose
│   │   ├── CoopManager.cs                # Co-op session, level transitions
│   │   ├── CoopNetworkManager.cs         # NGO lifecycle, player spawning
│   │   ├── RelayManager.cs               # UGS Relay wrapper
│   │   ├── MainMenuManager.cs            # Menu, lobby UI
│   │   ├── SaveSystem.cs                 # JSON save/load (solo only)
│   │   ├── RecordSystem.cs               # Best times per level
│   │   ├── LoadingScreenManager.cs
│   │   └── OptionsManager.cs
│   ├── NPC/
│   │   └── Dialogue.cs
│   └── Parallax/
│       ├── Parallax.cs
│       └── ColumnParallax.cs
└── Scenes/
    ├── 0 - MainMenu
    ├── 1 - Level1
    ├── 2 - Level2
    └── 3 - Level3
```

---

## 👥 Team

| Name | Role |
|------|------|
| **Benjamín Matías Pavez Vidal** | Developer |
| **Nicolás Renato Ramírez Berríos** | Developer |

**Course:** Programación de Videojuegos — IEI-061  
**Instructor:** Leopoldo Esteban Rodríguez Bravo  
**Institution:** Santo Tomas Iquique · 2026

---

*GDD v3.0 available in the repository root.*
