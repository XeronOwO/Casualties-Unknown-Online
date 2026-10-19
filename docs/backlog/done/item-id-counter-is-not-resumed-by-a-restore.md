# The item-id counter is not resumed by a restore, so a restored world can re-issue a live id

- Status: Done — accepted by batch `20261021-a` on the deployed `0.1.0+c9769177…`: a new process's
  continue resumed the host allocator at 241 → 242 and the next allocation took counter 242 with no
  duplicate-id refusal; the present member's grant arrived at the restore with no reconnect; the absent
  member's handshake grant carried the restored mark (1, not 0); and a layer change published ids up to
  counter 503 with no duplicate. Record:
  `docs/evidence/acceptance/item-id-counter-is-not-resumed-by-a-restore-20261021-a.md`.
- Priority: High
- Category: Remote inventory / item identity / persistence
- Source: found by reading during the `favourite-marks-are-local-and-saved` cycle (2026-10-11), which
  keys a local favourite mark by the item instance id and therefore had to answer "does that id survive
  a save and a load". It does — but for a reason that leaves this question open, and the cycle that
  depends on it must not silently own the answer.
- Related: `docs/architecture/save-archive-format.md` §3.4 and §6.1 (what a cut carries and where a
  restore lands), `docs/evidence/sync-coverage-matrix.md` row I8,
  `review/save-multiplayer-restore-and-backups.md`, `review/save-mid-run-consistent-cut.md`,
  `review/id-resume-sources-still-unseeded.md` (the two sources this landing did not read; both landed
  2026-10-11, and that change is what moved the seed to `WorldRestoreApplier.TryApply`).

## The claim, reproduced before it was fixed

CUO instance ids are `(counter << 32) | account-low` and the counter is PER PROCESS
(`ItemIdAllocator`). The only thing that ever raised it was the host's grant to a guest
(`GameAdapterSessionBinding.OnItemIdWatermark` → `ItemIdAllocator.SetWatermark`, on the
`ItemIdWatermark` message, whose recorded purpose is the crashed-and-REJOINED guest). A world restore
re-stamps the ARCHIVED ids onto the rebuilt objects — `ItemStateCodec.RestoreItem` /
`WearableRestorer` for carried items, `GeneratedItemReconcile` (through `GeneratedItemAuthority`) for
world items — and nothing resumed the allocator to the restored high-water mark.

