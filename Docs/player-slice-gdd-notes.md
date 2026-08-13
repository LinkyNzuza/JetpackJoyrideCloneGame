# Individual Game Design Document
## Player / Core Gameplay System — Jetpack Joyride Clone

**Author:** Ako Baloyi
**Role:** Person 1 — Player character, mechanics, animation, collision, death, collectible interaction, power-up effects
**Team:** Ako Baloyi (player), Linky Nzuza (world/obstacles), Unarine (UI/scoring/systems)
**Engine:** Unity 6000.0.53f1, 2D URP

---

## 0. Rubric map

| Rubric criterion | Section |
|---|---|
| Game research: category & sub-category, genre analysis | 1 |
| Interrogation: what we are questioning, hypothesis | 2 |
| Group design goals | 2.3 |
| **Personal design goals and intended contribution** | 2.4 |
| Actions and challenges that make up gameplay | 3 |
| **Feature documentation — micro-level** | 4 |
| **Feature documentation — macro-level / cohesive design** | 5 |
| **Diagrams and technical breakdowns** | 4.1, 4.2, 5.1 |
| **Iterative design process and why things changed** | 6 |
| Testing, evaluation, decision-making | 7 |
| **Personal reflection** | 8 |
| Project plan: systems and dependencies | 9 |

Sections in bold are the individual submission's weighted criteria.

---

## 1. Game research

### 1.1 The game being cloned

*Jetpack Joyride* (Halfbrick Studios, 2011) is a side-scrolling endless runner. The
player controls Barry Steakfries, who breaks into a laboratory and steals a
machine-gun-powered jetpack. The character moves forward automatically; the player
controls only altitude, dodging hazards and collecting coins until they die. There is no
win condition — only distance.

### 1.2 Category and sub-category

**Category:** Action.
**Sub-category:** Endless runner, specifically the *one-touch* or *single-input* branch.

The endless runner genre splits meaningfully by input verb:

| Sub-type | Input | Examples |
|---|---|---|
| Jump-runner | Discrete jump, sometimes double-jump | *Canabalt*, *Temple Run* |
| Lane-switcher | Discrete lateral commit | *Subway Surfers* |
| **Hover / thrust-runner** | **Continuous analogue-by-duration hold** | ***Jetpack Joyride***, *Flappy Bird* (impulse variant) |

*Jetpack Joyride* sits in the third group, and that is the distinction our clone is built
around. This matters because it changes what skill means. In a jump-runner, skill is
timing a discrete commitment. In a thrust-runner, skill is *continuous modulation* —
the player is always adjusting, never merely reacting.

### 1.3 What defines the genre

Four properties define the endless runner, and each one shifts the design burden:

1. **Automatic forward motion.** The player never chooses to advance. This removes
   navigation as a skill and concentrates all skill into one axis.
2. **Procedural, unbounded content.** No authored level to memorise, so difficulty must
   come from systems rather than layout.
3. **Single failure state.** One hit ends the run. This makes every hazard maximally
   meaningful and makes the death sequence emotionally load-bearing.
4. **Score as the only goal.** Progress is a number, so the game must be readable at a
   glance and instantly restartable.

Property 1 is why my slice locks the player's horizontal position rather than moving
them: the *world* moves, and the player's entire expressive range is vertical. Property 3
is why the death sequence in my slice must fire exactly once and be unambiguous.

---

## 2. Interrogation

### 2.1 What we are questioning

*Jetpack Joyride* gives the player **one binary input**. There is no aim, no attack, no
dodge, no lane, no double-jump. Held or not held. That is the entire control surface.

And yet the game does not feel impoverished. It feels precise. Players develop real
skill, describe near-misses in detail, and can distinguish a good run from a lucky one.
Two players with the same single button perform visibly differently.

**That is the contradiction we chose to interrogate: how does a single binary input
produce a wide expressive range?**

This is a genuinely non-obvious question. A binary input has two states, so the
expressiveness cannot be in the input. It has to be manufactured somewhere else — and
finding *where* is the reverse-engineering task.

### 2.2 Hypothesis

