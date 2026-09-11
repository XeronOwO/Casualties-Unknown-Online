# Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Status: Review
- Priority: Medium
- Category: Sync / world state
- Source: found while fixing S3.2's blocker B1 (the restore replay wrote CUO's registry rows into the
  game's own 128-entry list and pushed the game's OWN rows out). That write path is gone, but the two
  tables still disagreed about what they held.
- Related: `docs/evidence/sync-coverage-matrix.md` row W2, `todo/save-mid-run-consistent-cut.md`
  (S3.2 fixed the restore path), `docs/architecture/save-archive-format.md` §3.4

## Problem

Two bounded tables tracked "blocks that carry partial damage":

- the GAME's live list `WorldGeneration.world.blockDamages` — cap 128, and when it is full it EVICTS
  THE OLDEST entry and keeps the newest (`WorldGeneration.cs:716-737`: the new entry is added, then
  `blockDamages[0]` is dropped);
- CUO's `BlockDamageRegistry` — cap 256, and when it is full it REFUSED NEW CELLS and kept the
  oldest (`BlockDamageRegistry.cs:26-27`, `:56-63`).

Past 128 live damaged cells the two tables held DIFFERENT cells: the game kept the newest 128, CUO
kept the first 256 it was told about. Two more things made the mismatch worse, and neither is fixed
by changing a number:

- the late-joiner snapshot's source was a `Dictionary` enumeration (`CaptureEntries`), so its order
  was not the game's order at all — the receiving side applied rows until its own cap refused the
  rest, which is a different set from the host's newest-128 in almost every case;
- the game's list also holds damage CUO never observed (the direct `DamageBlock(Vector2Int)` callers
  — footstep crushing, the spider burrow — are not hooked).

The eviction itself is not a silent forget. `WorldGeneration.cs:732-737` sets the evicted entry's
`damage` to `1E+10f` and calls `UpdateSprite()` first, and `BlockDamage.cs:18-44` routes
`damage/health >= 1f` to `Object.Destroy(spr.gameObject)` — so the cell's crack sprite is destroyed
and its accumulated damage restarts from zero on the next hit. A long multiplayer run therefore
loses mining progress on its oldest half-damaged cells, on whichever side hit the cap in its own
order.

## Resolution

**Decision (user, 2026-09-11): option 1 — align to the game's own policy — and do it by REMOVING the
second table rather than by teaching it the same eviction.**

The stronger form was chosen on purpose: giving the registry the game's cap and the game's eviction
would have left two copies of one set with a hand-maintained agreement between them, so the next
divergence would need the same fix again. Removing the copy makes capacity, eviction, enumeration
order and damage written by the unhooked callers all follow from one table, with nothing to keep in
step.

What landed:

- `BlockDamageRegistry` is deleted. The late-joiner snapshot reads the game's own list AT SEND TIME
  (`BlockDamageSnapshotSender` → `INativeWorldFacts.CaptureBlockDamages` →
  `GameBlockDamageTable.Capture`), so what a member receives is what the host's gameplay holds.
- The live report hooks no longer feed a CUO table (`BlockBreakSync`): a host that damages a block
  records nothing CUO-side, because the game's own list already is the record.
- `IWorldFactSource` lost its partial-damage half (the capture method and `ApplyFacts`'s
  `blockDamages` parameter), and `WorldFactApplyReport` lost its damage counters — the partial damage
  has no Runtime table to report on, and the adapter's own applier reports what its cap refused.
- The archive's `block-damage` row kind is gone; `native-block-damage` is the only damage row. An
  older archive's `block-damage` rows are an unknown kind and are SKIPPED BY NAME (§6), while its
  `native-block-damage` rows still restore — so the manifest `schemaVersion` deliberately did not
  move, and an old archive degrades in a named way instead of being refused.
- `WorldStateMessageService` grew a collaborator (`BlockDamageSnapshotSender`) to own that send path,
  which also kept the type under the 600-line architecture gate.

Deliberately NOT done — the game's own cap is untouched. Option 2 (raise the bound on both sides) is
a gameplay change and was rejected here: the cap is a literal inside `WorldGeneration.DamageBlock`
(`ldc.i4 128`), and `GetBlockDamage` is a linear scan that same method calls on every hit, so a
larger bound both patches game code and scales the per-hit cost — while still not removing the
defect, only delaying when the eviction wipes a cell's accumulated damage. If that is ever wanted it
needs its own design and an in-game proof.

## Acceptance

| # | Scenario | Expected | Result |
|---|---|---|---|
| 1 | Long run, more damaged cells than the cap | Host, every connected guest and a late joiner hold the SAME damaged-cell set | HOST side closed: there is one table, and the snapshot is that table's rows read at send time, so what a member is sent cannot diverge from the host's gameplay table. Guest side CONVERGED, not guaranteed: a receiver whose own list is full for cells the host never saw refuses rows — named per cell in the log. Runtime-proven by `BlockDamageSnapshotTests` (verbatim rows, send-time read) and `WorldSnapshotWorldFactsTests`; the crack-render half still owes the dual-client pass. |
| 2 | Eviction | A cell leaving the table leaves it on every side (no side keeps a crack the authority dropped) | Closed for the CUO side by construction (no second set that could keep a stale cell). The guest's own list still evicts on its own FIFO order, and the host sends in the game's list order (oldest first), so a receiver short of room keeps the OLDEST host rows — the reverse of the game's own keep-the-newest policy. See the residual note below. |
| 3 | Unhooked writers | Damage written by the `DamageBlock` callers CUO does not hook reaches the shared view, or the gap is named in the report | Host side CLOSED: the snapshot reads the game's list, so unhooked host-side damage is carried. Guest side NAMED, not closed: a guest's own unhooked damage never reaches the host, and `GameBlockDamageTable.Decide` logs `RefuseCap` by cell when the guest's list cannot take a snapshot row. |

### Residual (owned elsewhere, not a regression of this change)

A guest's live apply still rides the game's own `DamageBlock` (`BlockBreakSync`), so a guest whose
list is full for cells the host does not know about (its own unhooked damage) can still refuse
snapshot rows — that refusal is logged by cell, never silent. Closing it means covering the unhooked
writers, which is the guest-report gap tracked in
`todo/guest-partial-block-damage-re-report.md`.

Two further properties of that apply path are worth naming, because the single-table fix does NOT
change them and no document should claim otherwise:

- **Order.** The host sends in the game's own list order, which is oldest-first. A receiver short of
  room therefore keeps the OLDEST host rows and refuses the newest — the reverse of what the game
  itself does when its own list overflows. Only a receiver already missing cells can be short of
  room, so the common case is unaffected.
- **Empty means silent.** A host whose own list is empty sends no message at all
  (`BlockDamageSnapshotSender`), so a receiver holding local rows is not cleared by a later snapshot.
  That is deliberate — those rows are real damage the host simply never observed — but it does mean
  the apply is a merge on the receiving side rather than an absolute set.

## Verification limits

The table/eviction rules are Runtime-testable and are covered. Crack rendering, break timing and
building support loss reading the resulting state need the in-game dual-client pass: the adapter's
world reads need a running game, and the engine-side branch that renders (or destroys) a crack sprite
is not exercised by the test host.
