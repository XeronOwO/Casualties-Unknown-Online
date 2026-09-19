# World/layer generation identity is missing from the wire

- Status: Review
- Priority: Medium
- Category: Network / protocol / world generation (attribution of world reports)
- Source: `review/guest-break-drops-recovery.md` — the limitation its 2026-09-18 review round forced: a break report naming a cell the host still holds is REFUSED, because without a generation identity a stale previous-layer report cannot be told apart from a legitimate one
- Related: `review/block-break-first-writer-wins.md`, `review/guest-block-mutation-re-report.md` (W1), `review/guest-partial-block-damage-re-report.md` (W2), `review/enemy-snapshot-binding-recovery.md` (that domain solved its own version of "which generation does this fact belong to?" with a per-entity binding anchor), `review/generation-identity-remaining-families.md` (the two families this cycle audited but did not stamp)

## Problem (evidence)

The W1/W2 recovery converges a swallowed guest report through an absolute host answer plus a guest
re-report. Attribution breaks at a world/layer boundary because the direct world reports carry no
generation identity:

- The guest's break travels as a cell-keyed report plus a drops report
  (`GameAdapter/World/BlockBreakSync.cs`), and the host's cell keys are LAYER-RELATIVE: after a layer
  change the same `(x, y)` addresses a freshly generated block.
- `Runtime/Session/World/BlockBreakArbitration.cs` therefore refuses a break report that names a cell
  the host still holds. The 2026-09-18 round established why accepting it is wrong: a stale
  previous-layer report would take that verdict and `DamageBlock` would break a newly generated block
  with the report's REAL damage (only the fallback's re-send carries zero). The refusal costs a
  legitimate same-generation case — the air-write report was lost, the drops report arrived — whose
  drops are then rolled back and destroyed on the breaker.
- The kernel path already has the vocabulary: `RunEpoch` rides every `EnvelopeHeader` and is
  epoch-filtered on both sides (`Runtime/Session/Items/KernelProtocolService.cs`). The direct
  `NetMsg` world reports (`BlockDamaged`, `BlockPlaced`, `BlockDamageReport`, trap-layout and
  runtime-entity snapshots) do not carry it.

## What landed

**1. The identity is the kernel run baseline, and it has one carrier.** `WorldReportGeneration`
(Runtime, pure) is `(RunEpoch, LayerIndex)` — the run baseline the host commits and the guests
follow, so both sides stamp and compare the SAME identity without a new counter and without trusting
either side's local world state. `WorldGenerationMsg` (`Protocol/Messages/`) is its wire shape, and
`KernelWorldGenerationSource` reads it from `ItemKernelAuthority.QueryRun()` +
`CurrentRunEpoch` (null before a run exists, which travels as an unstamped report). The comparison is
one pure decision — `WorldReportGeneration.Relate` returns `Current`, `Stale` or `Unknown` — and
`Unknown` (no stamp, or no baseline here) is never treated as fresh, so a peer that cannot be
attributed keeps the pre-stamp conservative behaviour.

**2. Every send site of the family stamps.** `BlockPlacedMsg.Generation` (proto member 4),
`BlockDamagedMsg.Generation` (6) and `BlockDamageSnapshotMsg.Generation` (2) are set by the send
paths themselves — the live report, the host's broadcast and correction, the host's late-joiner
snapshot and per-report answer, and all three of `GuestReportRecovery`'s fallback re-reports — read
live at send time. A fallback re-report therefore always carries this side's CURRENT generation, which
is correct by construction: the generation boundary drops every pending entry, so a surviving entry
belongs to the world this side simulates now.

**3. The gate runs at the receiving seam.** The block-report channel
(`Runtime/Session/World/BlockReportChannel.cs`) compares each arrived report's stamp with its own
generation before the report reaches the domain:

- `Stale` is refused with one precise log line naming the message kind, the sender, the reported
  generation and the current one (`RelateReportGeneration`), and the report is not applied.
- The **air write** (`BlockPlaced`) and the **damage rows** (`BlockDamageSnapshot` / the guest's
  absolute `BlockDamageReport`) are refused whole at that seam, with no answer: the reporter's own
  generation boundary drops its pending table, and answering with this side's current values would
  write THIS generation's blocks/damage into the reporter's older world.
