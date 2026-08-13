# Prompt for Gemini — Individual Feature Documentation

Copy everything below the line into Gemini.

---

## ROLE AND TASK

You are helping a third-year game design student write the **individual feature
documentation** section of a Game Design Document. This is university coursework at
honours-adjacent level. It will be marked by a lecturer who teaches from Ian Bogost's
*Unit Operations*, Anna Anthropy and Naomi Clark's *A Game Design Vocabulary*, and
Heather Maxwell-Gardner's *The Game Production Handbook*.

Write in the student's voice: first person, past tense for decisions made, present tense
for how the system currently works. Write as a designer explaining reasoning, not as a
developer listing features.

**Author:** Ako Baloyi
**System owned:** Player Controller and all player-side behaviour — vertical movement,
animation state, collision routing, death sequence, collectible interaction, power-up
effects on the player.
**Group:** Clone 2, Group 21. Linky Nzuza owns the world/obstacle/spawning systems.
Unarine Maswime owns UI, scoring, and game state.
**Engine:** Unity 6000.0.53f1, 2D URP, C#.

---

## THE SINGLE MOST IMPORTANT FRAMING INSTRUCTION

The group's investigation is about **distance-based difficulty progression**. The group
hypothesis is:

> Progressively increasing obstacle density and complexity as the player travels further
> will sustain engagement by providing increasingly demanding gameplay while allowing
> players to adapt through repeated play.

The group document explicitly states that the Player Controller is **a controlled system
within the investigation rather than the primary experimental variable.**

This is the spine of the entire section you are writing. Do not frame the player
controller as the exciting or expressive part of the game. Frame it as **the control
variable in an experiment** — and argue that this is a harder and more disciplined design
brief than making a fun controller.

The argument to develop and sustain throughout:

> Because difficulty is the independent variable, my system's design requirement was
> **mechanical invariance**. If the controller's response drifts between runs, any
> observed change in player performance could be caused by inconsistent movement rather
> than by the difficulty curve — which would invalidate the group's findings. My design
> decisions were therefore made in service of **measurement validity** rather than feel
> alone. Every guarantee my system makes exists so that two runs are comparable.

Explicitly connect these specific engineering guarantees to experimental validity. This is
the highest-value content in the whole section:

- **Death fires exactly once per run** → run boundaries are unambiguous, so recorded
  distance is trustworthy. If one collision produced two death events, run counts and
  retry-rate data would be corrupted.
- **Reset restores an identical starting state** → run *n+1* begins in the same condition
  as run *n*, so distance across attempts is comparable. Learning can be attributed to the
  player rather than to a drifting start state.
- **A coin counts exactly once** → score is trustworthy, and score is a dependent variable.
- **Player feedback does not intensify with difficulty** → matches the group's stated
  control for presentation confounds (group doc 3.6). The jetpack flame and shield bubble
  look identical at high and low difficulty by design.
- **Horizontal position is locked every physics tick without exception** → distance is a
  pure function of elapsed time and scroll speed, so it is a valid progression measure.

---

## VERIFIED FACTS — USE THESE, DO NOT INVENT OTHERS

Everything in this section is true of the actual implementation. Do not add features,
numbers, or systems that are not listed here. If you need a detail that is not provided,
leave a clearly marked `[TO CONFIRM]` placeholder instead of inventing it.

### Components built (six, on one GameObject)

| Component | Responsibility |
|---|---|
| `PlayerController` | Physics, input, alive/shield/magnet state, power-up timers, public event surface, `ResetRun()` |
| `PlayerAnimation` | Presents exactly one of three animation states. Reads state, never decides it |
| `PlayerCollision` | Tag-based contact routing, single collectible release point |
| `PlayerDeath` | One-shot death sequence |
| `PlayerJetpackFlame` | Exhaust visual while thrusting |
| `PlayerShieldVisual` | Bubble visual while shielded |

`PlayerController` is the only component that owns state. All others read it or request
changes through it.

### Tunable parameters (all Inspector-exposed, all re-read every physics tick)