> **The expressive range of *Jetpack Joyride* does not come from the input. It comes
> from the physical simulation the input pushes against.**
>
> Because thrust is applied as a *continuous force competing with gravity* rather than
> as a position change or a velocity impulse, the player is not choosing between two
> states — they are choosing a **duration**, and duration maps continuously onto
> altitude, velocity, and momentum. The button is binary; the state space it addresses
> is not.
>
> We therefore predict that the felt quality of the game is governed almost entirely by
> the **relationship between four numbers** — thrust force, gravity, maximum rise speed,
> and maximum fall speed — and that changing their *ratios* while leaving the input
> untouched will produce recognisably different games.

This hypothesis is falsifiable, which is what makes it useful. If the feel were really
in the input handling, then tuning those four numbers would change difficulty but not
character. If the hypothesis holds, tuning them should change the game's *personality*:
floaty versus heavy, forgiving versus twitchy.

### 2.3 Group design goals

1. Reproduce the core loop — thrust, dodge, collect, die, restart — as a playable
   artefact, not a demo.
2. Build the three systems (player, world, UI) concurrently in one shared project, with
   integration proven continuously rather than merged at the end.
3. Keep the systems decoupled enough that three people can work without blocking each
   other.
4. Make the game readable at a glance, in line with genre property 4.

### 2.4 My personal design goals

My slice is the character controller, which means **my slice is where the hypothesis
lives or dies**. If the expressive range comes from the force-versus-gravity
relationship, then it is my four numbers that carry it. My goals followed from that:

1. **Make thrust a force, not a movement.** Resist the easier implementations —
   setting position directly, or applying a fixed upward velocity — because both would
   collapse the continuous state space the hypothesis depends on. This was a design
   commitment, not a technical one.

2. **Make the four numbers live-tunable during play.** If the hypothesis is that ratios
   govern feel, the prototype must let us *test* that by changing them and feeling the
   difference immediately. Requiring a recompile between tunings would make the central
   claim untestable.

3. **Build an isolated environment for evaluating feel.** The world slice would not be
   ready when the controller was, and feel cannot be judged in a vacuum. I needed
   something to dodge in order to know whether the numbers were right.

4. **Treat the vertical bounds as design space, not as a wall.** Decide deliberately
   whether leaving the screen kills or contains, and justify it against the hypothesis.

5. **Publish state outward and consume nothing.** So that my teammates' systems could
   be built against my slice while it was still changing.

My intended contribution beyond my assigned role: owning the integration contract that
the other two slices consume, and owning the tuning methodology the group uses to judge
whether the clone feels like the original.

---

## 3. Actions and challenges

### 3.1 The player's verbs

The complete verb set, which is deliberately tiny:

| Verb | Input | Consequence |
|---|---|---|
| **Thrust** | Hold | Upward force accumulates against gravity |
| **Fall** | Release | Gravity accumulates unopposed |
| *(Collect)* | — | Passive, resolved by contact |
| *(Absorb)* | — | Passive, consumes a held shield |

Only two verbs are active. Collection and absorption are *consequences* of positioning
rather than actions, which is important: the player never presses a "collect" button, so
every pickup is really a navigation decision made earlier.

### 3.2 The challenge taxonomy

What the player is actually being asked to solve:

| Challenge | Skill tested | System |
|---|---|---|
| Thread a gap between hazards | Sustained altitude precision | Player + world |
| Reach a coin line without hitting a hazard | Risk/reward routing | Player + world |
| Recover from a bad altitude | Momentum management | Player |
| Decide whether to spend a shield | Resource judgement | Player |
| Survive increasing density | Endurance, adaptation | World |

**The tension that makes it a game:** coins and hazards occupy the same space. The
optimal *survival* path is rarely the optimal *scoring* path. Every altitude choice is
therefore a wager, and because altitude changes take time to execute (force, not
teleport), the wager must be committed to early. That delay between decision and effect
is where the skill lives — and it is a direct consequence of the hypothesis.

### 3.3 How the systems combine to produce the experience

