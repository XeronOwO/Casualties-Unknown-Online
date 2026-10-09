# Random rolls in a cross-player item use: can one temporary deterministic random window keep both sides in step?

- Status: Todo — **reported 2026-10-08 by the user**, who asked for the ticket now and the investigation
  later: "对于带随机性的效果来说，会导致不同步，能不能通过临时的、确定性的、统一的随机状态控制来消除这种
  不同步性？". **Steps 1-3 are done (2026-10-09, code-only — see *Investigation*): the divergence does NOT
  reach authoritative state** — every authoritative value of this family has exactly ONE roller — so the
  priority stays High rather than rising to Critical. What is left: step 4 (the two-client reproduction)
  and the disposition of the two display-side double-rolls §3 names.
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
- **Presentation divergence: the one genuine two-roller shape**, and it is transient. The operator sees the
  values its own roll just wrote onto the display clone until the authoritative values land: for shrapnel
  the host's state message follows immediately (the break-grasp report is what triggers the host's roll),
  and for the measured chains the display advances from the affected side's own report.

### 4. What that means for the candidate mechanisms

- **A — the temporary deterministic window: not needed, and not sufficient.** There is no second
  authoritative roller for a window to synchronize, and the one double-roll site cannot be covered by one:
  the host's roll happens after the operator's report crosses the wire, so no window on either machine
  spans both rolls.
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
by which side owns the body, with the roll's RESULT travelling as a fact. The engine-global window of
mechanism A would put a second owner on that resource for no gain this family can name.

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

> **[Answered 2026-10-09 by §Investigation 3-4.]** The classification is presentation (plus the recorded
> cadence limit), so A and D are not needed and B is already the family's shape; C is the only mechanism
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
