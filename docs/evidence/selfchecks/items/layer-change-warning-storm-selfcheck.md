# The Layer-Change Warning Storm — Self-Check (2026-10-06)

Delivery fact sheet for `docs/backlog/todo/layer-change-member-dropout.md`, whose evidence batch `20261005-b`
lost a session to: after a layer change the two members were out of the world and the guest's rolling log grew
from 0.8 MB to 33.4 MB in about four minutes (`docs/evidence/acceptance/guest-command-loss-reconciliation-20261005-b.md`,
Limits). The cycle bounds the diagnostics that produced that growth; the member-recovery half is filed
separately with its fixture, and the reason is stated in §4.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The batch's own reading: the member had no local body at +4.1 s after the change (`container-read mode=local` → `no-local-body`), the destroy probe then found nothing to destroy, and a SECOND layer advance follows the first on its own about 9 s later — so one command is already a consecutive change. Nothing recovered on its own; all three clients were restarted cold. | the batch record's Limits; the probe artifacts `r4b-guest-local-t2.json` / `r4b-guest-local-t3.json` (`"error": "no-local-body"`) in the batch's artifact directory |
| 2 | The batch's own record juxtaposes the growth with the repeating `[LayerMod] baseline divergence` warning, and that warning is emitted per ARRIVING SNAPSHOT: `LayerModifierSync.ApplyIndex` runs from both the periodic world-item snapshot and the item snapshot, each of which carries the layer-modifier index and random state. | `LayerModifierSync.OnWorldItemsSnapshot`/`OnItemSnapshot` → `ApplyIndex`, the warning's own text |
| 3 | That snapshot stream is the 5-SECOND KEYFRAME, not the 10 Hz movement stream: `AdaptiveStreamId.WorldItemSnapshotStream` is `BaseHz: 1, BaseIntervalMs: 5000, MaxIntervalMs: 10_000` (and `AdaptiveStreamProfile`'s own note is that a non-zero `BaseIntervalMs` takes precedence over `BaseHz`), and the wire mapper sends it as `WirePayloadType.ItemSnapshotStream`. Four minutes at that cadence is a FEW DOZEN lines, so the warning cannot produce 33 MB — and the two in-repository readings agree: the sibling record says the same warning repeated "every 10 s", and `ItemPositionAuthority`'s class note calls this stream "the 5 s keyframe fallback". Measured in the surviving log: 83 `World-item snapshot received` lines over 22:33:23.660 → 22:45:51.477, consecutive gaps 8.321 s / 8.358 s, i.e. ≈29 lines in four minutes. | `AdaptiveStreamCatalog` (`WorldItemSnapshotStream`), `AdaptiveStreamWireMapper`, `AdaptiveStreamProfile`; `done/layer-mod-baseline-divergence-on-continue.md`; the surviving log's own snapshot lines |
| 4 | The volume is the item follow pump's correction line: `[ItemPhysics] settle` is Information and sits inside the per-frame ease branch, so a copy whose gap to the host's state does not close writes one line PER ITEM PER FRAME. In a diverged world that is every item, indefinitely. | `ItemPositionFollow.Update` → `ReportDivergence`; `ItemFollowDecision.Decide` (`LogDivergence = dist > ItemMotionState.SettleLogDistance`), measured below |
| 5 | The measurement that attributes it: in the guest client log still on disk, `[ItemPhysics] settle` is 4,445 of 7,368 lines — 60 % of the whole file from that one line, with `[Fluid] region` second at 415. The storm's own log is gone (the client was restarted and its current log belongs to batch `20261006-h`), so the split is corroborating evidence and NOT a re-measurement of the storm. | the guest rolling log named by the acceptance area's `sandbox-guest-root`; the counts are a session measurement, reproducible from that log |
| 6 | The snap line is the same shape one branch over: `[ItemPhysics] snap` is Information and fires per frame for any copy a diverged world keeps pushing past `ItemMotionState.SnapDistance`. | `ItemPositionFollow.Update`'s moving branch |
| 7 | One receive handler writes an Information line per arriving message on a 10 Hz stream: the fluid region handler did not bound its log, while the SENDER of the same stream logs at Debug (`FluidSimulationAuthority`). | `FluidRegionHandler.Handle` (pre-fix), `FluidSimulationAuthority`'s `LogDebug`, `AdaptiveStreamId.FluidRegionDiffStream` ("the 10 Hz fluid changed-region diff stream") |
| 8 | The level policy this violates is the repository's own: an unobservable key path is unfinished, and the level is chosen by trigger frequency — high-frequency → Verbose/Debug, low-frequency or exceptional → Warn/Error. | `AGENTS.md`, Engineering Discipline |
| 9 | The divergence DETECTOR is genuine and stays: it caught a real defect once (a Continue that reopened the wrong world, both sides generating different baselines), and that cycle's own answer to "is divergence expected here?" is that a member generates the host's world from the same run baseline, so a mismatch means the baselines really differ. | `docs/backlog/done/layer-mod-baseline-divergence-on-continue.md` (its symptom shows the same warning at the same 10 s cadence), `LayerModifierDecide.OnSnapshot` (`BaselineDiverged` = the two decision-entry states differ) |
| 10 | The world's frame-level draws between the segment restore and the modifier decision leak into the public stream per-side and are frame-rate dependent (observed 123-151 ms windows) — which is why BOTH sides rewind the decision to the last segment start, and why a snapshot's random state is comparable to the local entry state at all. | `LayerModifierApplyPatch`'s class note, `WorldGenRandomIsolation.LastSegmentStart` |
| 11 | The member's absence from the world is NOT attributed: the batch's own evidence is the two facts in row 1, and the guest's `state` probe during that session reads `inWorld: true` / `role: Guest` / `gateWaiting: false` while the local body is missing, so the member believed it was in the world. Nothing in this cycle's reading explains why the game did not rebuild its body, and no fix is claimed. | `r4-guest-state.json` / `r4-alt-state.json` in the batch's artifact directory, the batch record's Limits |

## 2. The change

- `LogRepetitionGuard` (Runtime): the repetition window a repeatable diagnostic asks, keyed on the SUBJECT
  it reports (and the key carries the value). Its first `suppressAfter` lines pass with a `repeat` index,
  identical ones after that are refused with a running count, and `TryFlush` ends a subject — dropping the
  entry — which is what re-arms a window so a divergence that resolves and later returns reports its first
  line again. Entries are capped, oldest first, so a subject that keeps moving costs bounded memory.
- `DistanceLogWindow` (Runtime): what a window is still swallowing, per subject, in first-seen order, with the
  last value seen — capped, so a diverged world cannot trade a log storm for a memory one; past the cap
  `Add` returns false and the caller is told the line is counted nowhere.
- `ItemDistanceLog` (Runtime): the two per-frame correction lines (`settle`, `snap`), each behind its own
  window and its own counter, keyed by (item, distance bucket) so a gap that moves is news again;
  `Finished` closes a subject, writes back its count AND re-arms the window.
- `ItemPositionFollow`: the settle line and the snap line are now written through that shell. A refused line
  is counted, the item's own recovery reports how many lines it cost and at which distance band, and the pump
  flushes every open window when the follow table empties (world teardown, layer change, session end) — the
  story stays complete instead of being half-told by a bound.
- `LayerModifierSync`: both snapshot diagnostics (the index disagreement and the baseline divergence) are
  keyed on the pair that IS the divergence, so a standing pair reports its first lines and then one summary
  when it changes, when the layer is left, or when the session ends (all three flush; the session-end flush
  is `Unbind`, the same as the item pump's). The detector, its wording and its level are untouched.
- `FluidRegionHandler`: the region's rectangle is the key — a rectangle's first regions report at
  Information, repeats fall to Debug, and a rectangle whose BOUNDS move reports again. Two named properties
  rather than implied ones: the key is bounds, not content (fluid changing inside unchanged bounds stays at
  Debug), and the window belongs to a process-lifetime singleton, so after a session boundary that rectangle
  is Debug-only with a bounded, accumulating count — the level policy's own answer for a per-tick stream.
- No wire member, no save shape and no protocol number changes; the divergence detector is not weakened (its
  output is bounded to a window per standing subject, and `Finished` re-arms on resolution); no
  member-recovery code is added (§4).
- **This cycle's independent review is folded in**: it reproduced the line counts (7,368 / 4,445 / 415) and
  replayed the window over the 4,445 real lines (44 written, 4,401 refused, 12 subjects), and it is the
  reason this sheet no longer claims what the code did not do — (a) the `[LayerMod]` windows are now
  genuinely flushed in `Unbind` as well, (b) the cadence figure is the 5-second keyframe and a few dozen
  lines (row 3), not the 10 Hz movement stream and 2,400, which was this cycle's own error, (c) a divergence
  that resolves and returns at the same band is the case `Finished`'s re-arm and the new test cover, (d) the
  guard's "per distinct value, not per key" wording — dead in production, because every caller's key carries
  its value — is gone, (e) the gate's `LogDebug` matcher reads a CALL and has samples, its line matcher
  requires the log to be GATED by the window, and the census floor is the real one.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The window's contract: first line reported, repeats counted up to the window, identical lines after it refused with a running count, another subject news with its own clean window, the window ended and re-armed by a flush, keys independent, the oldest entry dropped at the cap, 1,000 identical frames costing the window only, a zero window refused | `tests/CasualtiesUnknownOnline.Tests/Items/LogRepetitionGuardTests.cs` — 16 cases, all green |
| The per-subject counter: accumulation, separation between subjects, insertion-order flush, the capacity cap with its `false` | the same file's `DistanceLogWindow` cases |
| The two-line shell: each kind keeps its own window and counter, the count moves with the distance band, finishing hands back what was swallowed exactly once, and a divergence that RETURNS at the same band reports its first line again (the review's finding) | the same file's `ItemDistanceLog` cases |
| The producers ask the window, and the 10 Hz receive handler is bounded | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/LogVolumeGateTests.cs` — 14 cases: the item follow pump's two lines must ask AND gate their log, `LayerModifierSync` must ask both diagnostics and drain them in one place, `FluidRegionHandler` must ask and must keep a Debug CALL (matched through Roslyn, with a comment-only negative sample), three census floors (four item sites, three layer-mod sites, one handler site) and three matcher theories with positive and negative samples each |
| The mutation | the settle line's window call was replaced by `if (false)` on the fixed tree: `LogVolumeGateTests` went RED naming `ReportDivergence` and the settle line, the other 13 cases green — the first mutation (both window calls removed from `ReportDivergence`/`ReportSnap`) did the same — and restoring the file from a byte-identical backup turned it green again (SHA-256 compared before the backup was deleted) |
| The pre-fix RED | the gate's matchers were written against the fixed tree and are not the red step: this cycle's red is the mutation above plus the guard's own cases being written first (they failed to compile against the missing types, which `AGENTS.md` does not count as a red — the RED this cycle claims is the mutation, recorded as such) |
| The runtime row | the next three-client batch re-drives the batch's staging: one `skiplayer` command and the 9-second follow-up advance that comes on its own, with the two members staying in the world (a local body on each, `container-read mode=local` answering), the host's `[LayerReset]` line seen twice as the batch saw it, and the guest's log growth over the window read as a SIZE (bounded: no per-frame correction storm) with the divergence lines present as a window plus at most one summary per subject |

## 4. Limits

- **No session ran in this cycle.** The reading is the batch's own record and artifacts, the client log still
  on disk, the adaptive-stream catalog and `reversing/`; the fix's runtime proof is the next batch's re-drive,
  and this cycle claims no green runtime row.
- **The storm's own log is gone, so the attribution is corroboration and not a re-measurement.** The guest
  client was restarted and its current rolling log belongs to batch `20261006-h`: the 4,445-of-7,368 split
  measures THAT session — the same SHAPE (one item at a standing d=1.02 for 4,438 frames), not the same
  event — and the 33.4 MB figure is the batch record's own sentence. The batch's record *juxtaposes* the
  warning with the growth rather than asserting causation, and this cycle's correction is to that reading.
- **The layer-mod cadence is read from the catalog and from a neighbouring measurement, not counted.** The
  stream is the 5-second keyframe (`BaseIntervalMs: 5000`, `MaxIntervalMs: 10_000`); the 83
  `World-item snapshot received` lines at 8.3 s intervals are the surviving log's own snapshot stream, and no
  log of the divergence itself survives, so its line count can be bounded (a few dozen over four minutes) but
  not counted.
- **The member's exit from the world is untouched.** No fix is claimed because the cause is not attributed
  (§1 row 11): the member's own state probe says it was in the world, the local body was absent, and nothing in
  this cycle's reading explains that. It is filed as its own ticket with the fixture the next cycle must run
  first (`docs/backlog/todo/layer-change-member-recovery.md`), which is where item 3 of this ticket's Required
  work now lives.
- **The bound is one window per subject, and that is a behaviour choice.** A divergence whose key changes every
  frame still reports every frame; the item key uses a 0.05-unit distance bucket rather than the raw distance
  precisely so that a standing gap stops moving, and a gap that slides across a bucket boundary costs the two
  bands' windows (16 lines) and no more. A divergence that ends and RETURNS is the case `Finished`'s re-arm
  covers.
- **The fluid window is per process and keyed on bounds, not content** (see §2): a rectangle whose INTERIOR
  changes without its bounds moving stays at Debug after its window, and the accumulating count survives a
  session boundary. Bounded and at the level the policy asks for a per-tick stream, but named here rather than
  implied.
- **The family sweep is bounded by a census, not by a rate model.** The gate pins the sites this cycle read
  (the two correction lines, the two snapshot diagnostics, the fluid receive handler); a NEW per-frame or
  per-10-Hz Information line elsewhere would not be caught until it is named here — which is what the ticket's
  item 4 asks for and what the census floors make loud rather than silent. One such line was checked and left
  alone as demonstrably low-frequency: `ItemSnapshotService`'s own `World-item snapshot received` (83 lines in
  12.5 minutes).