```mermaid
graph LR
    W[World: scrolls hazards<br/>and coins toward player] --> P[Player: modulates altitude<br/>against gravity]
    P --> C[Contact resolution:<br/>collect, absorb, or die]
    C --> U[UI: score, distance,<br/>power-up timers]
    C -->|death| R[Restart]
    R --> P
    U -.reads only.-> P
```

The experience emerges from the *rate* at which the world delivers challenge versus the
*latency* of the player's ability to respond. Neither system alone produces the feel.
The world sets the problem density; my slice sets the response latency. Tuning the game
means tuning the relationship between those two.

---

## 4. My system — micro level

### 4.1 Component structure

Six components on one GameObject. Four own behaviour, two are pure presentation.

```mermaid
graph TD
    subgraph Owns state
        PC[PlayerController<br/>physics, input, alive/shield/magnet,<br/>power-up timers, events, ResetRun]
    end
    subgraph Reacts only
        PA[PlayerAnimation<br/>presents one state]
        PJ[PlayerJetpackFlame<br/>exhaust while thrusting]
        PS[PlayerShieldVisual<br/>bubble while shielded]
    end
    subgraph Routes
        PCol[PlayerCollision<br/>tag routing + single release point]
        PD[PlayerDeath<br/>one-shot death sequence]
    end
    PCol -->|requests| PD
    PCol -->|consume shield / activate power-up / raise coin| PC
    PD -->|begin death, raise event| PC
    PA -.reads IsAlive, IsThrusting.-> PC
    PJ -.reads IsThrusting.-> PC
    PS -.reads IsShielded.-> PC
```

The single most important structural rule: **`PlayerController` is the only component
that owns state.** Everything else reads it or asks it to change. This is what makes the
"exactly once" guarantees enforceable in one place instead of five.

Presentation components read published state and never input. This matters for design
reasons, not just architectural ones: it guarantees the flame can never show while the
character is dead, and the animation can never disagree with the physics, because both
derive from the same source.

### 4.2 The fixed tick order

Several rules can apply on the same physics tick, so their order is fixed and deliberate.

```mermaid
flowchart TD
    A[FixedUpdate] --> B[1. Apply thrust force<br/>only while alive and held]
    B --> C[2. Clamp vertical velocity<br/>to -maxFall .. +maxRise]
    C --> D[3. Contain Y inside play bounds<br/>on contact: snap Y, zero vertical velocity]
    D --> E[4. Lock X to spawn value<br/>zero horizontal velocity - ALWAYS]
    E --> F{Alive?}
    F -->|yes| G[5. Tick power-up timers]
    F -->|no| H[End tick]
    G --> H
```

**Why clamp before contain:** a tick needing both should end at the containment result
of zero velocity, not the clamped value. If containment ran first, the clamp would
re-introduce velocity into a body that is supposed to be resting against a bound, and
the character would jitter at the ceiling.

**Why step 4 is unconditional:** it runs even while dead and even while physics
simulation is disabled. Horizontal drift would break the core premise of the genre
(automatic forward motion, player fixed), so it is enforced with no exceptions.

### 4.3 The four numbers, and what each does to the experience

This table is the practical form of the hypothesis. Each parameter is Inspector-exposed
and re-read every tick, so it can be changed mid-play.

| Parameter | Default | Range | What it does to *feel* |
|---|---|---|---|
| Thrust force | 35 | 0.1–500 | Authority. Low = sluggish, unresponsive. High = twitchy, hard to hold a line |
| Gravity scale | 3 | 0.1–20 | Punishment for releasing. Low = floaty and forgiving. High = heavy, demanding |
| Max rise speed | 8 | 0.1–100 | Ceiling on ascent. Caps how fast a mistake can be corrected upward |
| Max fall speed | 12 | 0.1–100 | Ceiling on descent. Sets the worst-case reaction window |

**Design decision — asymmetric clamps.** Max fall (12) is deliberately faster than max
rise (8). Falling should feel heavier than rising, so that releasing the button has
consequence and the jetpack reads as *working against* something rather than simply
toggling direction. A symmetric pair felt weightless in testing — the character behaved
like a cursor rather than a body.

**Design decision — the ratio matters more than the values.** Thrust 35 against gravity
3 is a ratio, and scaling both leaves the feel broadly intact while changing the
timescale. This is the clearest support for the hypothesis we found: the game's
personality tracks the ratio, not the magnitudes.