| Parameter | Default | Accepted range |
|---|---|---|
| Thrust force | 35 | 0.1–500 |
| Gravity scale | 3 | 0.1–20 |
| Max rise speed | 8 | 0.1–100 |
| Max fall speed | 12 | 0.1–100 |
| Play bounds min Y | −4 | — |
| Play bounds max Y | +4 | — |
| Shield duration | 5 s | 1–60 |
| Magnet duration | 5 s | 1–60 |

Thrust is applied as `AddForce(Vector2.up * thrustForce, ForceMode2D.Force)` on a
`Rigidbody2D` with `bodyType = Dynamic` — a continuous force, not a velocity impulse and
not a position change. Unity 6 requires `Rigidbody2D.linearVelocity`; the older
`.velocity` property is obsolete.

### Fixed physics tick order

1. Re-apply gravity scale (so Inspector tuning takes effect next tick)
2. Sample input: currently-held state plus a "pressed since last tick" edge flag
3. Apply post-reset latch (see below)
4. Apply thrust force, only while alive and held
5. Guard against non-finite velocity (NaN or infinity → zero, warn once per run)
6. Clamp vertical velocity to `[−maxFall, +maxRise]`
7. Contain Y inside play bounds; on contact, snap Y to the limit and zero vertical velocity
8. Lock X to the spawn value and zero horizontal velocity — **unconditionally**, including
   while dead and while physics simulation is disabled
9. Tick power-up timers, only while alive

Clamping runs **before** containment deliberately: a tick needing both should end at the
containment result of zero velocity. If containment ran first, the clamp would reintroduce
velocity into a body resting against a bound and the character would jitter at the ceiling.

### Public interface consumed by teammates

Read-only state: `IsAlive`, `IsShielded`, `IsThrusting` (defined as `IsAlive && held`),
`IsMagnetActive`.

Events raised outward:
- `OnPlayerDeath`
- `OnCoinCollected(int value)`
- `OnPowerUpActivated(PowerUpType)`
- `OnPowerUpExpired(PowerUpType)`

Operation: `ResetRun()` restores the initial state without reloading the scene.

Invariant: every activation is matched by exactly one expiry, whether the power-up ended
by timer, by shield consumption, or by death. Each subscriber is invoked inside its own
try/catch, so a fault in another slice cannot break the player.

The player reads **nothing** from the UI or world systems. Flow is one-directional.

### Collision handling

Handles both `OnTriggerEnter2D` and `OnCollisionEnter2D`. Case-sensitive whole-string tag
matching on exactly four tags: `Obstacle`, `Coin`, `PowerUp_Shield`, `PowerUp_Magnet`.
Any other tag is ignored entirely — no events, no state change, no release.

Consumed coins and shield-absorbed obstacles are tracked in `HashSet<int>` keyed by
`GetInstanceID()`. A coin is marked consumed *before* the event is raised, so overlapping
callbacks in one physics step cannot double-count. All collectible disposal goes through a
single `ReleaseCollectible` method — the only place that destroys or pools an object, so
switching to pooling changes one method body.

Coins declare their own value via an `ICoinValue` interface, clamped to 1–1000 with a
one-time warning on out-of-range values, falling back to a serialized default when a coin
carries no such component. Startup validates that all four tags are registered and reports
any that are missing.

### Death sequence

`PlayerDeath.RequestDeath()` calls a single guarded controller method that returns false if
already dying or dead. Order inside the guard: expire all active power-ups *while still
alive*, then set not-alive, disable input, zero velocity, disable physics simulation. Then
present the Death pose, then play the cue, then raise `OnPlayerDeath`.

A re-entrancy flag covers the case where a subscriber reacts to a power-up expiry by
requesting death again — without it, that path would produce a second death event.

### Animation

Three states: `Flying`, `Falling`, `Death`. Derived only from `IsAlive` and `IsThrusting`
plus a death notification — never from raw input, never from physics values. Death latches
until reset. If the derived state equals the currently presented state, no animator call is
issued, so a running clip is never restarted. Missing animator states are reported once per
name rather than failing silently.

### Presentation

Jetpack flame: 8 frames cycling at 18 fps while thrusting, drawn behind the character,
hidden otherwise, restarting at frame 0 on each burst so every ignition reads the same.
Shield bubble: 3 frames at 8 fps with a gentle scale pulse, drawn in front, shown only
while shielded. Both read published state only, so neither can ever contradict the physics
or show while the player is dead.

