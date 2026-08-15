# Third-Party Asset Attribution

All visual assets in `Assets/Art/` are by **Kenney** and are released under
**CC0 1.0 Universal (Public Domain Dedication)**.

- Author: Kenney Vleugels — Kenney (<https://kenney.nl>)
- Licence: CC0 1.0 Universal — <https://creativecommons.org/publicdomain/zero/1.0/>
- Canonical source: <https://kenney.nl/assets>
- Also mirrored on itch.io: <https://kenney-assets.itch.io>

Under CC0 these assets are dedicated to the public domain. Attribution is **not
legally required**, and there is no restriction on commercial or academic use.
We credit Kenney here because it is good practice and because our module is
assessed in part on referencing.

## Packs used

| Pack | Canonical page | Used for |
|---|---|---|
| Platformer Characters | <https://kenney.nl/assets/platformer-characters> | Player poses (3 character variants) |
| Jumper Pack | <https://kenney.nl/assets/jumper-pack> | Coins, power-up icons, spike hazards |
| Platformer Pack Redux | <https://kenney.nl/assets/platformer-pack-redux> | Gems, star, saw hazards |
| Space Shooter Redux | <https://kenney.nl/assets/space-shooter-redux> | Laser/zapper sprites, shield effect, jetpack flame frames |
| Background Elements Redux | <https://kenney.nl/assets/background-elements-redux> | Parallax background layers |

Files were obtained via the community mirror
<https://github.com/ETdoFresh/kenney.nl> for convenience. That mirror is
unofficial; the licence and authorship above derive from Kenney as the original
author, not from the mirror.

## Folder map

| Folder | Contents | Source pack path |
|---|---|---|
| `Player/Player/` | 24 poses | `kenney_platformercharacters/PNG/Player/Poses` |
| `Player/Adventurer/` | 24 poses | `kenney_platformercharacters/PNG/Adventurer/Poses` |
| `Player/Female/` | 24 poses | `kenney_platformercharacters/PNG/Female/Poses` |
| `Player/Jetpack/` | 8 flame frames (fire00-07) | Space Shooter Redux `PNG/Effects` |
| `Coins/` | 3 coin tiers, 4 gems, 1 star | Jumper Pack `PNG/HUD`, Platformer Pack Redux `PNG/Items` |
| `PowerUps/` | 5 power-up icons, 3 shield frames, 2 star frames | Jumper Pack `PNG/Items`, Space Shooter Redux `PNG/Effects` |

## Derived files

Files we generated from Kenney originals. CC0 places no restriction on
modification, so these carry the same licence as their source.

| Derived file | Source | Made by | How |
|---|---|---|---|
| `Assets/Resources/PlayerGear/Jetpack/jetpack.png` | `Assets/Art/PowerUps/powerup_jetpack.png` (Kenney, CC0) | `Assets/Editor/JetpackSpriteBuilder.cs` | The source is a 71x70 round UI badge: a blue disc with a white jetpack pictogram on it. The builder keeps only pixels at or above 235 on every channel, discards the disc, and crops to the pictogram's ink box at x[25..44] y[20..50] top-down, giving a 20x31 white-on-transparent sprite that is tinted at runtime. |

The original `powerup_jetpack.png` is unmodified and stays where it is. Re-run
the derivation from **Tools > Jetpack > Rebuild worn jetpack sprite**.
| `Hazards/` | 27 laser sprites, 4 spikes, 6 saw sprites | Space Shooter Redux `PNG/Lasers`, Jumper Pack `PNG/Environment`, Platformer Pack Redux `PNG/Enemies` |
| `Backgrounds/` | 8 full backgrounds, 12 parallax elements | Background Elements Redux `Backgrounds` |

## Animator state mapping

The `Player.controller` states are driven by name from `PlayerAnimation.cs`.
Suggested single-frame mapping using `Assets/Art/Player/Player/`:

| Animator state | Sprite |
|---|---|
| `Flying` | `player_jump.png` |
| `Falling` | `player_fall.png` |
| `Death` | `player_hurt.png` |

The same three pose names exist in the Adventurer and Female sets, so the
character can be swapped by repointing the clips at a different folder.

## Sources reviewed but NOT used

The following repositories were evaluated as asset sources and deliberately
excluded. Four carry no licence, which under GitHub's terms means all rights
reserved — citation does not grant permission to copy. The two that are licensed
appear to contain artwork owned by Halfbrick Studios (the original *Jetpack
Joyride* rights holder), which the uploaders could not sub-licence.

| Repository | Licence | Excluded because |
|---|---|---|
| `Turner-Christian/JetpackJoyride` | none | No licence — all rights reserved |
| `TaylanCeylan/JetpackJack` | none | No licence — all rights reserved |
| `CODEX6975/Jetpack-Joyride` | none | No licence — all rights reserved |
| `dvarshney1/Jetpack` | none | No licence — all rights reserved |
| `n3sfan/JetpackJoyride2` | Apache-2.0 | Licence cannot cover third-party (Halfbrick) artwork |
| `ivaylokenov/Jetpack-Joyride-Unity-3D` | MIT | Licence cannot cover third-party (Halfbrick) artwork |

*Jetpack Joyride* is a trademark of Halfbrick Studios. This project is an
educational clone of the game's mechanics and uses no Halfbrick artwork,
audio, or trademarks.