### 4.4 Design decision — containment rather than death at the bounds

Leaving the visible play area could either kill the player or stop them. We chose to
contain, and this is a design choice with real consequences.

Containing turns the ceiling and floor into **tactical space**. A player can ride the
ceiling to cross a low hazard field, or hug the floor under a high one. The bound
becomes a resource.

Killing on bounds exit would instead make the top of the screen a second hazard, and
would punish the player for an input they are actively holding — which reads as unfair
in a game whose only verb is holding. It would also contradict the hypothesis: if the
top of the screen kills, the usable state space shrinks and expressiveness drops.

Hitting a bound is therefore explicitly non-fatal. Position snaps to the limit and
vertical velocity zeroes, but alive and shield state are untouched.

### 4.5 Rules that required explicit decisions

Cases where the "obvious" behaviour was ambiguous, and why we chose as we did.

| Rule | Decision | Design reason |
|---|---|---|
| A coin touched by several colliders in one frame | Counts exactly once | Score must be trustworthy or the only goal in the game becomes meaningless |
| Two hazards hit while shielded in the same frame | One shield consumed, then death | Fair reading. A shield is one hit, not one frame of invulnerability |
| Re-collecting a power-up already active | Refresh timer, no second activation event | Stacking would let players bank power-ups, flattening the risk/reward of collecting one |
| Death triggered by multiple contacts | Fires exactly once | Genre property 3: a single failure state must be unambiguous |
| Button held through a restart | Counts as released until pressed again | Otherwise the player launches instantly at the start of a new run with no input |
| Press and release inside one physics tick | Still produces one tick of thrust | A fast tap that produced nothing would read as dropped input |

---

## 5. My system — macro level

### 5.1 The integration contract

My slice publishes state and events outward and reads nothing from the other two. This
one-directional flow is what let three people build concurrently in one project.

```mermaid
graph TD
    PLAYER[Player slice - Ako<br/>owns character state]
    WORLD[World slice - Linky<br/>scroll, spawn, difficulty]
    UI[UI slice - Unarine<br/>HUD, score, game manager]

    WORLD -->|tagged prefabs enter<br/>player's collider| PLAYER
    PLAYER -->|OnPlayerDeath<br/>OnCoinCollected int<br/>OnPowerUpActivated<br/>OnPowerUpExpired| UI
    PLAYER -->|IsAlive IsShielded<br/>IsThrusting IsMagnetActive| UI
    UI -->|ResetRun| PLAYER
    PLAYER -.never reads.-> UI
    PLAYER -.never reads.-> WORLD
```

**Read-only state:** `IsAlive`, `IsShielded`, `IsThrusting`, `IsMagnetActive`

**Events out:**

| Event | Meaning |
|---|---|
| `OnPlayerDeath` | Exactly once per run |
| `OnCoinCollected(int)` | Carries that coin's value |
| `OnPowerUpActivated(PowerUpType)` | A power-up became active |
| `OnPowerUpExpired(PowerUpType)` | Timer elapsed, shield consumed, or run ended |

**Operation:** `ResetRun()` restores the initial state without reloading the scene.

**The invariant that makes the UI slice possible:** every activation is matched by
exactly one expiry — whether the power-up ended by timer, by shield consumption, or by
death. Without this guarantee, Unarine's power-up timer UI would drift out of sync and
she would have to defensively re-read state. With it, she can trust the events alone.

Subscribers are each invoked inside their own try/catch, so a fault in another slice
cannot break the player. This was a decision about *team* robustness as much as code:
it means a bug in the UI slice cannot make my slice look broken during a shared build.

### 5.2 What my teammates needed from me

**Linky (world):** prefabs carry the exact case-sensitive tags `Obstacle`, `Coin`,
`PowerUp_Shield`, `PowerUp_Magnet` with trigger colliders. A coin should carry a
component implementing `ICoinValue` returning 1–1000; without one it falls back to 1, so
her prefabs work before she implements it.