Run on the physical machine, solo host, against the pre-fix deployed artifact
`0.1.0+d305ba9ca9e1200b7ff1dee0c6678cc6503cba44`: `start-run` → `item-provide create` (id
`1043998033859`, counter 243) → `console /save` (mid-run cut, world `w-20261011-d555`, whose
`live/items.json` held 244 ids of that account's space, counters 0..243) → `quit` → relaunch →
`continue-run` → `item-provide create` → the new item was stamped **`320980931` = counter 0 again**, an
id the restored world already held:

```
[16:31:46.598] [INF] [ItemApplication] [ItemBind] bound existing boxof12gauge at (-83.6, -167.6) to id 320980931 (no materialization).
[16:31:57.410] [WRN] [ItemKernelAuthority] Item kernel authority spawn rejected: Conflict (item 320980931 already exists)
```

One live id, two live objects: the kernel refused the duplicate spawn, and the scene index resolved the
id to whichever object registered last. The probe, its artifacts and the same claim as failing
regression tests are recorded in the cycle's local artifact directory (`probe-prefix-red.md` in it); the
tests that failed on the unfixed tree pass now. (Those cases were driven through a synthetic
`kernel.Restore` when this landing made them; `review/id-resume-sources-still-unseeded.md` moved the seed
out of the kernel event, so they now play a world, cut it and continue it in a second process — the same
claim through the seam the game uses.)

## What landed

| # | What the rule needed | Where it lives |
|---|---|---|
| 1 | the id format in one place, including that the owner half is the account id's LOW 32 bits and that the synthesized-item namespace owns a high bit | `Runtime/Session/Items/ItemInstanceIds.cs` (`Compose`/`CounterOf`/`SpaceOf`/`OwnerOf`/`AwardedHighBit`/`IsAwarded`) |
| 2 | the resume: per-id-space high-water marks seeded from the restored ids, the local allocator resumed on the spot, present members granted, absent members kept for their handshake | `ItemIdCoordinator.ResumeRestoredIds` |
| 3 | the seam: the archive's ids, read at the click before any generation can allocate (this landing read the checkpoint's rows; `review/id-resume-sources-still-unseeded.md` moved the rule to the one seam that holds BOTH of the archive's id carriers) | `WorldRestoreApplier.TryApply` → `IRestoredItemIdSeed` → `ItemIdCoordinator.ResumeRestoredIds` |
| 4 | one consumer on the adapter side — the watermark the host's grant already arrives on | `IItemControl.ItemIdWatermarkReceived` → `GameAdapterSessionBinding.OnItemIdWatermark` → `ItemIdAllocator.SetWatermark` (unchanged; raise-only) |
| 5 | the marks surviving a session boundary, because they belong to the world the ids live in | `ItemIdCoordinator.ResetSessionState` no longer clears them |
| 6 | every composer using the format owner, so encode and decode cannot drift | `ItemIdAllocator.Next`, and — since `review/id-resume-sources-still-unseeded.md` — `ItemIdCoordinator.NextAwardedItemId` (the synthesized mint this landing's `OtherMedicalRemovalApplier.NextAwardedItemId` became) |
| 7 | the rule under tests that can see it (the fixture's account ids are account-sized) | `ItemIdCoordinatorTests` — the restore half is five `AContinuedWorld_*` cases plus one foreign-space case, all driven through a real cut + continue (the suite's other two cases are wire-level and the format contract, which do not) — and `ItemSimWorld.Create`'s account-id and saves-root parameters |

## Sibling direction (the ticket's step 3)

A guest's resume and the host's own now run through ONE rule: the high-water mark of an id space, held
by `ItemIdCoordinator` and applied through one event and one message. The restore seeds that same table,
so a member in the session is granted the raised value immediately (a present member sees no second
handshake), an absent member keeps it for its next handshake, and the local side takes its resume point
locally because there is no grant to itself on the wire. The table's key is the id SPACE
(`(uint)accountId`), never the full SteamId: an id's owner half cannot address a table keyed by the
17-digit account id, and the first version of this fix made exactly that mistake — the independent
adversarial review caught it, and the tests now use account-sized ids so the mistake cannot hide
(the fixture's old 32-bit ids made the two keys the same number).

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Continue a mid-run cut in a NEW process, then allocate one new host item | The new id's counter is above every restored id of that account's space, the log names the resume (`[IdWatermark] the restored world already handed this side ids up to counter N`), and no kernel `already exists` refusal follows | pass (batch `20261021-a`) |
| 2 | A guest is in the lobby while the host continues that world | The guest receives the raised grant at the restore (no reconnect) and its own log resumes its counter | pass (batch `20261021-a`) |
| 3 | A guest that was away when the world was restored rejoins | Its handshake grant carries the restored high-water mark, not its last report and not 0 | pass (batch `20261021-a`) |
| 4 | Play on past the restore into the next layer | The generation's publish allocates above the restored marks: no duplicate-id refusal, and no two scene objects share one instance id | pass (batch `20261021-a`) |

## Limits recorded with the landing

- **What is seeded is the archive's ITEM rows** (the kernel checkpoint's table, which §3.4 says carries
  everything a mid-run cut holds — world, carried, contained and worn). The archive's second id carrier,
  `characters/<playerKey>.json`'s `CharacterItemMsg.InstanceId`, is not read: for the probe's own archive
  the character file's ids were a subset of the item table's, but that subset relation is not proved for
  a guest id that reached the 1 Hz character report before its kernel registration — CLOSED by
  `review/id-resume-sources-still-unseeded.md` (the seed now reads both carriers).
- **The host's synthesized-item namespace has a counter of its own** (`OtherMedicalRemovalApplier`'s
  awarded splint/tourniquet ids, `AwardedHighBit | counter | space`). Those ids are skipped by the resume
  — reading their counter as the plain one would push the allocator into that namespace — and their own
  counter was per process, so a continued world could re-issue an awarded id. CLOSED by the same sibling
  ticket (the counter moved to `ItemIdCoordinator` and resumes from the archive's awarded ids).
- **A member whose process restarted is bounded by what the host last recorded**, so a watermark report
  swallowed in the window before that crash still grants a stale value. The archive seeds a world
  continue; a mid-session crash does not.
- The `review/` rows are code-complete and judged by the agent-run acceptance in `docs/acceptance/`; the
  probe above is development evidence against the pre-fix artifact, not that acceptance.
