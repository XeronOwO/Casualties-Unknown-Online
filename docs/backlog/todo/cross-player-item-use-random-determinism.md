# Random rolls in a cross-player item use: can one temporary deterministic random window keep both sides in step?

- Status: Todo — **reported 2026-10-08 by the user**, who asked for the ticket now and the investigation
  later: "对于带随机性的效果来说，会导致不同步，能不能通过临时的、确定性的、统一的随机状态控制来消除这种
  不同步性？". Nothing has been investigated yet: the claim below is the report, not a finding, and this
  ticket's first job is to establish which of the three divergence classes it really is.
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

## Establish first (investigation order)

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

- Judge ownership holds: the side that owns the body judges, and latency never becomes a parameter.
- Determinism for a late joiner and for a replay: whoever arrives mid-effect must land on the same outcome.
- No new global mutable state owned by nobody; the project's rule is that state belongs to its owner.
- Whether the save must reproduce the outcome at all: `GameCheckpoint.RandomStreams` is decided-empty today
  ("no kernel domain consumes RandomStreams"), and mechanism A would keep it that way — an argument in its
  favour worth weighing against its global-window cost.

## Acceptance shape (write the rows during the investigation)

- A two-client run over an entry with a per-call roll: the outcome on the affected client and what the
  operator's client showed agree, read back on both sides.
- The same entry replayed for a late joiner lands on the same outcome.
- No random read outside the window is perturbed: a control case whose own roll must stay unaffected.

## Non-goals

- Not the world generation baseline (`run.randomState`) or the kernel's `RandomStreams`, both already decided.
- Not a general deterministic-simulation framework: this is the cross-player item-use family only.
- Not the effects authoring surface itself — that is `mod-authored-effects.md`.
