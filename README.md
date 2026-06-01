# Grid Siege

**Name:** 이건행  
**Student ID:** 20240483  
**Course:** CS-20200 Programming Principles, Spring 2026  
**GitHub:** https://github.com/rjsgod/defense-game

---

## Overview

Grid Siege is a real-time strategy castle defense game built with **F# (.NET 10)** and the **Raylib-cs** library. Inspired by Plants vs. Zombies, the player places towers on a 5×9 grid to stop waves of enemies from reaching and destroying the castle. The game runs in endless wave mode until the castle's HP reaches 0.

---

## How to Run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 or later

### Steps

```bash
git clone https://github.com/rjsgod/defense-game.git
cd defense-game
dotnet run
```

The game window (1200×750) will open. Click **START** on the title screen to begin.

### Required Asset Structure

All asset folders must be placed in the **project root** (same directory as the `.fsproj` file):

```
defense-game/
├── background/
│   └── grass.png
├── tower/
│   ├── basic.png
│   ├── speed.png
│   ├── range.png
│   ├── bullets.png
│   └── boss tower.png
├── fonts/
│   └── LuckiestGuy-Regular.ttf
├── Green_Slime/
│   ├── Idle.png
│   ├── Attack_1.png
│   └── Hurt.png
├── Blue_Slime/
│   ├── Idle.png
│   ├── Attack_1.png
│   └── Hurt.png
└── Red_Slime/
    ├── Idle.png
    ├── Attack_1.png
    └── Hurt.png
```

> If any asset file is missing, the game will fall back to rendering colored shapes instead of sprites. The game remains fully playable without assets.

---

## Controls

| Input | Action |
|-------|--------|
| `1` | Select Basic Tower |
| `2` | Select Rapid Tower |
| `3` | Select Area Tower |
| Left Click (grid cell) | Place selected tower |
| Left Click **RESTART** | Restart after Game Over |
| Left Click **GO TO MENU** | Return to title screen |

---

## Gameplay

- Enemies (mobs) spawn from the rightmost column and move west toward your castle.
- Place towers on any grid cell **except the rightmost column**.
- Towers automatically attack the nearest enemy in the same row.
- Income is earned passively (+1 gold/sec) and by defeating enemies.
- Waves are endless — defend until the castle HP (1000) reaches 0.

### Tower Stats

| Tower | Cost | ATK | HP | Attack Cooldown | Special |
|-------|------|-----|----|-----------------|---------|
| Basic | 50 | 25 | 100 | 1.0s | Standard single-target |
| Rapid | 100 | 20 | 70 | 0.5s | Fast attack rate |
| Area | 150 | 50 | 150 | 2.5s | Hits all enemies in 3×3 area around target |

### Enemy Stats

| Enemy | Reward | ATK | HP | Attack Range | Notes |
|-------|--------|-----|----|--------------|-------|
| Basic Mob | 8 | 10 | 150 | 1 cell | Standard melee |
| Ranged Mob | 16 | 30 | 100 | 3 cells | Fires projectiles |
| Tanker Mob | 24 | 25 | 800 | 1 cell | Slow, very high HP |

### Wave System

- Wave 1 starts with 3 mobs. Each subsequent wave adds 2 more mobs.
- Mobs spawn one at a time in a randomly chosen row.
- After all mobs in a wave are defeated, a 3-second delay precedes the next wave.
- Spawn intervals might decrease as waves progress.

---

## Requirement Changes from Proposal

There are no changes from the submitted proposal. All requirements described in the proposal have been implemented as specified.

---

## LLM Usage

Three LLM tools were used at different stages of development: Gemini for prototype logic and rendering, and ChatGPT for visual asset generation.

---

### Gemini — Prototype Logic and Rendering

**What it was used for:**

Gemini was used to implement projectile collision and damage handling. The goal was to make projectiles disappear the moment they reach a mob while simultaneously applying damage. This covers both tower projectiles and mob projectiles — the `updateProjectiles` and `updateMobProjectiles` functions, including the distance check (`if distance <= step || distance < 10.0f`), the hit target lookup, and the HP reduction logic.