- A **break report** (`BlockDamaged`) is delivered to the domain WITH its verdict, because its
  drops are the breaker's local copies and a stale break must roll them back (`ItemReject`) exactly
  as a first-writer loss does. The adapter logs the consequence and rejects them; a stale relay on a
  guest is neither applied nor allowed to acknowledge the guest's own pending report.

**4. The limitation this ticket was born from is closed.** `BlockBreakArbitration.TryAccept` takes the
cell's live state and the generation verdict, and returns the new `Verdict.LostAirWrite` when the
report is provably THIS generation's and names a cell this side still holds — the air-write report
was lost together with the drops carrier, so the standing cell is not evidence of another writer but
the shape a lost air write leaves. The adapter then registers and relays the drops and applies the
break's air transition (`OnRemoteDamageBrokeBlock`), which is the path the 2026-09-18 round had to
remove. First-writer-wins itself is unchanged: a recorded air write whose cell stands again stays
refused, and a DIFFERENT sender on an accepted cell stays refused whatever the cell looks like.

**5. Protocol 28 → 29** (`ProtocolVersion.Current`, `docs/decisions/active.md` #137,
`docs/api/mod-api.md`): this is a wire change on three messages. A peer without the stamp would report
unstamped (compared as UNKNOWN — the old behaviour) and would apply this side's stamped reports
without the check, so the two sides would disagree about which generation a report belongs to exactly
when a layer boundary is crossed.