**Unarine (UI):** subscribe to the four events, read the four properties, call
`ResetRun()`. I provided a working reference implementation
(`Assets/Scripts/_Sandbox/SandboxDebugHud.cs`) that consumes exactly this surface and
nothing else, so she had a concrete example rather than a description.

### 5.3 Where the interfaces are fragile

Honest assessment of the seams:

- **Tag strings are stringly-typed.** A typo in Linky's prefab fails silently at
  authoring time and loudly at runtime. Mitigated with a startup check that reports any
  unregistered tag, but not eliminated.
- **Shared files are the real integration risk.** `TagManager.asset`,
  `EditorSettings.asset`, and the shared scene are edited by all three slices and are the
  most likely merge conflicts.
- **The magnet is a contract with no owner.** I publish `IsMagnetActive`, but pulling
  coins requires touching the coin objects, which are Linky's. Unresolved.

---

## 6. Iterative design process

What changed from the original plan, and why. This is the honest version.

### 6.1 Architecture: abstracted core → direct components

**Original plan.** A "pure core, thin shell" split: all rules in plain C# classes with
no Unity dependency, wrapped by thin `MonoBehaviour`s, with adapter interfaces for
physics, logging, and audio cues. The goal was to make the rules testable without
launching Unity, and to make the engine's velocity API a single swappable call site.

**What changed.** Abandoned in favour of four self-contained `MonoBehaviour`s with logic
inline.

**Why.** The abstraction was solving a problem we did not have. It assumed the rules
would be verified by a large automated property-test suite; in a three-week team project
with a shared scene, the real verification was going to be *playing the game*. The
adapter layer added indirection that made the code harder for teammates to read, and
harder for me to tune quickly, which directly conflicted with personal design goal 2
(fast iteration on feel). The interfaces also could not be wired in the Inspector, which
pushed setup into code and away from the designer-facing workflow.

**What I lost.** Genuine ability to unit-test the rules in isolation. Verification is now
manual. In a longer project I would not make this trade.

### 6.2 Feel: a testable claim needed live tuning

**Original plan.** Serialized fields read once at startup.

**What changed.** All four numbers are re-read every tick, including gravity, so changes
in the Inspector take effect on the next physics tick during play.

**Why.** Directly forced by the hypothesis. If the claim is that ratios govern feel, then
the prototype must allow the ratio to be changed *while feeling it*. Restarting play mode
between tunings breaks the sensory comparison — by the time the game restarts you have
lost the memory of how the previous setting felt. This change turned the hypothesis from
an assertion into something we could actually probe.

### 6.3 Evaluation: added a sandbox that was not in the plan

**Original plan.** Test the player in the shared scene once the world slice existed.

**What changed.** Built a separate sandbox scene with a stand-in scroller, procedurally
generated colour-coded obstacles, coins and power-ups, a live state overlay, and hotkeys
to grant power-ups, force death, and reset.

**Why.** Two reasons. First, feel cannot be judged with nothing to dodge — I could not
evaluate whether 35-against-3 was right without hazards approaching at a rate. Second,
waiting on the world slice would have serialised the team, which contradicted group goal
2. The sandbox let me evaluate my own system on my own schedule.

**Unintended benefit.** Because the overlay consumes only the public surface, it became a
live proof that the integration contract works, and then became the reference
implementation I handed to Unarine. A testing tool turned into a communication tool.

### 6.4 Assets: six clone repos → CC0

**Original plan.** Borrow sprites and animations from existing Jetpack Joyride clone
repositories on GitHub, with attribution, to save time.

**What changed.** All six were rejected. Art is now entirely Kenney CC0.

**Why.** I checked the licences rather than assuming. Four of the six had no licence at
all, which under GitHub's terms means default copyright — all rights reserved. Citation
documents copying but does not grant permission. The two that did carry MIT and Apache
licences appeared to contain Halfbrick's original artwork, which the uploaders never held
rights to and therefore could not sub-licence onward. Attribution cannot fix that.

**The design upside I did not anticipate.** CC0 packs are internally consistent in style.
Mixed rips from six different projects would have looked incoherent, and visual coherence
is a design quality, not just a legal one. Constraint improved the outcome.

### 6.5 Input: Inspector-wired → code fallback

