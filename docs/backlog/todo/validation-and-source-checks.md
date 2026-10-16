# Source, permission and data checks on what a member reports

- Status: Todo — the user promoted this on 2026-10-14 (it was `future/strict-validation-anti-cheat.md`,
  deferred); the old ticket stays as the inventory behind it. S1a and S1b landed 2026-10-14, S2a
  landed 2026-10-16 **and its acceptance rows are read for the block family**
  (`docs/evidence/acceptance/validation-and-source-checks-s2a-20261016.md`: rows 1, 2 and 6's new half, on a
  deployed artifact), and S2b landed 2026-10-16 as CODE AND TESTS with BOTH halves of its acceptance row 1
  read on the machine (`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`: a forged
  building drop refused by name where the host has no dying building, nothing registered or relayed and the
  breaker answered; and then a census of the population, the host's capture of a real `requireGround`
  building's own table, the breaker's real roll accepted against it, and one forged report whose two drops
  were split per drop — the table-accountable one relayed, the other refused by name). S3 (the state
  preconditions) is the open stage, and the surfaces this ticket does NOT close are the census rows of
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
   **Limits S2b carries.** Two, and both are the same missing fact — the drop does not name the support cell
   it came from — seen from the two sides of the report.

   - **The reporter's side pairs a drop with a break by TIME, not by identity.** The pending-break machine
     holds ONE break (`BlockBreakPendingState.EnterBreak` overwrites it) and `TryAddBuildingDrop` folds
     EVERY building death that appears while one is pending into that break, with no check that the dying
     building's own support cell is the pending cell. A building that dies inside the break's one-to-two
     frame window of an unrelated break therefore rides that break's report, and the host — whose rows are
     the tables of the buildings that lost support at ITS cell — refuses the whole building set by name
     while the breaker destroys drops it really rolled. Before this stage those drops were registered and
     relayed, so this is the one shape where the new bound costs a legitimate item; it is rare (two breaks in
     one frame, or a support loss landing in another break's window) and it never diverges — the host relays
     none of them, so both sides agree on "no drops". The message cannot supply the association the reporter
     would need to check instead: a drop is position-keyed by a transform position and the entity is
     destroyed before its `Item.Start` runs. The fix is therefore a design one, carried by
     `todo/member-report-bounds.md`: read the dying entity's own `blockPlacedOn` while it still exists and
     carry it on the drop marker (the death scope already wraps the roll), then fold a drop only into the
     break for that cell.
   - **An entity already marked for a remote death contributes no rows.** `MarkSupportLossRemote` skips an
     entity that already carries `RemoteEntityDeath` — the marker is what stops the other paths from rolling a
     second set — and the skip sits BEFORE the capture, so an entity marked earlier in the same frame by a
     different path (a relayed attack whose damage took its health to zero, an opened lock, a restored
     health row) adds no rows and leaves the cell's row set empty: every building drop that report carries is
     refused, although the reporter's own copy was never marked and rolled them legitimately. The same fix
     covers it, and it is filed in the same ticket.

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