### Test environment built

A separate sandbox scene with a stand-in scroller that spawns colour-coded tagged
obstacles, coins and power-ups moving right to left, plus a live debug overlay showing
alive/thrusting/shielded/magnet state, coin and score totals, death count, power-up
activation and expiry counts, and the last event fired. Hotkeys: reset, force death, grant
shield, grant magnet.

Built because the world system did not exist yet and feel cannot be judged with nothing to
dodge. It became the reference implementation handed to Unarine, because it consumes only
the public interface.

### Assets

All art is by Kenney under CC0 1.0 (public domain). Six existing Jetpack Joyride clone
repositories on GitHub were evaluated as asset sources and rejected: four had no licence at
all, meaning default copyright and all rights reserved, and the two carrying MIT and Apache
licences appeared to contain Halfbrick Studios artwork the uploaders had no right to
sub-licence onward. Attribution does not substitute for permission.

---

## ITERATION — WHAT CHANGED AND WHY

The rubric awards marks specifically for the iterative design process: what changed from
the original plan, what was added or removed, and **why**. Cover all of these. Each is a
real decision with a real reason. Do not soften the failures.

1. **Abandoned an abstracted architecture.** The original plan split every rule into pure
   C# classes with no Unity dependency, wrapped by thin components, with adapter interfaces
   for physics and logging, so rules could be unit-tested outside the engine. Removed in
   favour of six direct components with logic inline. *Why:* it was solving a problem the
   project did not have. It assumed verification via a large automated test suite, but in a
   five-day build the real verification was playing the game. The indirection slowed the
   tuning loop and made the code harder for teammates to read. *Cost:* lost the ability to
   verify rules in isolation. Verification is now manual observation.

2. **Made all parameters live-tunable.** Originally read once at startup; now re-read every
   physics tick. *Why:* comparing two tunings requires feeling them back to back. Restarting
   play mode between them destroys the sensory comparison, because by the time the game
   restarts you have lost the memory of how the previous setting felt.

3. **Added a sandbox scene that was not in the plan.** *Why:* the player system was ready
   before the world system, and waiting would have serialised the team. Also, movement feel
   cannot be evaluated without hazards approaching at a rate.

4. **Rejected all six clone repositories as asset sources.** *Why:* licensing, as above.
   *Unanticipated upside:* CC0 packs are internally consistent in style, whereas mixed rips
   from six projects would have looked incoherent. Visual coherence is a design quality, not
   only a legal one — the constraint improved the result.

5. **Changed thrust input from Inspector-assigned to a code fallback.** *Why:* an
   unassigned input reference produces a prefab that silently cannot be played, with no
   error explaining why. In a shared project where teammates open each other's prefabs, that
   is a collaboration hazard, not just a bug.

6. **Moved the flame and shield sprites to runtime loading after a genuine failure.**
   Originally wired as serialized asset references. *Why:* Unity's sprite metadata was
   hand-authored and an internal sprite identifier was duplicated across eleven sprites,
   which broke sub-asset resolution and left the jetpack flame invisible with no error. The
   underlying assumption about how sprite sub-assets are addressed was simply wrong. *Lesson
   to state honestly:* the fragile approach was chosen because it avoided leaving a
   comfortable workflow — optimising for personal convenience rather than robustness.

7. **Chose containment over death at the vertical bounds.** *Why:* containment makes the
   ceiling and floor tactical space a player can use, whereas killing there would punish the
   player for an input they are actively holding, which reads as unfair in a game whose only
   verb is holding.

8. **Reordered the death sequence** so power-ups expire while the player is still alive,
   and added the re-entrancy guard. *Why:* the activation/expiry invariant must hold, and a
   subscriber reacting to an expiry could otherwise trigger a second death.

---

## DESIGN DECISIONS TO ARGUE, NOT JUST STATE

Each of these needs a *reason*, and each should be tied back to either measurement validity
or player experience.

