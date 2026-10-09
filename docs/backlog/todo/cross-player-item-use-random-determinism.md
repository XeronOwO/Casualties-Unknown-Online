# Random rolls in a cross-player item use: can one temporary deterministic random window keep both sides in step?

- Status: Todo — **reported 2026-10-08 by the user**, who asked for the ticket now and the investigation
  later: "对于带随机性的效果来说，会导致不同步，能不能通过临时的、确定性的、统一的随机状态控制来消除这种
  不同步性？". **Steps 1-3 are done (2026-10-09, code-only — see *Investigation*): the divergence does NOT
  reach authoritative state** — every authoritative value of this family has exactly ONE roller — so the
  priority stays High rather than rising to Critical. What is left: step 4 (the two-client reproduction)
  and the two divergences on screen §3 names. **The user then set the target for those (2026-10-09): remove
  the divergence WITHOUT waiting for the authority — random-state control, not a local guess and not a
  delay.** *The target the user set* carries the per-site answer: the grip-slip window is safe and gives
  immediate-plus-correct, and the measured-use site is done with a comparison that decides between the same
  window and a side-effect-free local run. Ticket only this round, no implementation.
- Priority: High — **raise to Critical if the investigation shows the divergence reaches authoritative state**
  (a body value, a limb fact, an item amount). Presentation-only divergence stays High.
- Category: Players / cross-player semantics
- Related: `docs/backlog/review/mod-cross-player-native-semantics.md` (the entry census and the two anchors
  quoted below), `docs/backlog/review/remote-medical-treatment-operations.md`,
  `docs/backlog/review/remote-medical-stage-1-injection-session.md`,
  `docs/backlog/review/concurrent-medical-operations.md`,
  `docs/backlog/todo/mod-authored-effects.md` (its census already counts the per-call rolls),
  `docs/backlog/todo/mod-content-attribute-declarations.md` (a mod-authored effect is what would make this
  worse), decision 246 (the item-gesture mapping), `docs/en/internals/judgment-ownership.md`.
- Source: the user's 2026-10-08 request.

## What the report says

Every cross-player item use runs as **local execution on the side that owns the body, plus a projection on
the other side**: the operator's client shows the gesture and its feedback, the affected client (or the host,
per the entry) runs the game's own effect and the network layer carries the fact. Where the effect consumes
randomness, the two sides can roll differently, so what the operator sees and what actually happened part
company.

Two anchors already recorded in the census ticket, and they point in opposite directions — which is exactly
why the investigation comes first:

- The drink chain deliberately moved the roll to the receiving body: "the per-call random rolls therefore all
  start on the body they land on, with no CUO message in between." There the target is the only roller, so a
  divergence would be in the operator's projection, not in the state.
- The injection chain records the opposite shape as a LIMIT: the dose call count on the target follows the
  ACCEPTED DELTA cadence, so `WaterContainerItem.Inject`'s per-call term and "any per-call random roll
  therefore follow the message cadence". A roll whose COUNT depends on coalescing is not reproducible by
  anyone who did not see the same cadence.

## Investigation (2026-10-09, code-only)

Steps 1-3 of *Establish first* are done; step 4 is not. **Nothing below was observed in a running game** —
every claim is a call-site reading of `src/` (quoted, no line numbers) plus `reversing/` (file:line, that
tree is never edited), and the acceptance rows below are what a session has to confirm.

### 1. Entry census (three columns)

