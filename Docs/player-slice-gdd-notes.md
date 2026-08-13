# Player Slice — Progress to MVP

Notes toward the Game Design Document. Covers the player/core-gameplay slice only
(Person 1). World scrolling, spawning, difficulty, UI, scoring and the game manager
belong to the other two slices and are referenced here only where they interface.

---

## 1. Scope of this slice

| Owned | Not owned |
|---|---|
| Player character and physics | World scrolling, parallax |
| Hold-to-thrust movement | Obstacle / coin / power-up spawning |
| Vertical speed clamping, screen containment | Difficulty ramp |
| Player animation states | HUD, score, distance, high score |
| Tag-based collision handling | Menus, game-over screen |
| Death sequence | Game manager, restart orchestration |
| Coin and power-up pickup | Audio asset authoring |
| Shield and power-up effects on the player | Particle effects in the world |

The governing constraint was **one-directional flow**: the player raises events
outward and never reads UI, scene loading, or the game manager. This is what allows
three people to work in one Unity project without editing the same files.

---

## 2. Core mechanic

Jetpack Joyride's defining feel is that the character is never *jumping* — it is
continuously fighting gravity. The design reflects that literally:

- **Hold to rise, release to fall.** One binary input. While held, an upward force is
  applied every physics tick. There is no jump impulse, no fuel budget, no cooldown.
- **Fixed horizontal position.** The player's X is locked; the world scrolls past.
  This is what makes it an endless runner rather than a platformer.
- **Bounded vertical space.** The player is clamped inside the visible play area
  rather than killed on leaving it, so losing the character off-screen is impossible.

### Design decision: clamp, not kill

Leaving the play area could either kill the player or contain them. Containment was
chosen because the ceiling and floor then become *tactical space* — you can ride the
ceiling to cross a low hazard field. Killing on bounds exit would make the top of the
screen a second hazard and punish an input the player is already holding.

Hitting a bound is explicitly non-fatal: position snaps to the limit and vertical
velocity zeroes, but alive state and shield state are untouched.

---

## 3. Tuning parameters

Every value below is Inspector-exposed and re-read each tick, so it can be adjusted
during play without recompiling. Defaults were chosen as a starting point and are
expected to change from playtest feel.

| Parameter | Default | Range | Design intent |
|---|---|---|---|
| Thrust force | 35 | 0.1–500 | Higher = punchier, more twitchy jetpack |
| Gravity scale | 3 | 0.1–20 | Higher = heavier fall, less forgiving |
| Max rise speed | 8 | 0.1–100 | Caps ascent so the player can't rocket uncontrollably |
| Max fall speed | 12 | 0.1–100 | Caps descent so there is always time to react |
| Play bounds Y | ±4 | — | Matches the orthographic camera's vertical view |
| Shield duration | 5 s | 1–60 | Long enough to matter, short enough to feel earned |
| Magnet duration | 5 s | 1–60 | As above |

**Deliberate asymmetry:** max fall (12) is faster than max rise (8). Falling should feel
heavier than rising, so releasing the button has weight and the jetpack feels like it is
working against something.

---

## 4. Architecture

Four components on one GameObject, each with a single responsibility.

| Component | Responsibility |
|---|---|
| `PlayerController` | Physics, input, alive/shield/magnet state, power-up timers, the public event surface, `ResetRun` |
| `PlayerAnimation` | Presents exactly one animation state. Reacts, never decides |
| `PlayerCollision` | Tag-based contact routing and the single collectible release point |
| `PlayerDeath` | The one-shot death sequence |

Two presentation components mirror published state without owning any:

| Component | Responsibility |
|---|---|
| `PlayerJetpackFlame` | Shows and cycles the exhaust while thrusting |
| `PlayerShieldVisual` | Shows a pulsing bubble while shielded |

### Fixed tick order

Order matters and is fixed, because several rules can fire on the same tick:

1. Apply thrust force (only while alive and held)
2. Clamp vertical velocity to the rise/fall bounds
3. Contain Y inside the play bounds; on contact, snap and zero vertical velocity
4. Lock X back to the spawn value and zero horizontal velocity

Clamping runs **before** containment, so a tick needing both ends at the containment
result of zero velocity rather than the clamped value. Step 4 runs unconditionally,
including while dead and while physics simulation is disabled, so the character can
never drift horizontally.

All physics writes happen in `FixedUpdate`. Animation polling happens in `Update` and
writes no physics.

---

## 5. Integration contract

The whole point of the slice. Other slices consume this and nothing else.

**Read-only state:** `IsAlive`, `IsShielded`, `IsThrusting`, `IsMagnetActive`

**Events raised outward:**

