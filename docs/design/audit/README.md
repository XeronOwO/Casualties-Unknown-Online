# Design audit: every ticket against the code and the corpus

A design page is only as good as its grounding. This audit walks the backlog one ticket at a time and answers three
questions per ticket, with the evidence written down:

1. **What does the ticket say the design IS?** 鈥?quoted from the ticket, not paraphrased from memory.
2. **Does the current code do that?** 鈥?the source locations that implement it, or the finding that it does not.
3. **Does the corpus agree?** 鈥?the page and section that carries the design, or `no page`, or `conflict` with what the
   ticket says.

A ticket whose design the code no longer follows is a `conflict` and is filed, not smoothed over. A ticket that the corpus
never mentions is a `no page` and its mechanic page is the fix.

## Scope: what the corpus owes

The corpus documents the PLAYER-FACING design. A ticket is in scope when it changes what a player does, sees, hears or can
be refused; architecture, protocol plumbing and process tickets are in scope for the audit too, but their design record is
`docs/architecture/` or `docs/acceptance/`, and the ledger says so rather than inventing a page. The audit therefore
classifies every ticket into one of: `corpus page` (a page exists and agrees), `no page` (a page is owed),
`out of corpus scope` (architecture/process, record named), `conflict` (ticket and code disagree).

## The ledger

| # | Ticket | Code matches its design? | Corpus page | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | `done/acceptance-records-carry-real-steam-ids.md` | yes 鈥?the rule landed as placeholders plus a gate and a pre-commit check | n/a | `out of corpus scope` | ticket: "Every occurrence in the tracked tree became the placeholder the records already use 鈥?`<host>` for the 鈥?, and "the scan is a pre-commit check, not a gate (decision 261)"; code: `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` exists and the rule is stated in `AGENTS.md` 搂4 |
| 2 | `done/adapter-capability-ports.md` | yes 鈥?the 21-member interface is now a composition of capability ports, frozen by a shape gate | n/a | `out of corpus scope` | ticket: it split "one 125-line interface whose 21 members span eight unrelated concerns"; code: `IGameAdapter` declares `IGameIntegrationLifecycle, IAdapterCapabilityQuery, IWorldPresenceQuery, IStartGateState, IDragUseCueState, ILocalHealItemQuery, ITraderRecruitRequest, INativeInputBlocker 鈥 and its comment says "adding a member here fails `AdapterCapabilityPortShapeTests`" |
| 3 | `done/agent-acceptance-workflow-foundation.md` | yes 鈥?the acceptance area, the preflight and the batched run procedure exist | n/a | `out of corpus scope` | ticket: "the agent-only acceptance area (`docs/acceptance/`, indexed by `AGENTS.md` 鈥?, the read-only dependency preflight (`tools/acceptance/preflight.ps1` 鈥?"; code: `docs/acceptance/AGENTS.md` and `tools/acceptance/preflight.ps1` both exist |
| 4 | `done/application-layer-first-slice.md` | yes 鈥?the Application layer exists and holds the kernel gateway | n/a | `out of corpus scope` | ticket: it builds "the kernel command gateway and the kernel replication move" between Runtime and the kernel; code: `src/CasualtiesUnknownOnline.Application/` carries `CommandAdmission`, `CommandAdmissionKind`, `IKernelBatchApplication`, `GuestCheckpointReceiver` |
| 5 | `done/architecture-split-pass.md` | **no** 鈥?the ticket asserts "no coordinator is close to the 600-line gate"; `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` is 922 lines | n/a | `conflict` | ticket: "no coordinator is close to the 600-line gate"; code: `RunCoordinator.cs` measures 922 lines (`Get-Content | Count`), while the delivery checklist's structure rule is "touched classes <= 600 lines". Filed as `todo/run-coordinator-over-the-class-line.md`; the audit does not decide which side is right |
| 6 | `done/backlog-index-summary-duplication.md` | yes 鈥?the index is a table of pointers with a hard row budget | n/a | `out of corpus scope` | ticket: the index "duplicates every ticket's summary; make it a table of pointers"; code: `BacklogIntegrityGateTests` exists and refuses a row over its 160-character budget (the gate fired twice this session on rows that summarised instead of pointing) |
| 7 | `done/block-break-first-writer-wins.md` | not checked at runtime here 鈥?the ticket's own acceptance record is the reading ("rows 1鈥撯€?pass 鈥?two senders broke one cell in both orders and once concurrently") | none | `corpus page` (written this cycle) | the design is world block arbitration, and `docs/design/mechanics/world-interaction.md` now carries it with the ticket's own reading ("two senders broke one cell in both orders and once concurrently; the host refused the second report") |
| 8 | `done/block-break-log-names-the-wrong-shape.md` | yes 鈥?the diagnostic line lives where the ticket put it | n/a | `out of corpus scope` | ticket: a break's log line "names the lost-air-write shape on every accepted break" and the fix makes the headline "the diagnosis of which shape ran"; code: `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs` carries `OnRemoteDamageBrokeBlock`. Diagnostics are a runtime channel, not a player-facing surface |
| 9 | `done/block-damage-table-capacity-alignment.md` | the tables' alignment is the ticket's own subject; the runtime reading is its acceptance records (a host, a guest and a late joiner "held one identical 128-row set") | none | `corpus page` (written this cycle) | world block damage sync is now on `docs/design/mechanics/world-interaction.md`, which quotes this ticket's own comparison of the two tables (the game's 128-entry list evicting the oldest versus CUO's 256-entry registry refusing new cells) and the user's decision to remove the second table |
| 10 | `done/call-context-scope-leak.md` | yes 鈥?the scope guard is one central type | n/a | `out of corpus scope` | ticket: "a game-side throw inside a scope-entering patched frame latches CallContext off"; code: `src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs` declares `internal static class CallContext`, the single scope entry the adapter uses |
| 11 | `done/carried-inventory-registration-re-report.md` | yes — registration is one-shot and a rejoin re-reports it | `model/item-custody.md` (Registration) | `corpus page` | ticket: "`CarriedInventory` (NetMsg 65) is the guest's one-shot registration of the item ids it self-assigned during generation. The host uses it to build the per-guest transfer table"; row 3's reading: "with the host's per-guest table cleared offline and the kernel facts left untouched, the rejoin re-…". The custody page now carries the rule |
| 12 | `done/carried-player-idle-sit-suppression.md` | yes — one pure carried-ride pose rule drives the presentation family | `mechanics/carry.md` (written this cycle) | `corpus page` | ticket: "The rider's own client never publishes `Sitting=true` while carried", "the carrier-side rider clone … never replays the native sit clips", "the carried-ride `idleTime` is held at zero every frame"; decision 155 supplies the live-carrier invariant |
| 鈥?| the remaining 237 `done/` tickets, then `review/` (36) and `todo/` (10) | | | | |

Rows are filled in order, a batch per cycle, and each filled row carries its evidence: the ticket's own quoted design
sentence, the source path that implements it, and the corpus page that carries it.

## Rules of the audit

- The ticket is read first, in full enough to quote its design statement; a row without a quote is not an audit.
- The code is checked at the place the ticket names, not at a place that looks similar.
- A disagreement between the ticket and the code is recorded as a finding with both quotations; the audit does not decide
  which is right 鈥?the design record does, and if it cannot, the ticket is reopened.
- A disagreement between the code and a corpus page is fixed in the page, in the same cycle, with the ticket's quote.
- A ticket with no corpus page names the page it belongs to; the page is written before the next batch.