| Entry | Which side runs the native effect | Which side runs a projection | Does the projection path read randomness |
|---|---|---|---|
| injection (`WaterContainerItem.Inject`) | the AFFECTED side: `NativeInjectionApply` runs the liquid's own `onHealthUse` on the patient | operator measures the ml (the native call is diverted) and the host builds the drain | no — the divert swallows the call before the liquid body runs, and the host's plan is arithmetic (`LiquidDrainPlan`) |
| drink / consume (`WaterContainerItem.Drink`) | the AFFECTED side: `NativeDrinkApply` runs each liquid's own `onDrink` | operator measures the ml; host builds the drain | no, same shape |
| topical / apply (`WaterContainerItem.ApplyToLimb`) | the AFFECTED side: `NativeTopicalApply` runs `onHealthUse` | operator measures the ml; host builds the drain | no, same shape |
| limb tool (the item's own `useLimbAction`) | the AFFECTED side: `NativeLimbToolApply` | the operator runs nothing (the chain never measures) | — |
| solid food (the item's own `useAction`) | the EATER | operator measures on the display clone | the item's own action can roll, and on the operator's client it rolls against the DISPLAY clone |
| wear | nobody: a placement on the snapshot, no native action | host places, affected side parents the garment | no |
| bandage, splint/tourniquet removal, dislocation, amputation, AED, manual defib | the HOST: `OtherMedicalOperationApplier`, reached only from the host-guarded update handler | every client applies the host's state message | no — the projection copies facts into the display |
| shrapnel | the HOST: `ShrapnelSessionStateWriter.ApplyBreakGrasp`, reached only from the host-guarded update handler | every client drives its own display `ShrapnelMinigame` | **yes — §3** |
| heal (the heal slice) | the HOST: `PlayerHealService` calls `RemoteHealApplication.Apply` with a profile of constants | every client applies the result | no |
| CPR | not implemented (`todo/remote-medical-cpr.md`), so there is no path to read | — | — |

### 2. Every random read on those paths

**Authoritative, and reached from exactly one machine:**

- The affected side, inside the game's own effect bodies (this is the single roller the migrated chains
  were built around): `Liquids.cs:16-115`, `:362`, `:525-557`, `:1059` and `:1090`
  (`Random.value < ml / 80f` — the disinfectant's infection chance), `:1144`, `:1353` and `:1370` (cardiac
  arrest), `:1669-1755` (sickness amounts); `Item.cs:2485`, `:2537-2539`, `:3544-3546`, `:3618-3887` (an
  item's own outcome table), `:3913-3915`, `:7153-7156`; `Limb.cs:193-208` (`Dislocate` — the timer and the
  pain), `:221-247` (`BreakBone` — bleed, pain, the head's disfigure roll), `:535` (the limb update's
  infection roll).
- The host, in CUO's own code: `OtherMedicalOperationApplier` — the dislocation pain
  (`limb.Pain + (wrench ? RandomRange(4f, 10f) : RandomRange(15f, 24f))`) and the defibrillation verdict
  (`if (chance >= 1f || Rng.NextDouble() < chance)` deciding `health.FibrillationProgress = 0f` and the
  blood-pressure halving); `ShrapnelSessionStateWriter.ApplyBreakGrasp`, which mirrors the native formula
  read for read (`RandomInRange(4f, 6f)` skin, `(0.4f, 1f)` bleed, `(9f, 16f)` pain, the head's `0.8f`
  gate and `(0f, 1f)` brain health). Both sit behind a host-only guard
  (`_session.Role != SessionRole.Host` in the update handler that reaches them), and both write the
  AUTHORITATIVE character data, which then travels as a fact.

**Rolled twice — once per side, and only one of the two is authoritative (§3):**

- `ShrapnelMinigame.cs:61-77` (`BreakGrasp`): the operator's own client runs it against the limb of the
  DISPLAY body it built for the session (`StartRemoteShrapnel` hands it `display.limbs[limbIndex]`), rolls
  skin/bleed/pain/brain there, and reports `BreakGrasp = true`; the host then rolls its own values for the
  same event on the target's real data.
- The measured use action of the solid-food chain: the operator runs the item's own `useAction` on the same
  display clone to measure it, so any roll inside that delegate (the `Item.cs` range above) is a second,
  discarded roll.

**Presentation only, rolled per client and never carried:** clip variants (`Limb.cs:231`, `Body.cs:1912`,
`:2105`, `:3466`, `Item.cs`'s own sound picks), gore and decal geometry (`WorldBloodReplay.cs:30-49`),
the broken-bone sprite's rotation (`Limb.cs:255`) and the clone's (`CloneLimbRenderer.cs:147`), the blood
drip multiplier (`Limb.cs:422`), the camera's vignette/droplet effects (`PlayerCamera.cs:529-591`), and
minigame UI jitter (`ShrapnelMinigame.cs:50`, `AEDMinigame.cs:49`, `ManualDefibMinigame.cs:80`). These
are differences in what a screen shows, and the tree already has the pattern for the one case where such a
variant must agree: `FluidPresentationMsg` carries the roll's RESULT because the sound index is relayed.

### 3. Classification

- **State divergence: none found.** Every value the family commits has exactly one roller — the affected
  side for the five migrated chains (injection, drink, topical, limb tool, solid food) and the host for
  the Stage-3 operations, shrapnel and heal. CUO's own two rolling sites are both host-side and both feed
  the authoritative data. The mechanism inventory confirms it from the other end: the only `Random` in
  `Runtime/Session/PlayerInteraction` is those two files, and every other consumer of randomness in
  `GameAdapter` belongs to worldgen, traders, explosions or replay jitter, not to this family.
- **Cadence divergence: real, and already recorded.** The injection chain's per-call terms follow the
  accepted-delta cadence rather than the operator's frame count (the limit quoted in *What the report
  says*). Every peer agrees on the result; what differs is the result a LOCAL use would have produced.
- **A divergence on screen, and it is a divergence, not a wording problem: the one place that rolls
  twice.** Two sites run the same effect twice, once per machine: `ShrapnelMinigame.BreakGrasp`
  (`ShrapnelMinigame.cs:61-77`) runs on the operator's client against the display clone and the host rolls
  the same event again in `ShrapnelSessionStateWriter.ApplyBreakGrasp`; and the measured use action of the
  solid-food chain runs the item's own delegate on the operator's client while the eater runs it for real.
  In both, the operator's screen holds a value for a moment that NO other peer ever holds — calling that
  "presentation" describes where it lands, not how real it is: until the authoritative value arrives, that
  screen is wrong. It is bounded (the host's state message follows the break-grasp report immediately, and
  the display advances from the affected side's report) and it is not the state, but "short-lived" is not
  "not a divergence", and the fix for it is mechanism C rather than mechanism A.

### 4. What that means for the candidate mechanisms

- **A — the temporary deterministic window: not needed for state agreement, and not sufficient there.**
  There is no second authoritative roller for a window to synchronize in the five migrated chains or the
  host-applied operations, and the host's roll in the injection cadence case happens after the operator's
  report crosses the wire, so no window on either machine spans both rolls. It IS the mechanism for the two
  double-roll sites, per site and with the safety net described in *The target the user set* — that is the
  narrow, deliberate exception rather than a family-wide rule.
- **B — carry the outcome**: this is already the family's shape wherever the outcome is a fact (the drain
  plan, the state message, the healed health). It is the mechanism to reach for if a future roll ever
  decides something the current facts do not carry.
- **C — no roll on the projecting side**: the only mechanism that addresses what §3 actually found.
  It applies to exactly two sites (the display-side `BreakGrasp` and the measured use action) and would
  mean the display shows the authoritative value only.
- **D — a CUO-owned action-scoped stream**: the reads are not behind one seam — `Liquids.cs`, `Item.cs`,
  `Limb.cs`, `Body.cs` and `ShrapnelMinigame.cs` call `UnityEngine.Random` directly — so D would have to
  intercept every one of them, and §3 gives no state reason to.

The state-preserving invariant worth keeping, now stated: **one roller per authoritative value**, decided
by which side owns the body, with the roll's RESULT travelling as a fact. For the family at large that needs
no window at all; the ONE place a window IS the answer is the pair of double-roll sites, and each of them is
decided on its own in *The target the user set* below.

### Verified chain: a nausea-causing liquid given to another player (2026-10-09)

The user asked this case by name ("我给其他玩家引用引发反胃的液体"), so the chain is written out rather than
left inside §1's row. Call sites, in order:

- The roll itself lives in the LIQUID's own body: `groundwater`'s `onDrink` rolls `Random.value > 0.5f`
  and then `Random.Range(7f, 15f)` into `body.sicknessAmount` (`Liquids.cs:1663-1678`), and `lumalgae`
  rolls `Random.value > 0.35f` into the same field plus `Random.value < 0.1f` into
  `body.vomiter.Vomit()` (`Liquids.cs:1690-1707`). `onDrink` is called from exactly one place in the
  game: `WaterContainerItem.Drink`'s per-stack loop (`WaterContainerItem.cs:198-215`).
- **The affected side runs it.** `NativeDrinkApply.Apply` calls `liquid.onDrink(stack.Amount, body)` on the
  drinker's own client, in the drinker's own frame.
- **The operator's side cannot run it.** `RemoteDrinkUseHandler.TryMeasure` runs the item's own
  `useAction` (`info.useAction(drinker, dragItem)`) with a capture window armed; the item delegates that
  drink
  are bare `container.Drink(body, N, "drink")` calls (`Item.cs:1869`, `:3171` and the rest of the
  `WaterContainerItem.Drink` rows), and `RemoteDrinkPatches`' prefix on `WaterContainerItem.Drink`
  swallows that call while the window owns the container — so no `onDrink`, no roll, and no `Drain` on the
  operator's client. (`Harmony` binds the prefix's `float amount` by name, so the prefix really is on the
  overload that runs the liquid body.)
