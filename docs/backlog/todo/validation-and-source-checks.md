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
   | The way back from a refusal (S1b: `ItemDataWriteSettlement` + `WireCommand.RefusedKind`) | a refusal settles the value the writer already applied, and the answer names the report it judged | `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenTheHostRuleIsClosed_IsRefusedAndTheValueGoesBack` — RED with the settle skipped (local 0.25, expected 0.5); `.WhenRefused_NeverBecameAPickupRollback`; `.ForAnIdTheHostNeverJudged_IsRefusedAndSettled` |
   | What S1b did NOT change | a console write's DOMAIN verdict is the native use's own (`UpdateItemStateCommand`), the host's own console never passes the seam, and the read verb stays open to everyone | `KernelWireMapper`'s two arms; `ItemDataCommands`' host branch; `CommandConsoleItemDataTests` green |
   | What did NOT change | the domain's own rules and the handler's revision re-stamp | `ItemDomainModule.DecideUpdate` checks existence, a terminal row and the revision only, and `ResolveCommandRevision` re-stamps the revision from the host's own record; `DecideSyncContainer` was left as it is, because the seam is where eligibility belongs |
   | Not covered | the drop table (S2), the state preconditions (S3), and a `CommandRejected` frame arriving as a command KIND (pre-existing: the mapper's default arm threw the same way; `KernelProtocolService` answers it only on the guest's own payload-type branch) | the stages above; the write verb's own rows and limits are `review/item-data-write-commands.md` |
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

Where the rows stand after S1a and S1b: row 3 is answered on both halves — a kind only the host authors is
refused by name, and a member's console write is refused unless the host's `AllowMemberItemDataWrite` rule
grants the verb, with the refusal answered and the writer settling its own local value back. Row 4 is answered
by the eligibility rule, which reads a carried, installed OR contained row, and by the container sync's own
parent and children. Rows 1–2 wait for S2, and row 5 is the creation-before-operation invariant that already
runs above the seam. Row 6 is the batch, and it has TWO halves to read rather than one: the authority half
refuses only kinds no member sender emits (kept true by `WireCommandSenderAuthorityGateTests`), and the
eligibility half refuses only report shapes a member's own client cannot produce — each sender of those kinds
sits behind a proxy guard (`ItemUseSync`'s standing-item branch, `ItemSlotSync`'s `RemoteCloneRender` check,
`GunStateSync`, `ContainerItemSync`), so an unmodified client meets no new refusal while a modified one does.
S1b adds a third half to that read: with the host's rule closed, a member's `/data set` IS refused, so the
batch must read it as the family intends — a refused write that says so and settles back, never a silent
divergence — and the row asserts it on the operator's own console and on the host's own kernel row.

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