**Original plan.** The thrust action assigned as an `InputActionReference` in the
Inspector.

**What changed.** Kept the field, but the controller now builds an equivalent action in
code when nothing is assigned.

**Why.** An unassigned reference produces a prefab that silently cannot be played, with
no error explaining why. For a team project where teammates open each other's prefabs,
that is a trap. The fallback means the prefab works on first Play for anyone.

### 6.6 Presentation: authored asset references → runtime loading

**Original plan.** Wire flame and shield sprites as serialized references.

**What changed.** Both effects load their frames by name from `Resources` at runtime.

**Why.** A genuine failure. I authored Unity's sprite metadata by hand and duplicated an
internal sprite identifier across eleven sprites, which broke sub-asset resolution and
left the flame invisible. When I compared against metadata Unity generated itself, my
assumption about how sprite sub-assets are addressed was simply wrong. Rather than keep
guessing at engine internals, I removed the fragile references entirely. Loading by name
cannot break the same way.

**The lesson.** I chose a fragile approach because it was the one I could do without
opening the editor. The constraint I was optimising for was my own convenience, not the
project's robustness.

---

## 7. Testing, evaluation, and decision-making

**Method.** Feel was evaluated in the sandbox scene by playing with hazards at varying
densities and adjusting the four numbers live. The debug overlay made state and event
counts visible so that mechanical correctness could be checked while playing rather than
inferred afterwards.

**Correctness checks made observable in the overlay:**

- Power-up activations versus expiries — if these ever diverge beyond the number
  currently active, the invariant is broken.
- Death count per run — must be exactly one however many hazards are clipped at once.
- Thrust after reset while the button is held — must stay released until re-pressed.

**Controls:** `Space` thrust, `R` reset, `K` force death, `1`/`2` grant shield/magnet.

The sandbox is isolated under `Assets/Scripts/_Sandbox/` and `Assets/Scenes/PlayerSandbox.unity`
and is intended to be deleted before final submission. Note that `Assets/Resources/PlayerFX/`
must survive that deletion — the player's flame and shield load from it.

---

## 8. Personal reflection

> **This section needs Ako's own voice.** The factual scaffolding is below; the
> judgements should be yours, and a marker will be able to tell the difference. Prompts
> are marked with ▸.

### 8.1 On my system

**What worked.** The force-based controller does produce the continuous expressive range
the hypothesis predicted — holding the button for different durations produces genuinely
different trajectories, and the character reads as a body with momentum rather than a
cursor. The one-directional event contract worked: my teammates could build against my
slice while it was still changing.

**What did not.** The presentation layer failed twice for the same underlying reason: I
made assumptions about engine internals instead of verifying them, and I did it because
verifying would have meant leaving the workflow I was comfortable in. The sprite
identifier collision cost real time and produced a bug that looked like nothing had
happened at all, which is the worst kind.

▸ *Which of the four numbers ended up mattering most to how the game feels, and did that
match what you expected before you played it?*

▸ *Was abandoning the abstracted architecture the right call in hindsight, or did losing
automated tests cost you later?*

### 8.2 On the hypothesis

The hypothesis held up in the direction it predicted: changing the thrust-to-gravity
ratio while leaving input handling untouched produced recognisably different games —
floaty and forgiving at low gravity, twitchy and demanding at high thrust. The input
code never changed while the feel changed completely, which is the core of the claim.

Where it is incomplete: the hypothesis explains the *player's* expressive range but not
the *challenge* the range is expressed against. Feel turned out to be inseparable from
hazard density and approach speed, which live in Linky's slice. A more complete
hypothesis would be about the relationship between response latency and problem density,
not about the player controller alone.

▸ *Did anything about the original game surprise you once you tried to rebuild it?*

▸ *Would you now say the expressiveness is in the physics, or in the interaction between
physics and level pacing?*

### 8.3 On group workflow

▸ *This part I cannot write for you — I have no visibility into how the three of you
actually communicated. Things worth addressing honestly:*

- ▸ *Branch strategy. The repository moved from your own remote to Linky's shared repo
  partway through, and branches were reorganised. How did that affect integration?*
