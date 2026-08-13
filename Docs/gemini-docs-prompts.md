# Gemini in Google Docs — Prompt Sequence for the Player System Sections

## Why this is structured as a sequence, not one prompt

Gemini in Google Docs behaves differently from the Gemini chat app:

- **It already reads your document.** You do not need to paste the GDD into the prompt,
  and doing so tends to make responses worse.
- **You target a section by naming it** in the prompt, e.g. *"in section 7, Mechanical
  Development."*
- **Output arrives as inline suggestions** you accept or reject one at a time, so small
  focused edits are far easier to control than one large generation.
- **The prompt box is a bottom bar.** Very long prompts are unwieldy and instructions get
  dropped.
- **It does not know your code.** Nothing about your implementation is in the document,
  so it will invent details unless you supply them.

The fix for that last point is Step 1: paste the fact sheet into the document as a
temporary appendix. Document content is the context Gemini reads most reliably. You delete
it in Step 5.

---

## STEP 1 — Paste the fact sheet into your doc

Scroll to the very end of the GDD and paste the block below as a new section. Keep the
heading exactly as written, because later prompts refer to it by name.

> **Tip:** switch the Gemini bar to the side panel (bottom bar → *Switch to side panel*)
> so you can read longer responses while you work.

```
APPENDIX Z — PLAYER SYSTEM BUILD FACTS (TEMPORARY, DELETE BEFORE SUBMISSION)

Owner: Ako Baloyi. Engine: Unity 6000.0.53f1, 2D URP, C#. Unity 6 requires
Rigidbody2D.linearVelocity; the older .velocity property is obsolete.

COMPONENTS (six, all on one GameObject)
- PlayerController: physics, input, alive/shield/magnet state, power-up timers, the public
  event surface, ResetRun(). The only component that owns state.
- PlayerAnimation: presents exactly one of Flying, Falling, Death. Reads state, never
  decides it.
- PlayerCollision: tag-based contact routing, single collectible release point.
- PlayerDeath: one-shot death sequence.
- PlayerJetpackFlame: exhaust visual while thrusting.
- PlayerShieldVisual: bubble visual while shielded.

TUNABLE PARAMETERS (Inspector-exposed, re-read every physics tick)
thrust force 35 (range 0.1-500); gravity scale 3 (0.1-20); max rise speed 8 (0.1-100);
max fall speed 12 (0.1-100); play bounds Y -4 to +4; shield duration 5s (1-60);
magnet duration 5s (1-60).
Thrust is AddForce(Vector2.up * thrustForce, ForceMode2D.Force) on a Dynamic Rigidbody2D:
a continuous force, not a velocity impulse and not a position change.

FIXED PHYSICS TICK ORDER
1 re-apply gravity scale so Inspector tuning takes effect next tick
2 sample input: currently-held state plus a "pressed since last tick" edge flag
3 apply the post-reset latch
4 apply thrust force, only while alive and held
5 guard non-finite velocity (NaN/infinity to zero, warn once per run)
6 clamp vertical velocity to [-maxFall, +maxRise]
7 contain Y inside play bounds; on contact snap Y and zero vertical velocity
8 lock X to spawn value and zero horizontal velocity, UNCONDITIONALLY, including while
  dead and while physics simulation is disabled
9 tick power-up timers, only while alive
Clamp runs before containment deliberately: a tick needing both must end at the
containment result of zero velocity. Reversed, the clamp would reintroduce velocity into a
body resting on a bound and the character would jitter at the ceiling.

PUBLIC INTERFACE CONSUMED BY TEAMMATES
Read-only state: IsAlive, IsShielded, IsThrusting (defined as IsAlive && held),
IsMagnetActive.
Events out: OnPlayerDeath; OnCoinCollected(int value);
OnPowerUpActivated(PowerUpType); OnPowerUpExpired(PowerUpType).
Operation: ResetRun() restores the initial state without reloading the scene.
Invariant: every activation is matched by exactly one expiry, whether ended by timer, by
shield consumption, or by death.
Each subscriber is invoked inside its own try/catch, so a fault in another system cannot
break the player.
The player reads nothing from the UI or world systems. Flow is one-directional.

COLLISION
Handles both OnTriggerEnter2D and OnCollisionEnter2D. Case-sensitive whole-string match on
exactly four tags: Obstacle, Coin, PowerUp_Shield, PowerUp_Magnet. Any other tag is ignored
entirely: no events, no state change, no release.
Consumed coins and shield-absorbed obstacles tracked in HashSet<int> keyed by
GetInstanceID(). A coin is marked consumed BEFORE the event is raised, so overlapping
callbacks in one physics step cannot double-count.
All disposal goes through one ReleaseCollectible method, the only place that destroys or
pools an object, so switching to pooling changes one method body.
Coins declare their own value through an ICoinValue interface, clamped to 1-1000 with a
one-time warning when out of range, falling back to a serialized default when a coin has no
such component.
Startup validates that all four tags are registered and reports any missing.

DEATH
PlayerDeath.RequestDeath() calls one guarded controller method that returns false if
already dying or dead. Inside the guard, in order: expire all active power-ups WHILE STILL
ALIVE, then set not-alive, disable input, zero velocity, disable physics simulation. Then
present the Death pose, then play the cue, then raise OnPlayerDeath.
A re-entrancy flag covers a subscriber reacting to a power-up expiry by requesting death
again; without it that path produces a second death event.

ANIMATION
Three states derived only from IsAlive and IsThrusting plus a death notification. Never
from raw input, never from physics values. Death latches until reset. If the derived state
equals the state already presented, no animator call is issued, so a running clip is never
restarted. Missing animator states are reported once per name rather than failing silently.

PRESENTATION
Jetpack flame: 8 frames at 18 fps while thrusting, drawn behind the character, hidden
otherwise, restarting at frame 0 each burst so every ignition reads identically.
Shield bubble: 3 frames at 8 fps with a gentle scale pulse, drawn in front, shown only
while shielded.
Both read published state only, so neither can contradict the physics or appear while dead.
Neither changes intensity with difficulty.

TEST ENVIRONMENT
A separate sandbox scene with a stand-in scroller spawning colour-coded tagged obstacles,
coins and power-ups right to left, plus a live overlay showing alive/thrusting/shielded/
magnet state, coin and score totals, death count, power-up activation and expiry counts,
and the last event fired. Hotkeys: reset, force death, grant shield, grant magnet.
Built because the world system did not exist yet and movement feel cannot be judged with
nothing to dodge. It became the reference implementation handed to the UI owner, because it
consumes only the public interface.

ITERATION: WHAT CHANGED AND WHY
1 Abandoned an abstracted architecture. Original plan split every rule into pure C#
  classes with no Unity dependency behind adapter interfaces so rules could be unit-tested
  outside the engine. Replaced with six direct components. Why: it assumed verification by
  automated test suite, but in a five-day build the real verification was playing the game;
  the indirection slowed the tuning loop and made the code harder for teammates to read.
  Cost: lost the ability to verify rules in isolation, so verification is now manual.
2 Made all parameters live-tunable, re-read every tick instead of read once at startup.
  Why: comparing two tunings requires feeling them back to back; restarting play mode
  between them destroys the comparison because the memory of the previous setting is gone.
3 Added the sandbox scene, which was not in the original plan. Why: the player system was
  ready before the world system and waiting would have serialised the team.
4 Rejected six existing Jetpack Joyride clone repositories as art sources. Four carried no
  licence at all, meaning default copyright and all rights reserved; the two with MIT and
  Apache licences appeared to contain Halfbrick artwork the uploaders could not sub-licence
  onward. Attribution does not substitute for permission. Used Kenney CC0 art instead.
  Unanticipated upside: CC0 packs are internally consistent in style, whereas mixed rips
  from six projects would have looked incoherent, and visual coherence is a design quality.
5 Changed thrust input from an Inspector-assigned reference to a code fallback. Why: an
  unassigned reference produces a prefab that silently cannot be played with no error
  explaining why, which is a collaboration hazard in a shared project.
6 Moved flame and shield sprites to runtime loading after a real failure. Sprite metadata
  was hand-authored and an internal sprite identifier was duplicated across eleven sprites,
  which broke sub-asset resolution and left the jetpack flame invisible with no error. The
  assumption about how sprite sub-assets are addressed was simply wrong. Lesson: the
  fragile approach was chosen because it avoided leaving a comfortable workflow, optimising
  for personal convenience rather than robustness.
7 Chose containment over death at the vertical bounds. Why: containment makes ceiling and
  floor tactical space a player can use, whereas killing there punishes the player for an
  input they are actively holding, which reads as unfair in a game whose only verb is
  holding.
8 Reordered the death sequence so power-ups expire while still alive, and added the
  re-entrancy guard, so the activation/expiry invariant holds.

DESIGN DECISIONS WITH REASONS
- Asymmetric clamps: max fall 12 against max rise 8. Falling is deliberately faster than
  rising so releasing the button has weight and the jetpack reads as working against
  something. A symmetric pair made the character behave like a cursor rather than a body.
- Force rather than impulse or position change: the alternatives were easier but would
  collapse the continuous relationship between how long the button is held and where the
  character ends up.
- The ratio matters more than the magnitudes: thrust 35 against gravity 3 is a
  relationship; scaling both changes the timescale but preserves the movement's character.
- One state owner: only the controller mutates state, which is what makes the exactly-once
  guarantees enforceable in one place rather than six.

WHAT IS NOT DONE
Magnet publishes IsMagnetActive but pulls no coins; it spans two systems and has no agreed
owner. Shield-break and death cues are wired as empty events pending audio. Death is
visually thin: a pose change with no knockback or fade. Animation states are single frames
rather than multi-frame loops. Parameter values are still defaults and have NOT been fully
tuned through playtesting.
```