- **The field travels.** `sicknessAmount` is part of the character report
  (`CharacterHealthMsg.SicknessAmount`) and is written onto the operator's display clone by
  `RemoteCharacterDisplayProjection`. The host's copy of the drinker advances from the same report.

So for THIS case two peers never hold different values — not even briefly: the only machine that rolled is
the one whose body it is, and the other side's copy of it is report-driven. What differs is WHEN the
operator learns the value, which is the ordinary report cadence every field shares. The cases where a
screen really can show a value nobody holds are the two in §3, and there the honest word is divergence, not
"presentation only". Both are answered by random-state control rather than by waiting — per site, with the
caveat each one carries — in *The target the user set* below.

## The target the user set (2026-10-09): no divergence AND no perceived latency

The user's instruction: "能不能消除延迟的同时尽可能消除延迟感？能做到吗？只改票，不在本轮开发". So the ticket
now carries the target and the way to reach it; nothing is implemented this round.

**Why waiting is not actually required, and where it is.** The user asked why there must be any waiting at
all: control the random state and both sides compute the same value, so the local side can show it at once.
That is right, and it is mechanism A scoped to the two sites rather than to the family. What decides whether
it works is one question per site: **do both runs consume the same draws in the same order?** A window can
only promise the same STREAM, not the same sequence of choices — so at a site whose draw sequence depends on
state the two sides hold differently, a shared seed can produce a *wrong* value that looks authoritative,
which is worse than a late one.