Gemini was also used for per-frame animation rendering of towers and mobs. This includes the sprite frame slicing logic — `towerSourceRect`, which computes each frame's `Rectangle` from the sprite sheet width divided by frame count, and `bulletSourceRect`, which defines the source rectangle for each bullet type within the shared `bullets.png` sheet. On the mob side, `updateMobAnimation` — the frame counter increment, the `FrameTimer` threshold check, and the action state reset back to `Idle` after an attack or hurt animation completes — was also drafted with Gemini's assistance.

Additionally, most of `StartScreen.fs` was drafted using Gemini, including the countdown timer logic, button hover detection, the transition into `runGame()`, and the return-to-menu flow.

**What required reprompting or manual correction:**

The tower attack animation had a timing mismatch where the firing frame did not align with the moment the projectile was spawned, creating a visually awkward result. Several rounds of reprompting were needed to align `AnimFrame`, `AnimTimer`, and `AttackTimer` resets in `updateTowers`.

Health bar drawing for both towers (`drawTowerHealthBar`) and mobs (`drawMob`) required reprompting. Early versions used fixed bar widths or incorrect anchor points, so the bars did not scale with HP or align properly above the sprites.

Font color and UI panel styling — the `drawInfoPanel` helper, the layered rectangle drawing for the top UI bar, and color values like `labelColor`, `valueColor`, and `panelFill` — went through multiple iterations before reaching the current appearance.

The wave spawn interval formula required repeated adjustment:

```fsharp
let mutable wavebase = 2.0f - (float32 (gs.Wave - 1) * 0.08f)
if wavebase < 0.4f then wavebase <- 0.4f
let randomoffset = float32 (rand.NextDouble() * 1.0 - 1.0)
wavebase <- wavebase + randomoffset
if wavebase < 0.2f then wavebase <- 0.2f
```

Getting the difficulty curve to feel natural — fast enough in later waves without becoming unplayable, with enough randomness to feel organic — required prompting several times with different coefficient and clamp values before settling on the current formula.

**What it could not do correctly:**

Gemini was unable to produce clean and accurate placement of the grid, towers, and mobs on screen. Despite multiple prompts requesting precise alignment, the generated coordinate calculations were consistently off. All pixel-level positioning — the exact values of `MARGIN_X`, `MARGIN_Y`, `CELL_SIZE`, the castle position (`castleX`, `castleY`), and the sprite destination rectangles — was corrected manually by adjusting X and Y values directly. Coordinate calculations in the screen rendering sections that appeared frequently were also written by hand, as the generated values did not produce the intended layout.

---

### ChatGPT — Background, Tower, and Bullet Images

**What it was used for:**

ChatGPT was used to generate all visual assets: the grass background (`background/grass.png`), the three tower sprites (`tower/basic.png`, `tower/speed.png`, `tower/range.png`), and the bullet sprite sheet (`tower/bullets.png`).

**What required reprompting or manual correction:**

The background image was regenerated multiple times to match the green grass tone used as the window clear color (`Color(132, 190, 70, 255)`), so the texture would blend naturally with the base color rather than creating a harsh visual boundary at the grid edge.

The tower sprites were initially generated with white backgrounds. Requests for transparent backgrounds were not reliably fulfilled, so white-to-transparent conversion was handled in code via `loadTextureWithWhiteTransparent`, which calls `Raylib.ImageColorReplace(&image, Color.White, Color.Blank)` before uploading the texture to the GPU.

The bullet sprite sheet required the most iteration. Each bullet type — Basic (`Rectangle(38.0f, 230.0f, 125.0f, 56.0f)`), Rapid (`Rectangle(200.0f, 235.0f, 118.0f, 54.0f)`), and Area (`Rectangle(372.0f, 208.0f, 90.0f, 95.0f)`) — needed to sit at a predictable position within the shared sheet. Early versions placed bullets at inconsistent positions and sizes. The sheet was regenerated several times, and the final source rectangles in `bulletSourceRect` and display sizes in `bulletSize` were determined by manually measuring pixel coordinates in an image editor.

**What it could not do correctly:**

ChatGPT was never able to produce a bullet sprite sheet where all three bullet types had consistent, predictable positions matching the requested layout. Every generated sheet had slight variations that did not match the specification. The final solution was to accept a sheet that was close enough and hardcode the exact measured pixel coordinates directly into the source code.