---

## STEP 2 — Set the constraints

Paste this first, on its own. It shapes everything after it.

```
I am writing the individual feature documentation for the Player Controller system in this
Game Design Document. Before you write anything, read Appendix Z at the end of this
document. It contains the verified build facts for my system.

Rules for everything you write from now on:
- Use only facts from Appendix Z. If you need a detail that is not there, write
  [TO CONFIRM] instead of inventing it.
- Match the existing prose style of sections 1 to 5 of this document: third person for the
  game, first person for my own decisions, plain declarative sentences, no bullet-point
  padding.
- Never use these words: robust, seamless, leverage, delve, elevate, crucial, pivotal,
  cutting-edge, comprehensive, holistic, testament, showcase.
- Do not open a section by restating its heading.
- Do not praise my work. Let the reasoning carry it.
- Every technical statement must connect either to the player's experience or to the
  validity of our difficulty investigation. If it does neither, leave it out.

Confirm you have read Appendix Z and list the six components you found there.
```

Check its answer names all six components. If it does not, it has not read the appendix —
re-paste and try again before continuing.

---

## STEP 3 — The framing prompt

This is the most important one. Paste it next, on its own.

```
Section 3.1 of this document states that the Player Controller is a controlled system
within the investigation rather than the primary experimental variable. That framing is the
spine of my individual documentation.

Explain back to me, in one short paragraph, how you would use this argument: because
difficulty is our independent variable, my system's design requirement was mechanical
invariance, and my design decisions were therefore made in service of measurement validity
rather than feel alone. Do not write the section yet.
```