| Site | The draws | Verdict |
|---|---|---|
| shrapnel grip-slip — the native `ShrapnelMinigame.BreakGrasp` on the operator's client and CUO's own host-side mirror `ShrapnelSessionStateWriter.ApplyBreakGrasp` | unconditional, fixed order: skin `Random.Range(4f, 6f)`, bleed `Random.Range(0.4f, 1f)`, pain `Random.Range(9f, 16f)`, and for a head limb `Random.value < 0.8f` then `Random.value`. The only branch that steers them reads `isHead`, which both sides agree on | **SAFE, and it is exactly the shape the user asked for: immediate AND correct, no waiting, no divergence.** Both sides derive the stream from the operation id plus the slip's own sequence number (the report already carries one); the host's mirror stops drawing from `new Random()` — a defect in its own right, since it makes the authoritative value unreproducible by anyone, including a replay — and the operator's local run gets the state set and restored around the native call. The only other drawer inside that call is `DoGoreSound`'s clip pick, which then becomes deterministic on both sides and is relayed as such |
| measured cross-player use — the item's own `useAction` (solid food and the like) | inside a branch tree the GAME writes, keyed on the BODY's own state: `float num24 = Random.value * 100f;` then a chain of thresholds, with `body.Eat(...)`, hunger and `HoldingItem("filterstraw")` among the inputs (`Item.cs:3618-3887`) | **a shared seed cannot be trusted on its own here**: the operator runs that delegate against the target's DISPLAY clone (report-driven, up to one report old) while the eater runs it against the real body, so the two can take different branches, spend a different number of draws and land on different values — silently. Two ways forward, and the choice is a measurement rather than an opinion: (a) do it anyway WITH a comparison — the request carries the nonce the seed is derived from, each side derives its own numbers, the operator shows its value at once, and the authority's value is compared against it and a disagreement is logged. If that log stays empty across the acceptance run, the site keeps "immediate and correct"; (b) if it does not stay empty, this site's local run goes back to being side-effect-free (it measures the dose and writes no numbers) and the value arrives with the report |