| Event | Meaning |
|---|---|
| `OnPlayerDeath` | Fired exactly once per run |
| `OnCoinCollected(int)` | Carries that coin's value |
| `OnPowerUpActivated(PowerUpType)` | A power-up became active |
| `OnPowerUpExpired(PowerUpType)` | Timer elapsed, shield consumed, or run ended |

**Operation:** `ResetRun()` returns the player to its initial state without reloading
the scene.

**Invariant:** every activation is matched by exactly one expiry, whether the power-up
ended by timer, by shield consumption, or by death. This lets the UI slice track
power-up timers without ever going out of sync.

Subscribers are invoked defensively — each one inside its own try/catch — so one
badly-behaved subscriber in another slice cannot break the player.

### What teammates need

**World/obstacles slice:** prefabs must carry the exact case-sensitive tags `Obstacle`,
`Coin`, `PowerUp_Shield`, `PowerUp_Magnet`, with trigger colliders. A coin should carry
a component implementing `ICoinValue` returning 1–1000; without one it falls back to a
default of 1.

**UI/systems slice:** subscribe to the four events, read the four state properties, call
`ResetRun()` to restart. `Assets/Scripts/_Sandbox/SandboxDebugHud.cs` is a working
reference implementation using exactly that surface.

---

## 6. Rules that needed explicit decisions

These are the cases where "obvious" behaviour was ambiguous and a choice was recorded.

**A coin counts exactly once.** Instances are tracked by object ID and marked consumed
*before* the event is raised, so overlapping contact callbacks in the same physics step
cannot double-count.

**A shield absorbs exactly one hit.** Absorbed obstacles are marked, so repeat callbacks
from the same obstacle consume no further shield. Two *different* obstacles in the same
step consume one shield and then kill, which is the fair reading.

**Re-activating an active power-up refreshes the timer** rather than stacking, and raises
no second activation event. Stacking durations would let a player bank power-ups.

**Death fires once regardless of contact count.** A single hit can produce several
collider callbacks. A guard makes the whole sequence atomic, and it also covers the case
where a subscriber reacts to a power-up expiry by requesting death again.

**A held button does not survive a reset.** After `ResetRun`, thrust counts as released
until the player physically presses again. Otherwise a player holding the button through
a restart would launch instantly with no input.

**A sub-tick tap still thrusts.** A press and release landing between two physics ticks
produces exactly one tick of thrust, so fast taps are never silently dropped.

---

## 7. Testing approach

A temporary sandbox scene (`Assets/Scenes/PlayerSandbox.unity`) stands in for the
unfinished world slice so the player can be tuned in isolation. It provides a stand-in
scroller spawning tagged obstacles, coins and power-ups, plus a debug overlay showing
live state, event counts and the last event fired.

Hotkeys: `Space` thrust, `R` reset, `K` force death, `1`/`2` grant shield/magnet.

This exists to make the feel adjustable, and doubles as a live check on the integration
contract, since the overlay consumes only the public surface. It is isolated under
`Assets/Scripts/_Sandbox/` and `Assets/Resources/` is not used by it, so the folder can
be deleted in one action before submission.

---

## 8. Assets

All art is by **Kenney** under **CC0 1.0** (public domain). Full attribution, the packs
used, and a record of sources evaluated and rejected on licensing grounds is in
`Assets/Art/ATTRIBUTION.md`.

Six GitHub Jetpack Joyride clones were evaluated as asset sources and rejected: four had
no licence at all (default copyright, all rights reserved), and the two carrying MIT and
Apache licences appeared to contain Halfbrick Studios artwork that the uploaders had no
right to sub-licence. CC0 assets were used instead, which also gave better visual
consistency than mixing rips from six projects.

*Jetpack Joyride* is a trademark of Halfbrick Studios. This is an educational clone of
the mechanics using no Halfbrick assets or trademarks.

---

## 9. Current state and next steps

**Working:** thrust and fall, speed clamping, bounds containment, fixed-X lock, three
animation states, tag routing, coin pickup with per-coin values, both power-ups with
independent timers, shield absorption, one-shot death, reset, jetpack flame, shield
bubble, and the four outward events.

**Outstanding in this slice:**

- Magnet has no effect yet — it publishes state but nothing pulls coins in. Needs an
  owner agreed with the world slice, since the coins belong there.
- Shield-break and death cues are wired as empty events, pending audio assets.
- Death is visually thin: a pose change with no knockback or fade.
- Animation states are single-frame poses rather than multi-frame loops.
- **Values are untuned.** Every number in section 3 is a starting default. Tuning by
  feel is the main remaining design work.

**Known risks:**

- `ProjectSettings/TagManager.asset`, `EditorSettings.asset` and the shared scene are
  edited by all three slices and are the most likely merge conflicts.
- The scene is still named `SampleScene.unity`; the team intended `Main.unity`.
