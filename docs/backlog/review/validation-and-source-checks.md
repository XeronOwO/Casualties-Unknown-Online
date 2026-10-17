# Source, permission and data checks on what a member reports

- Status: Review — all three stages are CODE AND TESTS, and the remaining work is the agent's acceptance
  batch. The user promoted this on 2026-10-14 (it was `future/strict-validation-anti-cheat.md`, deferred);
  the old ticket stays as the inventory behind it. S1a and S1b landed 2026-10-14, S2a
  landed 2026-10-16 **and its acceptance rows are read for the block family**
  (`docs/evidence/acceptance/validation-and-source-checks-s2a-20261016.md`: rows 1, 2 and 6's new half, on a
  deployed artifact), and S2b landed 2026-10-16 as CODE AND TESTS with BOTH halves of its acceptance row 1
  read on the machine (`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`: a forged
  building drop refused by name where the host has no dying building, nothing registered or relayed and the
  breaker answered; and then a census of the population, the host's capture of a real `requireGround`
  building's own table, the breaker's real roll accepted against it, and one forged report whose two drops
  were split per drop — the table-accountable one relayed, the other refused by name). S3a landed 2026-10-17 —
  the clause "an item use must name an item the host records": a member's state report can no longer create
  an item this host never judged once that member's carried-set statement has been judged — and S3b landed the
  same day, the second clause "in a state that permits that use": the native use travels under its own wire
  kind and the host bounds it with the item's own data (`ItemInfo.usable`), answered and settled. That closes
  the three stages as code and tests. Row 7 was READ on the machine on 2026-10-17 by batch `20261017-o`
  (`docs/evidence/acceptance/validation-and-source-checks-s3b-20261017-o.md`): the host refuses a forged
  native use of a `bandage` BY NAME, answers it, the member's already-applied `condition` is settled back to
  the recorded one, the host's own row does not move, a control forged use of a `painkillers` (whose data
  marks it usable) is ADMITTED, an ordinary native use still passes, and the whole session carries exactly
  the two forged refusals. The ticket stays in `review/` for row 5 alone — its refusal half was read in batch
  `20261017-c`, its accepted half was not staged, and the batch found WHY it is the hard half: the window it
  lives in closes at the generation-finished edge, before any in-process probe can run (the rejoin edge is
  the avenue the next batch takes; see the acceptance section). The same batch also read part of S2b's owed capture-loop row (the multi-building population
  half; the already-marked half stays `unproven`, see that section). The surfaces this ticket does NOT
  close are the census rows of
  `docs/architecture/member-report-validation.md` marked `unbounded:` — they carry their own evidence in
  `todo/member-report-bounds.md`, opened by the same cycle at the user's direction that report validation
  become a general development standard.
- Priority: Medium-High
- Category: Security / report validation / host rules
- Source: the user's 2026-10-14 request: no advanced anti-cheat, but BASIC checks from the game's own
  mechanics — a destruction report carries its source and must match the drop table (kinds, counts, the
  probability window for a non-integer roll), a command's report needs the sender to hold the cheat
  permission, an item use needs the item to exist in a state that allows it.
- Related: `docs/architecture/member-report-validation.md` (the rule and the per-wire-member census this
  ticket's stages produced), `todo/member-report-bounds.md` (the surfaces that census leaves unbounded),
  `future/strict-validation-anti-cheat.md` (the inventory this ticket works from — its
  authority-declaration half is closed by S1a below; the console command's own path, the drop table and the
  state preconditions are what remains of it),
  `todo/health-simulation-and-host-authority.md` (the continuous-value half of the same conversation),
  `done/item-data-write-commands.md` (its write verb is host-only today for exactly this reason: a command
  write is byte-identical to a native use on the wire), `docs/decisions/active.md` 210 (the admission seam),
  `docs/evidence/sync-coverage-matrix.md` (a new wire kind owes it a row), `docs/architecture/current.md` §7.1

## The three stages

1. **S1 — source and permission.** Split on 2026-10-14 into the half a production sender reaches today
   (landed) and the half that needs a sender of its own (open).

   **S1a — landed 2026-10-14.** The mapper's blanket authority became a per-kind declaration
   (`WireCommandAuthorityPolicy`, one entry per mapped kind, filled from the senders this tree actually has —
   each entry names its sender). The seam's authority half therefore tells a member's `RunStart` from a
   member's item use at last: the thirteen kinds no member sender emits are `HostOnly` and are refused by
   name, while the eleven a member's own client emits keep an authority a member may author. The same seam
   gained the item-report eligibility family the inventory names, as ONE rule rather than one per kind: a
   member may report on a world item or on anything whose container chain is world-rooted, and on its own
   carried, installed or contained item — never on an item the record puts in another member's hands, on
   another member's limb or in another member's container. That rule now covers a destroy report, a state
   report AND a container sync, the last of which had reached the kernel without passing the seam at all
   (the domain rewrites the parent's row, moves the listed children in and destroys the descendants the
   report leaves out), plus the container case's own second half: a listed child may not be an item the
   record puts in another member's hands, which is what stops a report pulling a peer's carried item into
   the reporter's own container. Evidence: `WireCommandAuthorityPolicyTests` (the per-kind census, which
   fails on a wire kind nobody classified), two new cases in `KernelCommandGatewayTests`, the new
   `KernelItemReportEligibilityTests` family (eighteen cases, split out because one class may carry at most
   forty), three end-to-end rows in `CommandAdmissionIntegrationTests`
   (`AHostOnlyReportFromAMember_NeverReachesItsDomain`, `AStateReportForAnotherMembersItem_ChangesNoRowAndIsNotAnswered`,
   `AContainerSyncForAnotherMembersContainer_MovesNothingAndIsNotAnswered`) and the new
   `WireCommandSenderAuthorityGateTests`, which keeps the declaration table and the senders under `src/` one
   list in both directions. Every end-to-end row was observed RED against the previous behaviour before the
   fix: the host-only report committed an entity-health fact, the forged state report moved another member's
   row, and the forged container sync rewrote the peer's container and moved the peer's item.

   **S1b — landed 2026-10-14, and it is the half the write verb was waiting for.** The console write has a wire
   path of its own: `WireCommandKind.ItemDataWrite` (its payload the same save-shaped state a native use
   carries, and the mapper deliberately produces the same `UpdateItemStateCommand`, because the row it writes is
   the row the game would have written), so the host can judge the VERB where it could only judge the state
   before. Whether a member may run that verb is the host's own rule — `HostRulesOptions.AllowMemberItemDataWrite`,
   following the existing flags and defaulting OFF (the family's "not a cheating surface by default") — reaching
   the Application layer's admission seam through the port `IMemberCommandRules`, because the seam may not
   reference Runtime configuration. The seam therefore takes the submission's wire kind beside the mapped
   command (the command alone cannot say which verb it was), and it ANSWERS this kind's refusals, unlike the
   item-eligibility verdicts beside it. The way BACK landed with it, which is the half S3 names: the writer's
   console has already applied the value to its own object and reported it, so a host refusal settles that
   object back to the state this session records (`ItemDataWriteSettlement`) and the operator reads the refusal
   where they typed the command. `WireCommand.RefusedKind` is what makes the answer exact — one member can have
   several reports in flight for the same item, and a refused console write settles differently from a refused
   native use, so a verdict keyed on the item id alone could settle the wrong one.

   Evidence: the per-kind census gained the kind (`WireCommandAuthorityPolicyTests`, 25 rows), the sender gate's
   member-authored floor moved 11 → 12, the seam's console-verb pair and the answered-eligibility case are in
   `KernelCommandGatewayTests` / `KernelItemReportEligibilityTests`, two rows run through the production
   composition root in `CommandAdmissionIntegrationTests`, and the new `CommandConsoleMemberItemDataTests`
   family (four cases: admitted, refused and settled, refused-without-becoming-a-pickup-rollback, and an id the
   host never judged). Each of the three mechanisms was observed RED with that mechanism alone turned off: the
   member's write committed with the rule check gone, the member's local value stayed at the written one with
   the settle skipped, and an ineligible console write fell back to the silent verdict the native shape gets.
   The independent review then found the half this stage had left in the re-report window, and it is fixed in
   the same commit: the console write is now one of the kinds that window re-reports (a swallowed write used to
   vanish with no trace, while the row claimed it was covered), and a refusal closes the newest report OF THE
   KIND it names — the wire field this stage added made that expressible, and it removes a divergence the
   window itself could cause, where a refused console write spent a pending pickup's budget. Both halves have
   their own RED in `GuestCommandReconciliationTests`.

   Self-check for S1a (mechanism × change × evidence):

   | Mechanism | Change | Evidence |
   |---|---|---|
   | The wire→kernel authority declaration | new per-kind table (`WireCommandAuthorityPolicy`) replacing the blanket `OwnerPredictedHostValidated` in `KernelWireMapper.FromWireCommand` | `WireCommandAuthorityPolicyTests` — 24 declared rows plus a census that fails on a kind nobody classified; `HostOnlyWireKindFromAMember_IsRefusedAndAnswered` was RED with the blanket value; `WireCommandSenderAuthorityGateTests` keeps the table and the `src/` senders one list in both directions (proved able to fail by flipping one kind each way) |
   | The seam's authority half (`KernelCommandGateway`) | reads a declaration that now differs per kind | `AHostOnlyReportFromAMember_NeverReachesItsDomain` — RED before the fix (the member's entity-health fact committed), green after, and it reads the seam's own audit line so a domain verdict with the same answer cannot stand in |
   | The seam's item-report eligibility (`MayReportOn`) | ONE rule for the destroy report, the state report and a container sync's parent | `AStateReportForAnotherMembersItem_ChangesNoRowAndIsNotAnswered` — RED before (another member's row moved), green after; the destroy and nested cases in `KernelCommandGatewayTests`; the world-rooted escape is what keeps a ground container's own contents reportable |
   | The container sync's reach (`KernelProtocolCommandHandler.HandleItemContainerSync`) | the kind now passes the seam, which it never did | `AContainerSyncForAnotherMembersContainer_MovesNothingAndIsNotAnswered` — RED before (the peer's container row was rewritten and the peer's item moved into the reporter's container), green after |
   | The container sync's children (`MayEnterOwnContainer`) | a listed child may not be an item another member carries or wears | the four container cases in `KernelCommandGatewayTests`, including the admitted `Contained` row a container that changed hands leaves behind |
   | The console write's own kind (S1b: `WireCommandKind.ItemDataWrite`, sent by `ItemActionSync.SendItemDataWrite`) | a member's write is distinguishable from a native use while mapping to the same kernel fact | `WireCommandAuthorityPolicyTests` (25 declared rows), `WireCommandSenderAuthorityGateTests` (member-authored floor 11 → 12, the new send site is what satisfies it), `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_IsReportedAndTheHostRecordsIt` — the HOST's own kernel row moves |
   | The host's rule for the verb (S1b: `IMemberCommandRules` ← `HostRulesOptions.AllowMemberItemDataWrite`) | the seam judges the VERB, and the wire kind is what tells it which | `KernelCommandGatewayTests.ConsoleVerbWithTheHostRuleClosed_IsRefusedAndAnswered` / `.ConsoleVerbWithTheHostRuleOpen_IsAdmitted` in isolation, the end-to-end pair in `CommandAdmissionIntegrationTests`; RED with the rule check disabled alone (the member's write committed) |
   | The way back from a refusal (S1b: `ItemDataWriteSettlement` + `WireCommand.RefusedKind`) | a refusal settles the value the writer already applied, and the answer names the report it judged | `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenNothingWasAnnounced_IsSettledByTheHostsOwnRefusal` — RED with the settle skipped (local 0.25, expected 0.5); `.WhenRefused_NeverBecameAPickupRollback`; `.ForAnIdTheHostNeverJudged_IsRefusedAndSettled` |
   | What S1b did NOT change | a console write's DOMAIN verdict is the native use's own (`UpdateItemStateCommand`), the host's own console never passes the seam, and the read verb stays open to everyone | `KernelWireMapper`'s two arms; `ItemDataCommands`' host branch; `CommandConsoleItemDataTests` green |
   | What did NOT change | the domain's own rules and the handler's revision re-stamp | `ItemDomainModule.DecideUpdate` checks existence, a terminal row and the revision only, and `ResolveCommandRevision` re-stamps the revision from the host's own record; `DecideSyncContainer` was left as it is, because the seam is where eligibility belongs |
   | Not covered | the drop table (S2), the state preconditions (S3), and a `CommandRejected` frame arriving as a command KIND (pre-existing: the mapper's default arm threw the same way; `KernelProtocolService` answers it only on the guest's own payload-type branch) | the stages above; the write verb's own rows and limits are `done/item-data-write-commands.md` |