If its paragraph frames your controller as the fun or expressive part of the game, correct
it before continuing. It must frame the controller as the **constant** against which
difficulty varies.

---

## STEP 4 — Write the sections, one prompt at a time

Run these in order. Accept or reject suggestions after each before moving on.

### 4a — Mechanical development

```
In section 7, Mechanical Development, write a subsection titled "Player Controller
Implementation" of about 500 words, using Appendix Z.

Cover: the six components and the rule that only PlayerController owns state; the nine-step
fixed physics tick order; and specifically why clamping runs before containment. Explain
that thrust is a continuous force rather than a velocity impulse or a position change, and
why that choice preserves the continuous relationship between how long the button is held
and where the character ends up.

Describe, in words I can use to draw it, one diagram: a flowchart of the nine-step tick
order.
```

### 4b — Design decisions

```
Still in section 7, add a subsection titled "Player Design Decisions" of about 450 words.

Argue four decisions with their reasons, from Appendix Z: the asymmetric speed clamps;
containment rather than death at the vertical bounds; force rather than impulse; and the
single state owner.

For each one, state what the alternative was and what choosing it would have cost. Do not
simply list the decisions.
```

### 4c — Measurement validity (highest-value section)

```
Still in section 7, add a subsection titled "The Player Controller as Control Variable" of
about 400 words.

Connect these guarantees from Appendix Z to the validity of our difficulty investigation:

- death fires exactly once per run, so run boundaries are unambiguous and recorded distance
  is trustworthy
- ResetRun restores an identical starting state, so run n+1 is comparable to run n and any
  improvement is attributable to the player rather than a drifting start state
- a coin counts exactly once, and score is one of our dependent variables
- the jetpack flame and shield bubble do not intensify with difficulty, which supports the
  presentation-confound control already stated in section 3.6
- horizontal position is locked every tick without exception, so distance is a pure
  function of elapsed time and scroll speed and is therefore a valid progression measure

Explain that each guarantee exists so that two runs are comparable.
```

