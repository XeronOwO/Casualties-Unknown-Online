# Source, permission and data checks on what a member reports

- Status: Todo — the user promoted this on 2026-10-14 (it was `future/strict-validation-anti-cheat.md`,
  deferred); the old ticket stays as the inventory behind it.
- Priority: Medium-High
- Category: Security / report validation / host rules
- Source: the user's 2026-10-14 request: no advanced anti-cheat, but BASIC checks from the game's own
  mechanics — a destruction report carries its source and must match the drop table (kinds, counts, the
  probability window for a non-integer roll), a command's report needs the sender to hold the cheat
  permission, an item use needs the item to exist in a state that allows it.
- Related: `future/strict-validation-anti-cheat.md` (the inventory this ticket works from — its
  authority-declaration half is closed by S1a below; the console command's own path, the drop table and the
  state preconditions are what remains of it),
  `todo/health-simulation-and-host-authority.md` (the continuous-value half of the same conversation),
  `review/item-data-write-commands.md` (its write verb is host-only today for exactly this reason: a command
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

   **S1b — still open, and it is the half the write verb waits for.** A console command still has no wire path
   of its own: a member's `/data set` would ride `ItemUpdateState`, byte-for-byte a native item use, so the host
   has nothing to grant or refuse separately. What keeps it open is not the wire shape but the way BACK: the
   member's write lands on its own local object before the host sees it, so a host-side refusal has to settle a
   row the client already changed — the missing half S3 names ("what the client does locally when the host says
   that did not happen"). Landing the path without that half would leave the two sides' pictures different,
   which this ticket's own rules forbid, and a path no sender uses is a mechanism with no consumer. So S1b
   lands with the host rule (`HostRulesOptions`, following the existing flags and defaulting OFF — the family's
   "not a cheating surface by default") and the member route together.

   Self-check for S1a (mechanism × change × evidence):

   | Mechanism | Change | Evidence |
   |---|---|---|
   | The wire→kernel authority declaration | new per-kind table (`WireCommandAuthorityPolicy`) replacing the blanket `OwnerPredictedHostValidated` in `KernelWireMapper.FromWireCommand` | `WireCommandAuthorityPolicyTests` — 24 declared rows plus a census that fails on a kind nobody classified; `HostOnlyWireKindFromAMember_IsRefusedAndAnswered` was RED with the blanket value; `WireCommandSenderAuthorityGateTests` keeps the table and the `src/` senders one list in both directions (proved able to fail by flipping one kind each way) |
   | The seam's authority half (`KernelCommandGateway`) | reads a declaration that now differs per kind | `AHostOnlyReportFromAMember_NeverReachesItsDomain` — RED before the fix (the member's entity-health fact committed), green after, and it reads the seam's own audit line so a domain verdict with the same answer cannot stand in |
   | The seam's item-report eligibility (`MayReportOn`) | ONE rule for the destroy report, the state report and a container sync's parent | `AStateReportForAnotherMembersItem_ChangesNoRowAndIsNotAnswered` — RED before (another member's row moved), green after; the destroy and nested cases in `KernelCommandGatewayTests`; the world-rooted escape is what keeps a ground container's own contents reportable |
   | The container sync's reach (`KernelProtocolCommandHandler.HandleItemContainerSync`) | the kind now passes the seam, which it never did | `AContainerSyncForAnotherMembersContainer_MovesNothingAndIsNotAnswered` — RED before (the peer's container row was rewritten and the peer's item moved into the reporter's container), green after |
   | The container sync's children (`MayEnterOwnContainer`) | a listed child may not be an item another member carries or wears | the four container cases in `KernelCommandGatewayTests`, including the admitted `Contained` row a container that changed hands leaves behind |
   | What did NOT change | the domain's own rules and the handler's revision re-stamp | `ItemDomainModule.DecideUpdate` checks existence, a terminal row and the revision only, and `ResolveCommandRevision` re-stamps the revision from the host's own record; `DecideSyncContainer` was left as it is, because the seam is where eligibility belongs |
   | Not covered | a console command's own wire path and the host rule that admits it (S1b), the drop table (S2), the state preconditions (S3), and a `CommandRejected` frame arriving as a command kind (pre-existing: the mapper's default arm threw the same way) | the stages above; `review/item-data-write-commands.md` still refuses a member's `/data set` by name |
2. **S2 — the drop table.** A block or entity destruction report carries its drops; today the host registers
   them first-writer-wins and never checks them against the block's own table. The host re-derives the allowed
   set (kinds, counts, and for a random roll the probability window) and refuses what is outside it — the
   refusal path already exists (`ItemRejectMsg.Reason.BlockAlreadyBroken` makes the reporter destroy the drops
   it rolled). FIRST: establish with evidence whether the drop table is reproducible on the host at all, and
   how its randomness is drawn; the answer decides whether this is a check or a bound.
3. **S3 — state preconditions.** An item use must name an item the host records, in a state that permits that
   use; today the use report's digest is compared with the record and the client is CORRECTED when they
   disagree (`ItemArbitration`) — "wrong but equal-shaped" heals, "not allowed at all" passes. Turning the
   correction into a refusal needs the missing half named: what the client does locally when the host says
   "that did not happen".

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

Where the rows stand after S1a: row 4 is answered — the eligibility rule reads a carried, installed OR
contained row, and the container sync's own parent and children — and row 3 is answered for the host-only
half, a member's report of a kind only the host authors being refused by name; row 3's console-command half
waits for S1b, rows 1–2 for S2, and row 5 is the creation-before-operation invariant that already runs above
the seam. Row 6 is the batch, and it has TWO halves to read rather than one: the authority half refuses only
kinds no member sender emits (kept true by `WireCommandSenderAuthorityGateTests`), and the eligibility half
refuses only report shapes a member's own client cannot produce — each sender of those kinds sits behind a
proxy guard (`ItemUseSync`'s standing-item branch, `ItemSlotSync`'s `RemoteCloneRender` check, `GunStateSync`,
`ContainerItemSync`), so an unmodified client meets no new refusal while a modified one does.

## Non-goals

- No player-state simulation, no movement validation: the continuous-value half is
  `todo/health-simulation-and-host-authority.md`, and behaviour stays as it is.
- No bans, kicks or scoreboards: a check answers the report, and what to do about a repeat offender is a
  later decision (the user's 2026-10-14 call: "refuse and correct, log it").
