# Acceptance — the interaction gates are judged by the two clients involved

- Ticket: `docs/backlog/done/remote-interaction-local-gating.md`
- Artifact: the tree at `1b0cebf3` (no runtime change since the two stages landed 2026-09-19; this record is the
  row-by-row judgement of the matrix those stages left, not a new implementation)
- Batch: none needed — every row's seam is pinned by the suite
- Date: 2026-10-24
- Result: **all six rows pass** on the rule-level evidence the ticket itself named.

## Rows

| Row | Evidence |
|---|---|
| 1 — a high-latency actor whose own screen shows a clear line is not refused | `InteractionGateAuthorityTests`: `Push_ActorSeesClearWhileTheHostDoesNot_IsNotRefused`, `MedicalStart_ActorSeesClearWhileTheHostDoesNot_IsAccepted`, `Heal_ActorSeesClearWhileTheHostDoesNot_IsApplied`, plus `Push_HostPictureSaysOutOfReach_DoesNotRefuseTheActor` — the host's stale picture never refuses an actor whose own oracle is clear |
| 2 — a wall on the actor's own screen refuses | the `*_ActorSeesAWall_IsRefusedOnTheActorClient` cases across take, carry, guest heal, use, remote inventory and the Stage-3 start, and `MedicalStart_ActorSeesAWall_IsRejectedOnTheActorWithThePreciseReason` for the medical family's refusal parity |
| 3 — the target's own body decides when the stale report differs | `MedicalStart_TheTargetsOwnBodyOverridesTheHostsStaleReport`, `..._OverridesAStaleHostVerdictOfDeath`, `..._ATargetThatCannotBeJudged_IsRefusedRatherThanJudgedByTheHost`, `MedicalStart_ALiveBodyThatCannotBeRead_IsRefusedInsteadOfUsingAStoredSnapshot`, `ShrapnelStart_TakesThePieceLayoutFromTheTargetsOwnLimb`, and the ticket/liveness cases (`TargetBodyCheck_UnansweredWithinTheLivenessBound...`, `..._AnAnswerFromSomebodyElse_IsDropped`, `..._ASecondAnswerForOneTicket_IsIgnored`, `..._TheTargetLeaving...`) |
| 4 — two actors claim one exclusive unit: the host arbitrates and the loser gets a precise refusal | `MedicalOperationConcurrencyTests` asserts the loser's answer is `"Item is already reserved."` — arbitration stays host-side and the refusal is immediate and precise |
| 5 — host-local interaction unchanged | `InteractionGateAuthorityTests.HostLocalRequest_IsJudgedByTheHostsOwnClient`: the host's own send runs through the same request-formation seam and its own oracle |
| 6 — third-party view unchanged | no wire change in either stage: the effect and its report remain the shared truth (the stages' change sets touched request formation and the target-body question only) |

## Residuals the ticket records on purpose (not rows)

(a) the operator's item facts stay host-side because the item table is host-owned; (b) the appliers' apply-time
re-checks are resolution against authoritative state, not authorization; (c) the take/remote-inventory/carry/
trader target-body refusals are a follow-up family; (d) the operator's own conscious/alive facts stay with the
participant gate. All four are named in the ticket and none is a half-migration.

## Limits

- This is a rule-level acceptance: the tests pin every seam the matrix names, and the two stages' own records
  (`InteractionGateAuthorityTests` 18 cases, `LineOfSightRefusalTests`) are the machine evidence. No new
  dual-client run was made for this record because no row needs a screen reading the suite cannot reach.
- A client that lies about its own reach is the anti-cheat non-goal the ticket already states.