- **Asymmetric speed clamps.** Max fall is 12, max rise is 8. Falling is deliberately
  faster than rising so releasing the button has weight and the jetpack reads as working
  against something. A symmetric pair made the character behave like a cursor rather than a
  body with momentum.
- **Thrust as a continuous force rather than a velocity impulse or position change.** The
  alternatives were easier to implement but would have collapsed the continuous relationship
  between how long the button is held and where the character ends up.
- **The ratio matters more than the magnitudes.** Thrust 35 against gravity 3 is a
  relationship; scaling both changes the timescale but preserves the character of the
  movement.
- **One state owner.** Only the controller mutates state, which is what makes the
  exactly-once guarantees enforceable in one place rather than five.
- **One-directional event flow.** The player publishes and never subscribes to teammates'
  systems, which is what allowed three people to build concurrently against a specification
  rather than against each other's unfinished code.

---

## THEORY — USE SPARINGLY AND ONLY WHERE IT DOES WORK

Do not name-drop. Only include a set work where it genuinely sharpens a point. Two or three
substantive engagements are worth more than five decorative ones.

- **Anthropy and Clark** argue that a verb is constituted by what it acts on and what
  resists it, not by the button that triggers it. Useful for explaining why "hold to thrust"
  is not a trivial verb: its meaning comes from the gravity it opposes. Also useful for
  reading what the verb economy *says* — the character is always falling, and thrust only
  ever interrupts the default state, so effort is temporary and gravity is permanent.
- **Bogost's unit operations** — discrete encapsulated elements whose meaning emerges from
  configuration, as opposed to totalising system operations. Useful for justifying the
  one-directional architecture against the more common alternative of a central GameManager
  that polls everything. **Also state the cost honestly:** behaviour distributed across
  units is harder to locate when it goes wrong, and it left cross-cutting features such as
  the coin magnet without a natural owner.
- **Maxwell-Gardner** treats production as a constraint that shapes design rather than
  administration after it. Useful for the decisions where schedule overrode technical
  preference — items 1 and 5 in the iteration list.

---

## STRUCTURE TO PRODUCE

```
[N].1  System overview and its role as the investigation's control variable
[N].2  Micro-level: how the system works
       - component responsibilities and the single-state-owner rule
       - the fixed physics tick order, and why that order
       - the tunable parameters and what each does to feel
[N].3  Design decisions and their reasoning
       - asymmetric clamps
       - force not impulse
       - containment not death at the bounds
       - the exactly-once guarantees, tied to measurement validity
[N].4  Macro-level: how this system interacts with the world and UI systems
       - the interface, the invariant, what each teammate needed
       - where the seams are fragile
[N].5  Iterative design process — what changed and why
[N].6  Testing and evaluation — what was verified, how, and what was not
[N].7  Outstanding work and known limitations
```

Suggest one or two diagrams in each of `[N].2` and `[N].4`, described precisely enough that
the student can draw them: a component-ownership diagram, the tick-order flowchart, and a
cross-system event-flow diagram.

---

## VOICE AND QUALITY RULES

**Write like this:** specific, committed, willing to state a trade-off and name what was
lost. Prefer "I chose X over Y because Z, which cost me W" over "X was implemented."

**Do not write:**
- Openers like "In the ever-evolving landscape of game development" or "This section will
  explore"
- Words used as filler: robust, seamless, leverage, delve, elevate, crucial, pivotal,
  cutting-edge, comprehensive, holistic, testament, showcase
- Tricolon padding ("engaging, immersive, and dynamic")
- Sentences that restate the heading before saying anything
- Praise for the student's own work. Let the reasoning carry it
- Any claim not supported by the facts listed above

**Every technical statement must earn its place** by connecting to either the player's
experience or the validity of the group's investigation. If a sentence does neither, cut it.

Length: roughly 1,800–2,500 words, excluding tables and diagram descriptions.

**Two honesty requirements.** First, the parameter values have not yet been fully tuned
through playtesting, so do not claim tuning findings that do not exist — state it as
outstanding work. Second, the sprite-metadata failure in iteration item 6 should be written
plainly, including the lesson, because a marker will trust the rest of the document more if
one item is genuinely self-critical.

Ask me for any clarification you need before writing, rather than filling gaps with
plausible invention.