2. **S2 — the drop table.** A block or entity destruction report carries its drops; today the host registers
   them first-writer-wins and never checks them against the block's own table. The host bounds them against
   the destroyed thing's own table and refuses what is outside it — the refusal path already exists
   (`ItemRejectMsg.Reason.BlockAlreadyBroken` makes the reporter destroy the drops it rolled). Split on
   2026-10-16 into the half that can be bounded today (landed) and the half that needs a carrier of its own
   (open).

   **The evidence first, because it decides the shape — a BOUND, not a check.** The roll is NOT
   reproducible on the host: the game rolls its block drops with `UnityEngine.Random` inside
   `WorldGeneration.DamageBlock` (`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:751-837`), a
   process-global UNSEEDED stream that everything else in that process advances (sky colour, footsteps,
   world generation, earthquakes). Where the breaker's stream stood is not knowable here, so which item, how
   many and which condition a legitimate roll produced cannot be re-derived — a check that re-rolled would
   refuse ordinary play. The TABLE is readable, and it is code rather than data: one switch arm per block id
   (`3` → scrapmetal at `Random.Range(0.05f, 0.2f)`; `6`/`10` → scrapmetal at `Random.Range(0.5f, 1f)`;
   `7` → glassshards; `8`/`9` → plasticchunk; `11`/`24` → ONE `Random.Range(0, 3)` over woodscraps / stick /
   woodpanel; `34` → rawcopper; `35` → ilmenitechunk; `27` → a water write into the cell and no item; every
   other id → the no-loot path). A CUO custom tile is the second table and it is DATA:
   `IModTileDefinition.Drops`, rolled by `GameAdapterTileContentProvider.TrySpawnDrops`.

   **S2a — landed 2026-10-16.** The host bounds a member's break report against the block that break
   destroyed and relays only the drops some row of that table can account for
   (`DropBoundPolicy` judges, `GameBlockDropTable` carries the two tables, `BlockDropBoundJudge` is
   the GameAdapter's host-side user). Four decisions worth naming. (a) The destroyed block is the HOST's own
   read, never the reporter's claim: the block is air by the time the drops arrive, so the id is captured as
   the block that stood before the air write was applied (`WorldEventSync.OnRemoteBlockPlaced` reads it,
   `BlockBreakArbitration` carries it on both records, so a Fresh, a Repeat and a LostAirWrite report are all
   bounded against it). (b) The bound is per-drop, not per-report, and the rows are matched to the report as a
   SET: a drop is refused only when NO assignment of the whole report to the table's rows can account for it
   (the matching walks augmenting paths, so two authored rows over the same item id with different windows
   cannot cost a legitimate drop just because the report named them in the order that puts the wide row
   first), and the drops that do fit keep their normal fate — the bound exists to stop an item no local roll
   can create, not to punish the breaker for a roll this table misreads. (c) One row is one ROLL, which is why
   the wood arm is one row naming three ids: spelled as three rows the table would accept all three items
   from a single break. (d) The rejects go out BEFORE the relay, because the breaker's pending break set has
   to shrink first or the relay's echo would not cover it and the break would be re-reported once more.
   The bound is deliberately one-sided: a MISSING drop (a row that rolled nothing, a creation the report
   never folded in) is the breaker's own loss and is never refused. The refusal reuses
   `BlockAlreadyBroken` rather than adding a reason: the breaker's action is identical (forget the drop,
   destroy the local copy — `ItemApplication.OnItemRejected`), and the host's own line names the block, the
   table and each refused drop, so the two shapes are told apart where the judgement is made instead of by a
   second vocabulary the receiving side would treat the same way.

   Evidence: the new `DropBoundPolicyTests` (fourteen cases when this stage landed, sixteen once S2b added the building-death shape: a wrong kind, a condition
   outside the declared window, both window edges, the float slack, one row = one roll, the wood arm, a row
   with no window, an empty table, a missing drop, a drop with no item state, and the partition of the
   report) and the new `GameBlockDropTableTests` (six cases over the REAL adapter assembly: each arm of the
   switch, the wood arm as one row, the no-loot ids, the windows, and a custom tile's authored rows with the
   entries its spawner can never roll); four new cases in `BlockBreakArbitrationTests` pin the destroyed block
   on the Fresh, Repeat, LostAirWrite and refused verdicts. Both halves were observed RED by mutation, each
   against the rule it pins: with the item-id term of the bound neutered exactly the two cases that pin it
   failed, and with the condition window's slack opened exactly the four window cases failed (2 and 4 of 18,
   the other cases green both times).

   **S2b — landed 2026-10-16, and it is the stage that needed a design decision rather than the same
   treatment.** The building-death drops riding the same `BlockDamagedMsg` are now bounded too. Their table
   is not code but per-prefab inspector DATA on a live `BuildingEntity` (`itemsDropOnDestroy` with its
   `chance`/`conditionMin`/`conditionMax`, `guaranteedDropAmount` rolls over `ItemLootPool.pool[category]`
   for a random entry of `itemCategoriesToAdd`, `alwaysDrop`, and a `dropChanceMultiplier` —
   `reversing/Assembly-CSharp/Assembly-CSharp/BuildingEntity.cs:74-120`), which is what made both of the
   stage's questions real: the entity is destroyed by its own next `Update`, so no table exists at judgement
   time, and the drops carry no identity to key to that entity. The design answers both with what the host
   already holds. (a) The CAPTURE POINT is the support-loss MARK — the one moment the components still
   exist: `WorldBuildingEntitySync.MarkSupportLossRemote` reads each dying entity's own table and returns
   the rows, and the air-write path records them beside the destroyed block inside `BlockBreakArbitration`,
   so they live exactly as long as the break claim they belong to and a repeat is judged identically.
   (b) The KEY is the CELL the report already carries: `blockPlacedOn` is the association the host itself
   uses to decide which buildings lose support, so no new wire field and no invented identity was needed —
   and the rows are the UNION over the entities that died there rather than one set per entity, because the
   message cannot say which of them dropped what. (c) The table is `GameBuildingDropTable`: one row per
   chance-gated `itemsDropOnDestroy` entry whose gate the multiplier can still pass, one per `alwaysDrop`
   entry (the game reads no chance there), and one row per guaranteed roll naming the union of every named
   category's pool at the exactly-1 condition the game writes. The judgement is the S2a rule unchanged —
   the same set matching in `DropBoundPolicy`, a per-drop refusal by name with the same `BlockAlreadyBroken`
   answer — and an EMPTY row set refuses every building drop the report carries, which is the safe
   direction (this host knows of no building that died there) and carries its own log line, so a population
   drift between the two sides is diagnosable instead of a silent loss.

   Evidence: the new `GameBuildingDropTableTests` (six cases over the REAL adapter assembly: the
   chance-gated bucket and its multiplier gate, the always-drop bucket, the guaranteed rolls as one row per
   roll over the category union, what the pool cannot name, a negative amount, and the rows feeding the
   bound), two new `DropBoundPolicyTests` cases for the building-death message shape, four new
   `BlockDropBoundJudgeTests` cases driving `BoundBuildingDrops` over the real `IItemControl` (a table-legal
   drop kept, a drop no row can produce refused AND answered, an empty row set answering every drop, one row
   accounting for one drop), and three new `BlockBreakArbitrationTests` cases pinning that the rows ride the
   air-write and accepted records, answer identically through a repeat, and come back empty on the standing
   shape and on a refusal. Two mutation controls were run over the three drop-bound classes together (29
   cases under the filter `FullyQualifiedName~BlockDropBoundJudge|FullyQualifiedName~DropBoundPolicy|FullyQualifiedName~GameBuildingDrop`),
   each with that mechanism alone turned off: with the building-death matcher accepting everything, SIX cases
   go red — the three `BlockDropBoundJudgeTests` cases that pin a refusal, the two `DropBoundPolicyTests`
   building-shape cases, and `GameBuildingDropTableTests.TheRows_AreWhatTheBoundJudgesADeathReportOver` (the
   fourth judge case, `..._ALegitimateSet_ComesBackWholeWithNoAnswer`, stays green, which is what makes the
   count six and not seven); with the guaranteed-roll loop disabled, exactly ONE goes red
   (`TheGuaranteedRolls_AreOneRowEach_OverEveryNamedCategorysPool`) and the other 28 stay green.
   **What the machine has read**
   (`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`). Batch `20261016-b` run 9 on
   `0.1.0+9124a859…`: the host refuses a forged building-death drop BY NAME, registers and relays NOTHING of
   it (`0 block drop(s) + 0 building drop(s) registered + relayed`), the member is answered once per drop and
   destroys its own copy, and the run's other refusals are only the ones it provoked. Batch `20261016-c` run 2
   on `0.1.0+32e4b857…`: the census found the population that run 9's grid had missed — 1365 `requireGround`
   buildings in the loaded world, 1262 of them carrying authored drop data, both clients agreeing cell for
   cell — the host captured 3 rows from the one building that lost support at the broken cell, accepted the
   breaker's OWN roll against them, and then judged one forged report's two drops per drop: the drop the rows
   account for relayed, the other refused by name against rows that exist. That closes the captured-rows half
   of row 1. Run 1 of the same batch is what corrected the claim this paragraph used to make, that this map
   has no `requireGround` building at all: the population is there, a 5×4 grid simply did not contain one, and
   a census taken 7 s after world entry reads a world still materializing (the recipe now says so).
   **Limits S2b carried — BOTH CLOSED 2026-10-17** (the cell key the bound rests on now travels to the
   reporter's fold, and the host's capture no longer treats the death marker as a table answer). Two, and both
   were the same missing fact — the drop did not name the support cell it came from — seen from the two sides
   of the report.

   - **The reporter's side paired a drop with a break by TIME, not by identity.** The pending-break machine
     holds ONE break (`BlockBreakPendingState.EnterBreak` overwrites it) and `TryAddBuildingDrop` folded
     EVERY building death that appeared while one was pending into that break, with no check that the dying
     building's own support cell was the pending cell. A building that died inside the break's next-frame
     window therefore rode that break's report, and the host — whose rows are the tables of the buildings
     that lost support at ITS cell — answered it per drop: a foreign drop its table could not account for was
     refused by name while the breaker destroyed an item it really rolled, and one that happened to fit that
     cell's table was accepted as another building's drop. (The whole-set refusal is the EMPTY-row shape, not
     this one: a report for a cell this side captured no rows at.) Before that stage those drops were
     registered and relayed, so this was the one place where the new bound cost a legitimate item; rare (two
     breaks in one frame, or a support loss landing in another break's window), and it never diverged — the
     host relayed none of them, so both sides agreed on "no drops". The message could not supply the
     association the reporter would need to check instead: a drop is position-keyed by a transform position
     and the entity is destroyed before its `Item.Start` runs — but the DEATH SCOPE still holds it.
     **CLOSED**: the cell rides the scope the death branch already opens
     (`CallContext.EnterBuildingDeathDrop` publishes `BuildingEntity.blockPlacedOn` beside the origin, read
     while the entity exists), `Item.Awake` stamps it on the drop marker
     (`BuildingDeathDropOrigin.SupportCell`, nullable because a building that does not depend on its ground
     has no break that could own its drops), and `TryAddBuildingDrop` folds a drop only into the break for
     that cell. A drop from a foreign building now reports standalone — the path an ordinary attack death
     already took — and the mismatch has its own line, so the shape is diagnosable instead of silent.
     **The sibling is NOT closed**: `TryAddDrop` (the BLOCK drops) is still keyed by time alone, so two
     breaks inside one frame can cross-fold block drops the same way; every native `DamageBlock` call site
     is single-cell, which is why it has never been read on the machine, and it is named here rather than
     left to look closed with the bullet above.
   - **An entity already marked for a remote death contributed no rows.** `MarkSupportLossRemote` skipped an
     entity that already carries `RemoteEntityDeath` — the marker is what stops the other paths from rolling a
     second set — and the skip sat BEFORE the capture, so an entity marked earlier in the same frame by a
     different path (a relayed attack whose damage took its health to zero, an opened lock, a restored
     health row) added no rows: the cell's row set then held only the unmarked subset's tables, and an empty
     one whenever every building on the cell was already marked — every building drop that report carried was
     refused, although the reporter's own copy was never marked and rolled them legitimately. **CLOSED**: the
     table is read for EVERY `requireGround` building standing on the cell and the marker only decides whether
     THIS call marks the death — the marker answers who applies the death, the rows answer what a building
     there could have dropped, and they are different questions.

   Evidence: `BlockBreakPendingStateTests.TryAddBuildingDrop_FromAnotherCell_IsNotFolded` and
   `.TryAddBuildingDrop_FoldsOnlyTheCellsOwnDrops` — both observed RED with the cell comparison neutered
   (2 of 15 in that class failed, the other 13 stayed green), green with it in place; and
   `CallContextCompositionTests.TheBuildingDeathScope_PublishesTheSupportCell` pins the published cell,
   its absence outside the scope, that a NESTED classification scope does not answer for it (the same
   predicate `Current` answers), and that a building with no ground dependency publishes no cell at all
   rather than the real cell (0,0). The marker's own shape is pinned by
   `BuildingDeathDropProvenanceTests.Marker_CarriesTheSpawnPositionAndTheSupportCell`.
   **The three glue sites between those two halves are pinned by `BuildingDropPairingGateTests`** (a
   Roslyn source gate: the patch's argument must be the conditional `requireGround ? blockPlacedOn : null`,
   `Item.Awake` must write the value the query PUBLISHED — the assignment's source has to be the very local
   the `out` declared — and the fold must pass one object's `x` and `y` to `TryAddBuildingDrop`). Without it
   a one-line change at any of them leaves the whole suite green while the fold silently returns to pairing
   by time: observed — replacing the stamp with `origin.SupportCell = null` turned exactly
   `TheAwakePatch_StampsThePublishedCellOnTheDropMarker` red (1 of 25 in that class) and left the other 24
   and the whole behaviour suite green.
   **What is verified and what is not**: the published-cell semantics, the fold rule and the three glue
   sites are unit/ gate-covered, and the fold rule was mutation-red; the host's reordered capture is a
   three-line change inside a loop over live Unity components, so no unit test can reach it. Batch
   `20261017-o` read HALF of it on the machine and names the half it could not
   (`docs/evidence/acceptance/validation-and-source-checks-s3b-20261017-o.md`): a scan of the live population
   (`tools/acceptance/recipes/building-support-cells.cs`) found 40 cells carrying more than one
   `requireGround` building — one with three, and both clients agreeing cell for cell — and breaking that
   three-building cell's support from the member made the host capture `15 drop row(s) from 3 building(s) …
   3 were still unmarked and were marked here` and then accept the breaker's own 10 building drops against
   them. What stays `unproven` is the reordered half itself — an ALREADY-MARKED entity's table contributing
   rows — because in this staging the air-write relay and the break report land in different host frames
   (~100 ms apart), so by the second capture the entities are destroyed, and a marked building with health
   left is a one-frame state in production (the mark is applied together with the health that kills it on
   its next `Update`). Reading that half needs the lost-air-write verdict, where both captures happen inside
   one call; it is named here rather than implied. The cell's own precondition is a native one: `blockPlacedOn` is written by the
   game's two worldgen placement sites and by nothing else, so a `requireGround` building a MOD materializes
   outside worldgen would publish the struct default `(0,0)` — the same value the real cell (0,0) has, and
   not distinguishable from it. No vanilla path was found that materializes a `requireGround` building
   outside worldgen, so this is a mod-only edge, recorded here instead of guarded against by a check that
   could not tell the two apart.

   **Reused refusal by name, not by vocabulary.** A refused building drop is answered with
   `ItemRejectMsg.Reason.BlockAlreadyBroken`, the same reason the block half uses, because the breaker's
   action is identical (forget the drop, destroy the local copy) and the host's own line names the case; the
   cost is that the member's console says "block" for a building's drop, which is recorded here rather than
   papered over with a second reason whose round trip would collapse to the same value.
3. **S3 — state preconditions.** An item use must name an item the host records, in a state that permits that
   use; today the use report's digest is compared with the record and the client is CORRECTED when they
   disagree (`ItemArbitration`) — "wrong but equal-shaped" heals, "not allowed at all" passes. Turning the
   correction into a refusal needs the missing half named: what the client does locally when the host says
   "that did not happen".

   **S3a — landed 2026-10-17, and it is the clause "an item use must name an item the host records".** The
   console write's way back landed with S1b, which is what unblocked this half: a refused report now has a
   named settlement, so the clause could become a refusal instead of a correction. What it closes is the
   item-CREATION channel a member's state report opened. The host's carried-item heal
   (`KernelProtocolCommandHandler.HandleMissingCarriedUpdate`, the documented exception to
   creation-before-operation: an `ItemUpdateState` report for an id this host never judged is spawned as the
   reporter's own carried item, from the report) had no bound at all, and it is reachable with ONE frame: a
   member could conjure any item definition with any state, because the report's identity IS the creation it
   materializes. The bound is the fact that makes the heal an exception rather than a hole — the member's own
   carried-set statement (`NetMsg.CarriedInventory`, absolute and re-sent on its cadence: 12 reports at 5 s
   then one a minute, decision 187, and now also stating an EMPTY set on the window's first step so the fact
   is never left unset by silence). Until this host has judged one, the heal stays exactly as it was (that is
   the swallowed-registration window it exists for). Once it has, an unjudged id under a state report is
   refused BY NAME (`RejectionReason.UnclaimedItem`, its own reason, so the answer says which refusal this
   is) and ANSWERED, and the member settles by re-stating its carried set — which is what its own side does on
   that answer (`ItemService.OnCommandRejected` makes the next report due AT ONCE without restarting the dense
   window, and never destroys the local copy: this client's copy is the only one there is, so the pickup
   rollback would delete the item the refusal asks to have reported). A creation the host already REFUSED
   answers first, with its own tombstone reason, on both paths — a report can no longer resurrect a refused
   creation by making the heal materialize it. The refusal names the sender, the item and the fact in one Warn
   line.
   **The statement is now the creation claim for what it carries, contents included.** The capture is
   recursive (`ItemStateCodec.CaptureItem`), and the host used to drop the contents: it created the container
   and not the items in it, so a use of a nested item would have been refused with no statement that could
   ever fix it. `ItemArbitration.AdoptCarriedContents` now materializes an unknown child inside that parent
   (registration-only exactly like the root: an id the host already holds is left alone), and it adopts the
   contents of a row the kernel already records as THAT member's own — without that ownership test a
   registration could name somebody else's item as a container and have a new item created inside it, since
   the kernel's contained-spawn arm only checks that the parent exists and is not terminal.

   Evidence: six new cases in `CreationBeforeOperationTests` (`AUseReportForAnUnjudgedItem_AfterTheCarriedSetWasStated_IsRefusedAndNothingIsMaterialized`,
   `..._BeforeTheCarriedSetWasStated_IsStillMaterialized`, `ARefusedUseReport_ReStatesTheCarriedSet_AndTheItemLandsWithTheUsedState`,
   `AUseReportForARefusedCreation_StillAnswersItsOwnReason_WithAndWithoutAStatement`,
   `ACarriedContainersContents_AreRegisteredWithTheStatement_SoTheirUseIsNotRefused`,
   `ACarriedStatement_MayNotCreateContentsInsideAnotherMembersItem`), two in
   `CarriedInventoryReportScheduleTests` (`RequestNow_MakesTheNextReportDue_MidWindow_WithoutRefillingTheDenseBudget`,
   `RequestNow_OnAClosedWindow_OpensOne`) and one in `CarriedInventoryRegistrationTests`
   (`TheWindowsOpeningStep_StatesAnEmptySet_SoTheHostJudgesIt`). FIVE of the nine were observed RED: two in
   the first run against the previous handler (0 refusals, item materialized as the reporter's carried fact),
   and three by mutation control in the finished tree — the tombstone case (the tombstone branch removed →
   red), the nested-contents case (contents adoption neutered → red) and the empty-statement case (the empty
   send disabled → red) — plus the ownership case (the ownership test widened to accept any row → red, the
   item created inside the other member's container). The refusal is the answered shape with its own reason
   (`RejectionReason.UnclaimedItem`) and the judged kind (`RefusedKind`), the item is not in the host's
   kernel afterwards, and the settlement case closes the loop in one round trip: after the refusal the
   registration is due at once, and the re-stated set lands the item on the host WITH the state the use
   produced (0.5f, a value the first statement did not carry). The port is `ICarriedRegistrationFacts`,
   answered by `ItemIdCoordinator` (it receives that report), and the record is session state it clears on
   the session edge.

   Self-check for S3a (mechanism × change × evidence):

   | Mechanism | Change | Evidence |
   |---|---|---|
   | The heal's creation channel (`KernelProtocolCommandHandler`'s `updateForAnUnjudgedId` branch) | conditional on the host's own record of the member's carried-set statement; the refusal is answered with its own reason | `AUseReportForAnUnjudgedItem_AfterTheCarriedSetWasStated_IsRefusedAndNothingIsMaterialized` (RED in the first run: 0 refusals, item materialized) and `..._BeforeTheCarriedSetWasStated_IsStillMaterialized` (the window unchanged) |
   | The tombstone's order (`TryRefuseRefusedCreation`) | answers first on BOTH halves of the state-report path, and its log names the kind it judged | `AUseReportForARefusedCreation_StillAnswersItsOwnReason_WithAndWithoutAStatement` — RED with the tombstone branch removed; the without-a-statement half is the one a heal-first order breaks |
   | The carried-set fact (`ICarriedRegistrationFacts` ← `ItemIdCoordinator`) | recorded when the statement is judged, cleared on the session edge; the window's opening step states an empty set | `TheWindowsOpeningStep_StatesAnEmptySet_SoTheHostJudgesIt` — RED with the empty send disabled; lifetime is `ResetSessionState` |
   | The statement's contents (`ItemArbitration.AdoptCarriedContents` + `OwnsRow`) | an unknown child of a row the member owns becomes a contained row; contents of a row the member does not own are not adopted | `ACarriedContainersContents_AreRegisteredWithTheStatement_SoTheirUseIsNotRefused` — RED with the adoption neutered; `ACarriedStatement_MayNotCreateContentsInsideAnotherMembersItem` — RED with the ownership test widened |
   | The settlement (`ItemService.OnCommandRejected` → `RequestCarriedInventoryReport` → `CarriedInventoryReportSchedule.RequestNow`) | one re-statement is due at once, on the existing cadence; never the pickup/block-drop rollback | `ARefusedUseReport_ReStatesTheCarriedSet_AndTheItemLandsWithTheUsedState`; `RequestNow_..._WithoutRefillingTheDenseBudget` / `RequestNow_OnAClosedWindow_OpensOne` |
   | What did NOT change | the pre-statement window still heals; the console write's settlement is untouched; no wire shape changed (`UnclaimedItem` is an appended reason value) | `..._BeforeTheCarriedSetWasStated_IsStillMaterialized`; `CommandConsoleMemberItemDataTests` green; the diff carries no `Protocol/` file |

   **Read on the real machine 2026-10-17, batch `20261017-c`** (`docs/evidence/acceptance/validation-and-source-checks-s3a-20261017-c.md`):
   the row PASSES end to end with two clients. A member holding an item whose creation it never reported (created
   through the game's own grant inside CUO's own inventory-internal scope, so the pickup report is suppressed by the
   product's classification, and stamped by the product's own allocator because `Item.Start` allocates only for a
   standalone world item) used it: the host answered with the S3a lineup —
   `Kernel command from <member> refused: item <id> was never judged, and that member's carried-set statement already
   was — …` plus its own answer `ItemUpdateState refused (UnclaimedItem)` — the member re-stated the carried set on the
   spot (`the next carried-set report is due now`), and the host's own kernel row then held the item as
   `Carried` under that member with the state the use produced (liquid 150 of the 250 ml it was created with, condition
   0.6 — the definition's condition-from-contents). The same item survived on the reporting client, so the two sides
   ended up agreeing. An ordinary stretch of the same session carried ZERO such refusals, the whole session exactly
   ONE (the forged one) and zero protocol-violation refusals.

   **What S3a does NOT close — the second clause, "in a state that permits that use".** A use report still
   states the item's post-use state and the host takes it as the fact: condition, liquids and components are
   the holder's own claim by design, and the digest comparison that used to correct them is now dead code
   kept alive by its own tests (`ItemArbitration.CheckEvidence` has no production caller). Turning THAT into
   a refusal needs a data anchor per use family — the game's own item data (a use's precondition is
   `ItemStats.usable`, `Body.cs:2449-2456` and `:2475-2481`, and a family's own requirements such as a
   limb), which the host would have to read through an adapter port and check per family. No anchor exists
   for the item's own numeric state: `Item.condition` starts at 1f but is decremented without a clamp
   (`Item.cs:7174`, the many `item.condition -= …` use actions), `SetCondition` sets an arbitrary value and
   at least one path raises it (`Body.cs:1281`), so "condition may only fall" and "condition is 0..1" are
   both FALSE for at least one arm — a check built on either would refuse ordinary play. **Limits S3a
   carries**: (a) the container SYNC's heals are the same creation shape and were NOT bounded by this fact —
   a missing parent was materialized as the reporter's own carried item, a listed unknown child as a
   contained row under the parent THE REPORT names (`ItemDomainModule.DecideSyncContainer`), and those rows
   are the same claim the statement makes; the follow-up cycle that bounded them landed with this anchor
   (`member-report-bounds.md`, the `ItemContainerSync` row), so this limit is closed rather than owed; (b) the
   bound rests on a statement the member VOLUNTEERS: a
   client that never states a carried set keeps the pre-change behaviour, which is the `ItemSpawn` census
   row's unbounded creation class (one state report == one spawn report at that point), filed in
   `todo/member-report-bounds.md` — this stage narrows the reporting shape, it does not turn the item's
   creation into a checked fact; (c) the re-stated set is itself a creation channel: `ItemArbitration`
   does not consult the refused-creation tombstones there, so the settlement of a refused report can
   re-create an id whose creation was refused — the same `ItemSpawn` class, and the reason the tombstone
   answers first on the REPORT path is that the SETTLEMENT is what re-states; (d) an empty statement is a
   statement, so a capture that is empty only because ids could not be stamped yet (an item still
   generating, a remote kill that zeroed an id) tells the host the member carries nothing, and that
   member's first use of the item costs ONE refused report — settled by the immediate re-statement, whose
   capture now holds the id, so no state is lost and no divergence survives it; a body that cannot be read
   at all is NOT this case (the reporter leaves the step unspent); and (e) a root the kernel REFUSES (an id
   it records elsewhere, a terminal one) never reaches the contents half, so an item nested inside it is
   never judged and every report about it is refused — the same nested-item shape, reached only through a
   registration the host disagreed with, not through ordinary play.

   **The independent review's findings and where each went** (`docs/development/review-prompt.md`, FULL tier,
   fresh context, frozen tree, two rounds). ROUND ONE. BLOCKER: the bilingual pair gate failed because
   `docs/standard/alignment.txt` had not been re-recorded for the two `internals/permissions-and-security.md`
   edits — re-recorded, both hashes and the pair gate green. MAJOR 1: "the bound is opt-in — a member that
   never states a set keeps the old hole open for the whole session", because `SendCarriedInventory`
   returned before the send on an empty capture and the ticket claimed the residual was "only the window
   before its first report" — the empty statement on the window's opening step now sets the fact, and the
   residual is stated as limit (b) instead of the false claim. MAJOR 2: "the stated way back cannot settle
   the reports the capture cannot state (nested container contents)", and the census page's reason was wrong
   ("a carried-set statement does not carry contents" — it does; the host dropped them) —
   `AdoptCarriedContents` now materializes them, the census wording is corrected, and the nested case has
   its own test. MAJOR 3: the census page claimed the report materialized an item "in exactly one window",
   contradicted by the same page's container-sync limit — reworded to the report family it covers, with the
   container sync named as the uncovered channel. MINOR: the client keyed on `(ItemUpdateState,
   UnknownAggregate)`, a pair two other answers also produce — `RejectionReason.UnclaimedItem` is its own
   reason now; the tests pinned frame counts rather than the verdict — they read the reason and the
   `RefusedKind` now; a stale rationale in `GuestCommandReconciliation` still described the heal as
   unconditional — corrected; and a settlement that re-armed the dense window on every refusal turned a
   repeated refusal into a report every burst step — `RequestNow` makes one report due without refilling the
   budget while the window is open. ROUND TWO (the delta): MAJOR — `AdoptCarriedContents` did not check that
   the parent row was the registering member's own, so a registration could create an item inside ANOTHER
   member's container (fixed with `OwnsRow` against the kernel record, and the case has its own RED-by-
   mutation test); the census's `CarriedInventory` row still described a registration that creates no rows
   (rewritten); and "an unmodified client never meets this refusal" was falsifiable through an empty capture
   whose body cannot be enumerated — the reporter now leaves the step unspent when there is no body, and the
   unavoidable residual (ids not stamped yet) is limit (d). Its minors are fixed too: the tombstone's log
   line names the KIND it answered instead of saying "container sync" on the state-report path; the
   statement's ownership rule is documented where it is enforced; the RED count in the evidence paragraph
   names its cases and now matches the tree; the inert-capture warning no longer repeats after an empty
   opening step; and the ticket's "exactly one window" wording is gone. The nit left open: the record's
   run-boundary behaviour is written down where it is held (a new run does not clear it; the statement is
   re-judged on the generation edge).

   **S3b — landed 2026-10-17, and it is the second clause: "in a state that permits that use".** The missing
   half was never the check; it was the CLAIM. The one wire kind carried a slot move, a gun transition and a
   native use in the same payload, so no bound could tell them apart: a check on the kind would have refused
   ordinary play (moving a bandage between slots) and a check without one could not see a use report at all.
   The console write had already shown the answer (S1b): a claim that needs its own verdict gets its own
   kind. **The native use now travels as `WireCommandKind.ItemUse`** (`ItemActionSync.SendItemUse`, the two
   `Body` call sites), and every state change that is NOT one of those two guarded uses stays on
   `ItemUpdateState` and claims nothing — the slot move, and the gun chain's own persistent transitions
   (`GunStateSync`'s rack/safety/load/unload/auto-rack route, which reports through
   `ItemUseSync.OnItemStateChanged` and `IItemControl.SendItemState`). That split is not cosmetic: a rack is
   not a use, and sending it under the use kind would have let the use bound refuse a legitimate transition
   of a gun whose own data does not mark it usable. The host judges the use claim with the one data anchor
   the game gives: the item's own `ItemInfo.usable`, the flag BOTH use call sites require (`Body.cs:2452`
   is `usable && usableWithLMB` plus consciousness, `:2477` is `usable` alone — the necessary flag is
   `usable`, so that is the bound and the stricter flag is deliberately not asked), read from the HOST's
   registry through the adapter port `IKernelItemUseFacts` and keyed on the definition id the host's own
   record holds — never on the report's identity. The port is three-valued and the difference matters:
   `Forbidden` is refused BY NAME (`RejectionReason.UseNotPermitted`) and ANSWERED, while `Unknown` (a
   definition this host's registry does not carry — a mod it does not have) is admitted as before, because a
   read that failed is not evidence that the item is unusable (the `RecipeUnlock` row's own rule). The way
   back is S1b's settlement, generalized: `ItemDataWriteSettlement` becomes `ItemStateSettlement`, and a
   refused use puts the session's recorded state back onto the sender's own object — the use already ran
   there before it was reported, exactly like a console write. An item the use consumed locally is the one
   shape that settle cannot repair, and the warning says so.

   **What this does NOT bound, said here because the opposite reading is the natural one.** It bounds the
   CLAIM, not the numbers: a report that claims no use (a slot move, a gun transition, a state drift) still
   carries the holder's own condition/liquids/components, because the game's own item state has no anchor —
   the ticket's S3 paragraph says why, and a `bound:` cell for those values could only restate a rule the
   game does not have. The census rows say the same (`ItemUpdateState` names the residual, `ItemUse` names
   what it bounds) instead of reading as a whole-item guarantee. The kind is also OUTSIDE the guest's
   pending-command re-report window (`GuestCommandReconciliation.IsReReported`), exactly like the state
   report it was split from: a swallowed use report converges through the item's next report or the absolute
   item snapshots (matrix row I7), not through a re-send.

   Evidence: five new cases in `KernelItemReportEligibilityTests` (forbidden refused and answered with its
   audit line naming the definition, permitted admitted, unreadable admitted, another member's item keeping
   the eligibility verdict, and a plain state report of the same forbidden item still admitted — the last is
   what pins "the bound is the use claim, not the state"), three in `CommandAdmissionIntegrationTests`
   (forbidden: host row unchanged, `UseNotPermitted`/`ItemUse` on the wire, the sender's local object settled
   back to the recorded value and never the pickup rollback; permitted recorded; unreadable recorded), and
   one in `ItemActionSyncTests` (the state sender records a transition of the same forbidden item with no
   claim and no refusal — RED if that sender used the use kind; the GUN CHAIN's one-line call into it is
   adapter-only and no test reaches it, named under "Not covered"), and
   the authority census grew to 26 rows while the sender gate's member-authored floor moved 12 → 13 (the new
   `SendItemUse` send site is what satisfies it). Three mutation controls, each with that mechanism alone
   turned off, over the two suites (35 cases): with the seam's use rule keyed on the wrong kind exactly TWO
   go red (the forbidden unit case and the forbidden end-to-end case), with `SettleUse` a no-op exactly ONE
   goes red (the end-to-end case's local-object assertion), and with the sender back on the old kind exactly
   ONE goes red (the end-to-end case: the host records the use) while the unit cases stay green — which is
   what makes the new kind load-bearing rather than cosmetic.

   Self-check for S3b (mechanism × change × evidence):

   | Mechanism | Change | Evidence |
   |---|---|---|
   | The use claim's own wire kind (`WireCommandKind.ItemUse`, payload 30) | sent by `ItemActionSync.SendItemUse`; the mapper produces the same `UpdateItemStateCommand` the console write does, so the FACT is unchanged and only the claim is distinguishable | the sender gate's floor 13 (it fails without the send site); mutation control 3 — the host records the forbidden use when the sender is back on `ItemUpdateState` |
   | The state route's own sender (`IItemControl.SendItemState` ← `ItemActionSync.SendItemState`) and the gun chain on it | a persistent transition that no guarded use produced reports under `ItemUpdateState` and claims nothing | `ItemActionSyncTests.StateChangeWithoutAUseClaim_IsRecordedForAnItemWhoseDataForbidsAUse` (the forbidden definition is recorded and nothing is refused — RED if the state sender used the use kind); the gun chain's CALL into it (`GunStateSync` → `ItemUseSync.OnItemStateChanged`) is a one-line adapter route no test reaches — named below |
   | The data anchor (`IKernelItemUseFacts` ← `GameItemUseFacts`, `Item.GlobalItems` → `ItemInfo.usable`) | the seam reads the RECORD's definition and refuses only `Forbidden`; `Unknown` admits and is logged where the read happened; a build without an adapter answers `Unknown` (`UnavailableItemUseFacts`) instead of inventing a refusal | the forbidden unit + end-to-end cases (RED with the rule keyed on the wrong kind); `.NativeUseOfADefinitionThisHostCannotRead_IsAdmitted` / `..._IsRecorded` |
   | The refusal's name and answer (`RejectionReason.UseNotPermitted`) | answered with `WireCommand.RefusedKind = ItemUse`, so the sender knows WHICH report the verdict judged | `.NativeUseOfAnItemWhoseOwnDataForbidsIt_IsRefusedAndAnswered`; `CreationBeforeOperationTests` reads its own refusals' kind (the creation half is unchanged for the new kind) |
   | The way back (`ItemStateSettlement`, generalized from S1b) | a refused use writes the recorded state back onto the sender's object; the console-write event stays console-only; the routing lives in its own `ItemCommandRejectionRouter` (the size gate's split) | `.ANativeUseOfAnItemWhoseOwnDataForbidsIt_IsRefusedAnsweredAndSettled` — RED with the settle skipped |
   | What did NOT change | the creation-before-operation invariant and the S3a heal now cover the use kind (`IsItemOperation`, the unjudged-id branch); a slot/state report's values stay the holder's claim; the limb families keep their record-based bounds | `CreationBeforeOperationTests` green (with the new kind's `RefusedKind`); `KernelItemReportEligibilityTests.StateReportOfAnItemWhoseOwnDataForbidsAUse_IsStillAdmitted`; `ItemActionSyncTests` green |
   | Not covered | `ItemInfo.usableWithLMB` (the LMB call site's stricter flag) is not checked — the report does not name which call site ran; an item the use consumed locally cannot be brought back by the settle (logged); the item's numbers stay unanchored (see the paragraph above); the adapter's `Item.GlobalItems` read is one line exercised only on a real machine; and the gun chain's one-line CALL to the state sender is adapter-only (the sender it calls is pinned, the call site is not — an ordinary gun's transition is read by the acceptance batch's row 6) | named here rather than implied |

   **The independent review's findings and where each went** (FULL tier, fresh context, frozen tree). ROUND
   ONE (interim report at its first milestone; the reviewer was stopped before its final report, so its
   remaining checks — the gates, the test bodies, the reference set — were re-run by the parent in the same
   round and are the ones this section cites). MAJOR: the gun chain's transitions reached the new use kind
   with no `usable` guard, so a gun whose definition data does not mark it usable would have had every
   rack/safety/load refused and settled back — a legitimate mod-gun transition undone by the bound. Fixed in
   this commit by splitting the ROUTE, not the guard: `ItemUseSync.OnItemStateChanged` reports through
   `IItemControl.SendItemState` under `ItemUpdateState`, `GunStateSync` calls it, and the new
   `ItemActionSyncTests` case pins the shape; the ticket's "mod gun" limit is gone because the exposure is.
   MINOR: the premise sentence said both call sites run behind `usable` — the LMB site runs behind
   `usable && usableWithLMB`; the port doc, the census row, this section and `current.md` now state the
   necessary flag and why the stricter one is not asked. MINOR: the docs did not say whether the new kind is
   in the guest's re-report window — it is not, and the paragraph above now says so and names how a swallowed
   report converges. The parent's own sweep of the same round also fixed two stale sentences (the census's
   "gun transition's sibling shape" and §7.1's "stages still open") and two historical records whose ticket
   path had moved. ROUND TWO (the delta, on the fixed tree): started in a fresh context to re-attack the
   fixes; it did not return a report before the parent stopped it, so there is no round-two finding to
   record. The fixed items were verified instead by re-reading the senders (`SendItemUse` reachable only
   from the two guarded `Body` call sites; `GunStateSync` on the state sender), the focused suites (61
   cases including the new state-route case) and the 693 gates; that sweep also caught and corrected one
   over-claim of this ticket's own (a test that pins the STATE SENDER, not the gun chain's call).

   **What is verified and what is not.** The mechanisms and the three mutation controls above are unit and
   integration evidence against the production composition root. **The machine row was read on 2026-10-17,
   batch `20261017-o`** (`docs/evidence/acceptance/validation-and-source-checks-s3b-20261017-o.md`), and it
   is the acceptance table's row 7: a member holding a `bandage` — created through the game's own grant inside
   CUO's own inventory-internal scope, so the pickup report is suppressed by the product's call
   classification, then landed on the host by the member's carried-set statement — wrote its own post-use
   `condition` (0.4, read back in its own step) and sent ONE hand-built `ItemUse` command through
   `IKernelProtocolControl.SendCommand` carrying it. The host refused it BY NAME on its own registry read
   (`UseNotPermitted: item <id> is 'bandage', whose own data says no native use exists (ItemInfo.usable is
   clear)`), answered it (`ItemUse refused (UseNotPermitted)` on the member), the member's object was settled
   back to the recorded `condition 1` by the product's own write (`[ItemWrite]`, `[ItemState]`), and the
   host's own row stayed at `condition 1`, `revision 1`, `Carried` under that member across both forged
   reports. The two controls that make it a bound rather than a blanket refusal were read in the same
   session: the SAME forged shape for a `painkillers` (whose own data marks it usable) was ADMITTED and
   recorded (`condition 0.5`, `revision 2 → 3`), and an ordinary `Body.UseItem` of that item still reported
   and landed (`[ItemUsed] … reported (use digest)`, both sides at `liquid 90`, `condition 0.9`,
   `revision 2`). The whole session carried exactly the two forged `UseNotPermitted` refusals and zero
   protocol-violation or command-rule refusals, so an ordinary client meets none. What the batch did NOT
   read: the gun chain's call site stays uncovered for the reason this ticket already gives — and the run
   adds the concrete one, that this build's item registry carries no gun at all (the 123 definition ids in
   the reversed `Item.SetupItems`), so no carried gun can be staged on this map.

## Limits S2a carries

- **The table is a mirror.** `GameBlockDropTable`'s vanilla rows transcribe the game's switch at the build
  this tree reverses (`WorldGeneration.cs:751-837`); a game update that moves an arm is not caught by a test
  (the switch is code, not data, so no test that does not parse the assembly's IL can see it). What makes
  such a drift diagnosable rather than silent is the host's refusal line — it names the block id, the table
  and every refused drop — plus the batch row that reads an unmodified session for refusals.
- **The item ids are the game's own ids** (`scrapmetal`, `glassshards`, …), compared ordinally against the
  id the report carries. The table test cannot prove that identity (the game's item registry is built at game
  startup), so the batch read of an ordinary break is what proves it for the blocks that run exercises.
- **The condition is checked only where the table declares a window** (the three scrapmetal arms, and a custom
  tile's authored range). Every other arm leaves the item's own default standing, so its condition is not
  verifiable and is not checked — a deliberate half-open side, pinned by a test rather than left implicit.
- **A same-cell race can pair a report with the wrong block.** The arbitration record is one per
  (sender, cell) and a repeat air write overwrites it, exactly as first-writer-wins always did; if another
  block is placed on that cell between a member's break and its one-frame-later report, the report is bounded
  against the NEW block. The consequence is a refused drop set (the breaker destroys drops it really rolled),
  never a divergence — the host relayed none of them, so both sides agree — and the refusal line names the
  block, which is what makes it diagnosable. Pairing exactly would need a break identity on the report, which
  the wire does not carry; the report is cell-keyed by design.
- **The roll's FREQUENCY is not bounded, only its outcome.** The game's arms are chance-gated
  (`Random.value < 0.5f` and friends), and a bound cannot check a probability: a modified client can make
  every break of a scrapmetal block produce that scrapmetal. The user's "probability window" is answered
  where it can be — the CONDITION window an arm rolls (`Random.Range(0.05f, 0.2f)`) — and the gate itself is
  unreachable on the host, whose own roll is suppressed (`ignoreLoot` on every remote apply).
- **The bound constrains the outermost item's id and condition, not its whole state.** An ACCEPTED entry
  still carries the full save-shaped item (components, liquids, container contents) and a receiver applies
  all of it (`RemoteItemSceneOps.ApplyAuthoritativeState` materializes contents recursively). That is the
  same surface the spawn-report family leaves open, and it is named here so the table is not read as a
  whole-item guarantee.
- **The spawn-report family is NOT bounded.** A member's item-spawn report names an item this side has never
  seen and carries no anchor to bound it against; that is the item-creation half of the same conversation and
  is not what this stage closes. Reading S2a as "item injection is closed" would be wrong.

## Rules every stage obeys

- **A refusal needs a way back to agreement.** A host-side refusal with no correction path leaves the two
  sides' pictures different, which is worse than not checking. The models that exist: accept-and-correct
  (items), and refuse-and-roll-back (block drops).
- **Reordering is not cheating.** A report can arrive before the creation it depends on (the codebase already
  handles creation-before-operation); a check that cannot tolerate that would refuse ordinary play.
- **Say what cannot be verified.** "Is the item really in that member's body" has no proof on the wire today;
  it is trusted and logged, and the ticket says so instead of implying coverage.
- **No compatibility layer.** The protocol may change freely before release (the handshake refuses a mismatch),
  so a new check replaces the old shape rather than living beside it.
- **The design principle is not negotiable**: the affected client judges its own experience, the host
  arbitrates and owns the world, latency is never a parameter of a judgement.

## Acceptance (the rows a run must read)

A two-client session where one client is modified by hand or by a test harness to send what a player cannot:

| # | Scenario | Expected |
|---|---|---|
| 1 | The client reports a block break whose drops are outside the block's own table | Refused by name on the host, the client rolls its drops back, both sides agree |
| 2 | The client reports a break whose drops are inside the window but at the edge | Accepted (a legitimate roll must never be refused) |
| 3 | A member sends a command report | Refused unless the host's rule grants that member the command, and the refusal is a named log line |
| 4 | A member sends an item-state report for somebody else's item | Refused by name (the ownership check S1 adds) |
| 5 | A report arriving before its creation | Accepted in the documented order, not refused |
| 6 | An ordinary session, unmodified clients | Nothing changes: no new refusals, no new logs at Warn |
| 7 | A member reports a native use of an item whose own data says it cannot be used (`ItemInfo.usable` clear) | Refused by name on the host and answered; the sender's already-applied state put back to the recorded one; the host's own row unchanged; an ordinary session meets no such refusal |

Where the rows stand after S1a, S1b, S2a and S2b: row 3 is answered on both halves — a kind only the host authors is
refused by name, and a member's console write is refused unless the host's `AllowMemberItemDataWrite` rule
grants the verb, with the refusal answered and the writer settling its own local value back. Row 4 is answered
by the eligibility rule, which reads a carried, installed OR contained row, and by the container sync's own
parent and children. Rows 1 and 2 are READ on the machine for a BLOCK break
(`docs/evidence/acceptance/validation-and-source-checks-s2a-20261016.md`): a drop the block's own table cannot
account for is refused by name on the host, nothing of it is registered or relayed, the breaker is answered
and destroys its local copy; a table-legal drop — including the game's own roll, and an authored window's own
edge — is accepted; one table row accounts for exactly one drop; and the run's ordinary breaks produced no
refusal line at all. Rows 1 and 2's ENTITY half is read on the machine, batch
`20261016-b` run 9 and batch `20261016-c` run 2
(`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`): a forged building drop is refused
by name on the host with NOTHING registered or relayed, the breaker is answered and destroys its own copy, and
no other refusal appears (run 9); and where a building really loses its support, the host captures its own
table, accepts the breaker's OWN roll against it and splits one forged report per drop — the accounted-for
drop relayed, the other refused by name against the captured rows (run 2, whose census also corrected run 9's
claim that this map has no `requireGround` building at all). Row 5 is the
creation-before-operation invariant that already runs above the seam. Row 6 is the batch, and it has TWO halves
to read rather than one: the authority half refuses only kinds no member sender emits (kept true by
`WireCommandSenderAuthorityGateTests`), and the eligibility half refuses only report shapes a member's own
client cannot produce — each sender of those kinds sits behind a proxy guard (`ItemUseSync`'s standing-item
branch, `ItemSlotSync`'s `RemoteCloneRender` check, `GunStateSync`, `ContainerItemSync`), so an unmodified
client meets no new refusal while a modified one does. S1b adds a third half to that read: with the host's rule
closed, a member's `/data set` IS refused, so the batch must read it as the family intends — a refused write
that says so and settles back, never a silent divergence — and the row asserts it on the operator's own
console and on the host's own kernel row. S2a adds a fourth, and the 2026-10-16 run read it: an unmodified
client's break must keep every drop it rolled, so row 6 also reads that no bound refusal appears in an ordinary
session, and when one does appear the bound's own line names the block, the table and the drops it refused.
S3b adds row 7 — READ on 2026-10-17 by batch `20261017-o`, see its own section — and one more half to row 6:
an unmodified client's native uses must all keep passing, because the item's own data admits them (a refused
`UseNotPermitted` in an ordinary session is the finding, and its line names the item and the definition).
That half is read too: an ordinary `Body.UseItem` of a `painkillers` reported through the use kind, the host
refused nothing and both sides read the post-use state, while the session's only two `UseNotPermitted` lines
are the two forged reports the batch sent.

**Rows 1–6 after the 2026-10-17 batch.** Row 5 is the one the ticket still holds open, and it is the only
remaining row: its refusal half (the creation-before-operation invariant) was read in batch `20261017-c`,
and its accepted half — a report arriving before the creation it depends on, materialized instead of
refused — was NOT staged. The batch's own reading of why is sharper than "not attempted": the window it
lives in is spent by the member's carried-set statement at the generation-finished edge
(`CarriedInventoryReporter.Update` opens the window on that edge and the next frame sends the statement),
which is BEFORE any in-process probe can run, so a freshly entered client already carries a judged
statement by the time a recipe can send anything. The one avenue left is the JOIN/RECONNECT edge, where the
host's id-watermark grant re-opens the window under a session that is already connected — a report driven
inside that edge is the way to read it, and the next batch should try it there. If that edge cannot be won
either, the row becomes a judgement for the user (whether the invariant plus its unit case
`..._BeforeTheCarriedSetWasStated_IsStillMaterialized` is the acceptance), not a self-issued pass. Its
verdict in batch `20261017-o`'s record is `unproven`.

**Read on the real machine 2026-10-14, batch `20261014-a`**
(`docs/evidence/acceptance/validation-and-source-checks-20261014-a.md`): rows 3 and 4 pass in full — a
host-only kind forged from the member's own client was refused by name AND answered while the host's kernel
stayed put; a forged state report for the host's item was refused by name with no answer and no row moved; a
forged container sync was refused for its parent and, in a second report, for its CHILD (the guest's own
container, the host's carried item as the child). Row 6's first half is read too: from world entry to the end
of the session the host logged no admission refusal except the five the run itself provoked. Rows 1–2 still
wait for S2 and row 5 was not exercised.

## Non-goals

- No player-state simulation, no movement validation: the continuous-value half is
  `todo/health-simulation-and-host-authority.md`, and behaviour stays as it is.
- No bans, kicks or scoreboards: a check answers the report, and what to do about a repeat offender is a
  later decision (the user's 2026-10-14 call: "refuse and correct, log it").