### 4d — System ownership

```
Fill in section 8.3, System Ownership, as a table with columns System, Owner, Interface it
exposes. Use Appendix Z for the player row: list the four read-only properties, the four
events, and ResetRun. Use sections 3.2 to 3.7 of this document for the other rows, and
assign owners as: player systems to Ako Baloyi, world, obstacles and spawning to Linky
Nzuza, UI, scoring and game state to Unarine Maswime.

After the table, add two short paragraphs on how the player system's one-directional event
flow let three people build concurrently, and where the interfaces are fragile: tag strings
fail silently if misspelled, and the coin magnet spans two systems with no agreed owner.
```

### 4e — Iteration and process

```
In section 10, Reflection, add a subsection titled "Player System: Iterative Process" of
about 600 words.

Work through all eight iteration items in Appendix Z. For each, state what the original
plan was, what it became, and why it changed. Keep the reasons concrete.

Write item 6, the sprite metadata failure, plainly as a failure and include the lesson
about optimising for personal convenience rather than robustness. Do not soften it.
```

### 4f — Risk management

```
Fill in section 8.5, Risk Management, as a table with columns Risk, Likelihood, Impact,
Mitigation.

Include from Appendix Z and this document: shared Unity files edited by all three members
causing merge conflicts, specifically the Tag Manager, Editor Settings and the shared
scene; misspelled collision tags failing silently; a cross-system feature with no agreed
owner, using the coin magnet as the example; late integration if builds are not produced
regularly; and parameter values reaching playtesting untuned.
```

---

## STEP 5 — Quality pass

Run these after the writing is in. They catch the things Gemini does badly by default.

```
Review only the subsections you added to sections 7, 8.3, 8.5 and 10. Find every sentence
that states a technical fact without connecting it to player experience or to the validity
of our difficulty investigation. List them so I can cut or fix them.
```

```
Check the subsections you added for any claim not supported by Appendix Z, and for any
claim that our parameter values have been tuned through playtesting. Appendix Z states they
have not been. Flag anything that overstates what we have done.
```

```
List the words from my banned list that appear anywhere in the sections you added, with
suggested replacements.
```

---

## STEP 6 — Delete the appendix

Delete the whole of Appendix Z from the document. It is scaffolding, not a deliverable, and
its presence would tell a marker how the section was produced.

Then read the sections yourself and rewrite at least the reflection in your own voice. A
marker can tell the difference, and the reflection carries marks for genuine engagement
that generated prose will not earn.

---

## Notes and gotchas

- **Adding a file as a source restricts Gemini to that file.** If you use the Sources
  button or `@` to attach something, it may stop using the rest of your document. The
  appendix method avoids this.
- **Accept suggestions in small batches.** Reviewing one at a time is the point of the
  inline suggestion flow. Accept All on a 600-word generation is how errors get through.
- **If a response drifts generic, the fix is a narrower prompt**, not a longer one. Split
  the request rather than adding more instructions.
- **Your timeline in section 8.4 runs 12–17 August** and has passed. Reconcile it with what
  actually happened before submitting, or it will read as a plan that was never followed.
- **Section 6, Design Goals, is still empty** and is a group section. Your player-side
  contribution to it is the invariance argument from Step 4c.