- ▸ *The brief asks for a build every two days and warns against last-minute merging.
  Did you achieve that? If not, why, and what would you change?*
- ▸ *Shared files — `TagManager.asset`, `EditorSettings.asset`, the shared scene — were
  the predicted conflict points. Did they cause problems in practice?*
- ▸ *Unresolved cross-slice items: who owns the magnet effect, and the scene naming
  (`SampleScene` versus `Main`). How were decisions like these made, or not made?*
- ▸ *Evidence of contribution beyond your own role: the integration contract, the
  reference HUD implementation you gave Unarine, the tag/collider requirements you gave
  Linky, the asset licensing decision that affected the whole group.*

---

## 9. Project plan — systems and dependencies

```mermaid
graph TD
    subgraph Ako - Player
        A1[Rigidbody2D thrust controller]
        A2[Speed clamps + bounds containment]
        A3[Animation states]
        A4[Tag collision routing]
        A5[Death sequence]
        A6[Power-up timers + shield]
        A7[Flame + shield visuals]
    end
    subgraph Linky - World
        B1[Background + parallax]
        B2[Scroll speed + difficulty ramp]
        B3[Obstacle spawner]
        B4[Coin spawner]
        B5[Power-up spawner]
    end
    subgraph Unarine - UI/Systems
        C1[Game manager]
        C2[HUD: score, distance, coins]
        C3[High score persistence]
        C4[Main menu + game over]
        C5[Power-up timer UI]
    end

    A1 --> A2 --> A3
    A4 --> A5
    A4 --> A6 --> A7
    B2 --> B3
    B2 --> B4
    B2 --> B5
    B3 -->|tagged prefabs| A4
    B4 -->|tagged prefabs + ICoinValue| A4
    B5 -->|tagged prefabs| A4
    A5 -->|OnPlayerDeath| C1
    A4 -->|OnCoinCollected| C2
    A6 -->|OnPowerUpActivated/Expired| C5
    C1 -->|ResetRun| A1
    C1 --> C4
    C2 --> C3
```

**Critical path.** The tag contract (`A4` ← `B3`/`B4`/`B5`) and the event contract
(`A5`/`A4`/`A6` → `C1`/`C2`/`C5`) are the two integration points. Both were specified
and stubbed early — via the sandbox spawner and the sandbox HUD — so neither slice
blocked the others.

**Shared-file dependencies (highest conflict risk):** `ProjectSettings/TagManager.asset`,
`ProjectSettings/EditorSettings.asset`, `Assets/Scenes/SampleScene.unity`.

---

## 10. Assets and licensing

All art is by **Kenney** (<https://kenney.nl>) under **CC0 1.0 Universal** — public
domain, no attribution legally required, no restriction on academic or commercial use.
Packs used: Platformer Characters, Jumper Pack, Platformer Pack Redux, Space Shooter
Redux, Background Elements Redux.

Full attribution, folder-to-source mapping, and the record of sources evaluated and
rejected on licensing grounds is in `Assets/Art/ATTRIBUTION.md`.

*Jetpack Joyride* is a trademark of Halfbrick Studios. This is an educational clone of
the game's mechanics using no Halfbrick artwork, audio, or trademarks.

---

## 11. Current state and outstanding work

**Working:** thrust and fall against gravity, vertical speed clamping, bounds
containment, fixed horizontal lock, three animation states, tag-based collision routing,
coin collection with per-coin values, both power-ups with independent timers, shield
absorption of exactly one hit, one-shot death, in-place run reset, jetpack flame, shield
bubble, and the four outward events.

**Outstanding in my slice:**

| Item | Why it matters |
|---|---|
| Magnet has no effect | Publishes state but pulls no coins. Needs an owner agreed with the world slice |
| Shield-break and death cues are empty | Wired as events, pending audio assets |
| Death is visually thin | A pose change with no knockback or fade. Weak for the genre's single failure state |
| Animation states are single-frame | Readable but static. Multi-frame loops would improve presentation |
| **Numbers are still defaults** | The central hypothesis is only partly tested until these are tuned by feel |

The last row is the most important remaining design work, not the most important
remaining code.