**What the change costs, either way.** A CUO-owned derived stream (the roll moves off `UnityEngine.Random`
for that site, or the engine's state is set and restored around the call), the request carrying the nonce the
seed is derived from — a wire change, free before release (decision 241) — and, per site, the statement that
nothing else draws inside the window. The engine-global caveat is real but small here: the two sites are
short, synchronous calls.

**Acceptance rows** — these replace "is the wait perceptible" with something factual:

| # | Row | Fails when |
|---|---|---|
| 6 | during a grip-slip and during a measured cross-player use, neither screen ever shows a number that changes afterwards | any value visibly corrects itself |
| 7 | the derived-value comparison for the measured use stays empty: both sides derived the same outcome | a derived value disagreed with the authority's |
| 8 | the same two actions never read as a stall, and the immediate, roll-free feedback still plays locally | the action appears to do nothing |

Rows 6 and 7 are the seeded window's test, and row 7 decides whether the measured-use site keeps (a) or (b).
Row 8 is the target the user set in the first place. None of the three is judged from code: they need the
two-client run.

## Establish first (investigation order)

> **[1-3 done 2026-10-09 — see *Investigation*; 4 remains.]**

1. **Entry census with three columns.** For every entry of the cross-player item-use family (world drag =
   inventory use, wound-view drag = limb use, injection, drink, topical/apply, bandage/splint removal, CPR,
   wear, tool use), record: which side runs the native effect, which side runs a projection, and whether the
   projection path reads randomness at all. The family's entry mapping is decision 246 and the census ticket.
2. **Name every random read.** For each one: the decompiled call site (`reversing/`, file:line — that tree is
   never edited) and what it decides (a state value, an amount, a sound variant, a sprite). A roll that only
   picks a clip is not the same defect as a roll that picks a bleed or an infection value.
3. **Classify the divergence**, because the fix differs per class:
   - **state divergence** — the two machines end up with different body/limb/item facts (blocker);
   - **cadence divergence** — the roll count follows message coalescing rather than the intent, so the
     outcome differs from what a local use would have produced, even though every peer agrees;
   - **presentation divergence** — state agrees, the two screens showed different feedback.
4. **Reproduce it** before designing: a two-client run over an entry whose roll is observable on both screens
   (an injection and a drink, per the anchors above), with the outcome read back on both sides.

## Candidate mechanisms (evaluate, do not pick before step 3)

> **[Answered 2026-10-09 by §Investigation 3-4.]** The classification is: no state divergence, the recorded
> cadence limit, and ONE real divergence on screen (the two double-roll sites §3 names). So A and D are not
> needed and B is already the family's shape; C is the only mechanism
> the finding points at, and it has two named sites. The list below is kept as the evaluation it was.

- **A — one temporary deterministic window (the user's proposal).** Both sides derive a seed from the action's
  own identity (the request/operation id already on the wire, the run epoch, the participants), set the
  engine's global random state to it, run the effect, then restore the previous state. It removes the
  divergence without a new round trip and without the save carrying anything. What has to be true: the window
  is a GLOBAL resource, so no other consumer may read `Random` inside it — the tree already has one precedent
  for holding consumers off a stream (`WorldGenRandomIsolation` wraps the worldgen coroutine and the guest
  waits for the baseline before a single draw) — and the seed must be derivable on both sides with no
  additional message.
- **B — carry the outcome, not the roll.** The side that owns the body rolls, and the result travels as the
  fact the other side applies (the shape the drink chain and the cross-player catalogue already use: report
  facts, never re-derive). Cost: the fact has to express the outcome, so it is a wire/typed-contract decision
  — which puts it next to `mod-api-no-opaque-envelopes`, not beside the scanner.
- **C — no roll on the projecting side.** The projection shows feedback that does not depend on the roll (or
  waits for the report). Cheapest, and honest when the consumer is presentation only.
- **D — a CUO-owned action-scoped random stream.** Deterministic per action id, but it only works if every
  native read is intercepted; the game calls `UnityEngine.Random` directly, so this is the most invasive of
  the four unless step 2 shows the reads are few and already behind one seam.

## Decision criteria

> **[Judged 2026-10-09.]** Criterion 1 HOLDS and is the finding: ownership is intact and no latency term
> entered any of it — the affected side or the host rolls, never both. Criterion 4 loses its force with it
> (`GameCheckpoint.RandomStreams` stays empty because nothing in this family needs a reproducible stream);
> criterion 2 is what the acceptance rows above test; criterion 3 is why the invariant in §4 is stated as
> "one roller per authoritative value" rather than as a window.

- Judge ownership holds: the side that owns the body judges, and latency never becomes a parameter.
- Determinism for a late joiner and for a replay: whoever arrives mid-effect must land on the same outcome.
- No new global mutable state owned by nobody; the project's rule is that state belongs to its owner.
- Whether the save must reproduce the outcome at all: `GameCheckpoint.RandomStreams` is decided-empty today
  ("no kernel domain consumes RandomStreams"), and mechanism A would keep it that way — an argument in its
  favour worth weighing against its global-window cost.

## Acceptance shape (rows written during the investigation)

Step 4 of *Establish first* is these rows: they need a game process and belong to an acceptance batch.
Each is judged on what BOTH clients show and hold, not on one screen.

| # | Row | How it is judged |
|---|---|---|
| 1 | A bare-hand shrapnel pull that breaks the grip: the affected client's skin/bleed/pain before and after agree with what the host committed, and the operator's screen ends on those same values | read the limb values on the patient's own client and on the operator's after the state lands |
| 2 | The same pull replayed for a late joiner lands on the committed values, not on a fresh roll | a third client joins mid-session and reads the limb |
| 3 | An injection whose liquid carries a per-call roll (the `Liquids.cs` bodies above): the patient's own values and the operator's display agree once the report lands | both screens |
| 4 | A drink whose liquid carries a per-call roll: same | both screens |
| 5 | A control case outside the window: an unrelated local roll (a footstep or gore clip) on either client is not perturbed by any of the above | both clients' logs |

## Non-goals

- Not the world generation baseline (`run.randomState`) or the kernel's `RandomStreams`, both already decided.
- Not a general deterministic-simulation framework: this is the cross-player item-use family only.
- Not the effects authoring surface itself — that is `mod-authored-effects.md`.
