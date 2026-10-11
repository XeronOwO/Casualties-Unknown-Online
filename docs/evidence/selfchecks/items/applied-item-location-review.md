# An applied item keeps its id — adversarial review record (2026-10-12)

Three independent adversarial reviews (fresh contexts, read-only, no stake in the change) ran against
the FROZEN working tree of the cycle that landed the applied-location model, before its commit. The
delivered state is commit `467f20f9`; the trees the passes read were `984ffa18` (pass 1, before any fix),
`151fd56f` (pass 2, before its own findings were fixed) and `056865b3` (pass 3, the state that became the
commit). The change's own record is
`docs/evidence/selfchecks/items/applied-item-location-selfcheck.md`; the ticket is
`docs/backlog/review/item-identity-across-transforms.md`.

Tier: FULL for passes 1 and 2 (shared wire, save format, kernel model), NARROWED for pass 3 (the fix
delta). **No pass found a blocker.** Every finding below was fixed in the same commit that was reviewed,
except where the row says otherwise.

## Pass 1 — `984ffa18`

Covered the premise (A0) against `reversing/` and reproduced the suite numbers.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major | `ItemTerminalFact.From` did not carry the limb, so the diagnostic comparator could not tell two applied items on two limbs of one body apart — and the consumer census did not list it | fixed: the fact carries `LimbIndex`, `ItemDiagnosticsProjection` compares and prints it, `ItemSimWorld`'s two hand-built facts pass `ItemLocation.NoLimb`, pinned by `ItemDiagnosticsProjectionTests.Compare_TellsTwoAppliedItemsOnDifferentLimbsApart` |
| 2 | major | `DecidePickup` / `DecideDrop` accepted an applied item, so a report could move the item while the component that IS the item stayed on the limb | fixed: both refuse an applied source by name, pinned by `ItemLimbApplicationTests.AppliedItem_CannotBePickedUp` / `.AppliedItem_CannotBeDropped` |
| 3 | minor | A limb-less applied row fails the WHOLE chunk set on the wire-checkpoint path, while the archive skips one row — and only the archive's model was written down | not a defect (an unknown location kind already behaves that way on that path); named in the self-check's §5 |
| 4 | minor | The MANIFEST row for the design record had been amended but still said the decision "is NOT ruled" | fixed: reworded to "written BEFORE the ruling and kept as the evidence it rests on" |

## Pass 2 — `151fd56f`

Completed the census, the contract-shape and the reference-integrity checks pass 1 had not finished.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major | `DecideSpawn` accepted `ItemLocation.Applied` from the wire, so a member could create an item that exists only as another body's limb component — and, because only that limb's owner may report such a row's end, nothing could ever correct it | fixed: `DecideSpawn` refuses an applied location by name (`LimbItemLocationRules.RefuseAppliedCreation`), pinned by `ItemLimbApplicationTests.SpawnDirectlyOntoALimb_IsRejected` and — for the reachable wire path — `KernelWireMapperTests.AWireSpawnReportThatNamesALimb_ReachesTheKernelsRefusal` |
| 2 | major | Three already-checked delivery-checklist boxes carried figures that no longer reproduced (a nine-row census, a page length, a case decomposition) — the first round of fixes had not been back-filled | fixed: every figure in those boxes re-measured after the fixes; pass 3 then found the same line still carried a duplicated clause and three further stale figures, and those were fixed too |
| 3 | minor | The self-check's §6 counts and §1's line count were stale | fixed |
| 4 | minor | The ticket still said the location enum had "four families" | fixed: five |
| 5 | minor | The census missed `DecideCook` (whose behaviour with an applied source was unstated) and `DecideTransfer` | fixed: both are rows now, and `DecideCook` refuses an applied source by name, pinned by `ItemLimbApplicationTests.AppliedItem_CannotBeCooked` |
| — | nit | §3's title claimed to enumerate every reader while seven pass-through readers were listed as "not rows" | fixed: the sentence now separates decisions from pass-throughs and says which is which |

## Pass 3 — `056865b3` (the delivered state)

Checked the delta of passes 1–2 plus every figure in the documents that state them.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major | The delivery-checklist line for the build/format/gate box had been edited by prefix, so the previous cycle's evidence clause was still appended to it — two `— evidence:` clauses on one line, with two different behaviour totals and a "documentation-only cycle" claim this cycle contradicts | fixed: the stale clause is deleted and the line states the filtered run (614 of 615 gates) and the full run (615/615) separately |
| 2 | major | Three checked boxes still carried unreproducible figures (the census row count, the self-check page's length, the added-case decomposition) | fixed: re-measured, and the new wire case the nit below asked for is counted |
| 3 | minor | The self-check's §6 labelled the FILTERED gate count (614) as "Full"; the unfiltered run is 615 because it includes the delivery-checklist gate | fixed: both numbers are stated with their runs |
| 4 | minor | The self-check's §1 still described the pre-consolidation file ownership (the refusals had moved into `LimbItemLocationRules`) | fixed |
| 5 | minor | The ticket said "plus 29 cases" | fixed: 35 |
| 6 | minor | §3's intro named `KernelBatchItemProjection`'s rebuilds as "not rows" while the table has a row for its batch path | fixed: the sentence distinguishes the batch path (a row) from the three rebuild filters (not rows) |
| 7 | nit | "five refusals" in one line against "four" elsewhere | fixed: four |
| 8 | nit | The spawn refusal's reachability from the WIRE had reading evidence only, no case | fixed: `KernelWireMapperTests.AWireSpawnReportThatNamesALimb_ReachesTheKernelsRefusal` maps a real `ItemSpawn` wire command carrying an applied location and pins the kernel's refusal |
| 9 | nit | The applied-refusal-versus-stale-revision priority was unpinned | fixed: the pickup case asserts both, so the family's rule is what a caller diagnoses |

## What the reviews confirmed, and what none of them could check

Confirmed on the frozen tree by the passes themselves: `GameAdapter` is untouched (0 files), so no
running game can create an applied row; the two new `LimbItemLocationRules` helpers return null for every
non-applied location, so the common path is unchanged; `ItemDomainModule`'s diff against HEAD is
additions only (no existing rejection was rewritten by the consolidation); the native citations
(`Item.cs:400-408`, `:1479-1491`, `:1505-1517`, `:1626`/`:1629-1639`, `PlayerCamera.cs:772-776`,
`:781-786`, `Limb.cs:101-103`) resolve; `docs/contracts/abstractions-api-baseline.txt` is untouched and
`ApiSurfaceGateTests` is green; the suite is green at gates 615/615 and behaviour 4990/4990.

Could not be checked by any pass: `dotnet format` (forbidden to a read-only reviewer); anything that
needs the game running — and nothing in this cycle does, by design; the 4955 case baseline (it lives in
HEAD, which no pass built); and the exact shape of the intermediate trees of the other passes, which are
not in git. The spawn-hole finding itself was reading-based at first and is now pinned by a wire-level
case; what remains unproven is that a REAL client can be induced to send such a report, which is the
pre-release trust model's own open question rather than this change's.