**6. Structure.** The channel was extracted from `WorldStateMessageService` at the 600-line
aggregate gate into `BlockReportChannel` (the block state / block damage / partial-damage family, its
gate, and the recovery surface it answers), and the new vocabulary is split one type per file
(`WorldGenerationRelation`, `WorldReportGeneration`, `KernelWorldGenerationSource`, `WorldGenerationMsg`; the identity type is named `WorldReportGeneration` so it cannot collide with the game's own `WorldGeneration` type).
`WorldService` (the `IWorldControl` facade) forwards the family to the channel; the message service
keeps the rest of the world message flow.

**7. Independent adversarial review** (one round, fresh context, frozen tree): the mechanism survived
both falsification attempts — the `LostAirWrite` branch is unreachable with an `Unknown` stamp and
unreachable on an air cell, and every production construction site of the three messages stamps — and
every finding was bookkeeping: a matrix row still describing the removed refusal, four test comments
naming the ticket's old `todo/` path, a renamed test cited by the older ticket's table, a duplicated
doc clause, the identity type's name colliding with the game's own `WorldGeneration`, and the
follow-up ticket overlapping already-landed work. All were fixed in this same cycle, before the
commit.

### Family audit — the generation-relative reports that are NOT stamped

The stamp was extended to every direct world report whose key is a block cell and whose receiver
would write into that key with no live-world existence check. The other generation-relative families
were audited one by one; each verdict below names the reason, and the two that are not provably safe
are carried by `review/generation-identity-remaining-families.md` rather than declared safe here:

| Family | Direction | Verdict |
|---|---|---|
| `BlockPlaced` / `BlockDamaged` / `BlockDamageSnapshot` (+ the guest's absolute re-report) | both | **Stamped this cycle** — cell keys, applied without a generation check |
| `TrapLayoutSnapshot` / `TrapLayoutEntryMsg` | host → guest | **Not stamped, not provably safe**: position-keyed entities materialized into the guest's world; a repair landing across the guest's own layer change would place the previous layer's traps. Follow-up ticket |
| `EntitySpawnedMsg` (runtime-entity creation) + its pending-report fallback | guest → host | **Not stamped, not provably safe**: a creation is materialized at the reported position; the in-flight window is bounded by the reporter's boundary reset, but nothing on the wire attributes it. Follow-up ticket |
| `EntityEventMsg` (trap triggers and other entity events) | both | Safe: the receiver resolves the entity in its OWN live scene and refuses/ignores a position where nothing exists, so a regenerated cell cannot be written |
| `BuildingEntityDamagedMsg` / `BuildingEntityOpenedMsg` | both | Safe, same reason: the adapter resolves the building entity through its own world and logs the miss |
| `DynamiteExplosionMsg`, `WorldBloodSpawnMsg`, fluid presentation, speech/chat/location pings | both | Safe: presentation or item/keyed facts, no generated-terrain key; a stale one is a transient artifact, not a world write |
| `WorldBlockState` / `BlockDamageSnapshot` (host → guest) | host → guest | Stamped: the rows are cell keys and the snapshot writes them absolutely |

## Verification

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | The air-write report is lost but the drops report arrives in the SAME generation | Accepted; the drops are registered, not destroyed | `GuestBreakDropRecoveryTests.LostAirWrite_OfThisGeneration_IsAccepted_AndItsDropsSurvive` (host at layer 3, report stamped with the host's own baseline; the drop lands in the host's table and the cell's block is gone) |
| 2 | A previous layer's break report arrives after the layer change | Refused as stale, logged as such, the new layer's block untouched | `GuestBreakDropRecoveryTests.ReportFromAPreviousLayer_IsRefusedAsStale_AndTheNewLayersCellIsUntouched` (layer 3 vs 4; no registration, no relay, no arbitration record, the drop rolled back on the breaker) |
| 3 | The same generation, first-writer-wins | Unchanged: the host's applied air-write record decides | `BlockBreakArbitrationTests` (31 call sites carry the two new inputs; the recorded-air-write, repeat, different-sender and air-cell cases are unchanged) |
| 4 | A stale `BlockDamageReport` row set for a regenerated cell | Not applied to the new layer's block | `GuestBlockDamageReportRecoveryTests.ReportFromAnotherGeneration_IsNeitherMergedNorAnswered` (0 merge calls, no answer, the reporter's entry stays its own boundary's business) |
| 5 | Session end / new run | The identity resets with the run baseline; no cross-run attribution | `WorldReportGenerationTests.AnotherRun_IsStale_EvenAtTheSameLayer` (the layer index restarts, the epoch is what separates runs) + `BlockBreakArbitrationTests.Reset_DropsEveryRecord_SoAStaleReportCannotClaimTheNewLayer` |
| 6 | Third-party view after a layer change | Every peer refuses the other generation's relay and does not re-attribute it | `GuestBreakDropRecoveryTests.RelayStampedWithAnotherGeneration_IsNotApplied_AndDoesNotAnswerTheReportersDrops` (the relation observed on the guest's event is `Stale`, its pending drop report stays unanswered) |
| 7 | The stamp is on the wire, not added by the caller | The send path stamps from the kernel run baseline | `GuestBlockReportRecoveryTests.LiveBlockReport_CarriesTheSendersOwnGenerationStamp` (frame decoded from the host's transport; run epoch + layer match the guest's baseline) |
| 8 | No stamp / no baseline on this side | Never treated as current — the pre-stamp behaviour | `WorldReportGenerationTests.AReportWithoutAStamp_IsUnknown_NeverCurrent`, `WithoutARunBaselineHere_AVerifiedLookingStamp_IsStillUnknown`, `BlockBreakArbitrationTests.Break_WithoutAnAirWriteRecord_OnAStandingBlock_WithoutAGenerationProof_IsRefused` |

Cycle measurements (this tree): focused block/world filter 112/112, main suite 3354 green, normative
gates 56/56, `dotnet build` 0 warnings / 0 errors, `dotnet format` exit 0.

## Known coverage gaps (declared, not silently carried)

- The adapter's own half is not executable in the test host (Unity types): the real
  `WorldGeneration.WorldToBlockPos` conversion, the `SetBlock`/`DamageBlock` application of an
  accepted lost-air-write break, the drop materialization, and the `ItemReject`-driven local
  destruction of a refused drop rest on code review plus the unified dual-client acceptance pass —
  the same boundary `review/guest-break-drops-recovery.md` recorded.
- The comparison depends on the host publishing the run baseline before a guest reports: a guest that
  has not applied the new baseline yet stamps the previous generation and its report is refused as
  stale (conservative, and its own boundary clears the pending entry). The reverse order — the host
  behind a guest — cannot occur, because the layer advance is host-initiated; the stamp does not
  defend against a peer that lies about its generation.
- The stale log line is asserted by review, not by a test: the runtime test host does not capture
  `ILogger` output for this channel, so what the tests pin is the refusal and its consequences.
- Physical deployment and dual-client acceptance remain the user's release-cycle action;
  development-period verification is simulation/static by rule.

## Non-goals

- Changing the first-writer-wins rule itself.
- The item-domain drop bookkeeping (W1's `PendingBreakDropTable` and its 60 s window).
- The two remaining generation-relative families (trap layout, runtime-entity creation) — audited
  above and carried by `review/generation-identity-remaining-families.md`.
