# Strict validation / anti-cheat hardening

- Status: Future — **promoted in part on 2026-10-14**: the user put the basic checks on the work list, and
  the stages live in `todo/validation-and-source-checks.md` (source/permission, the drop table, the state
  preconditions). This file stays as the inventory behind that ticket, not as the work item.
- Priority: Low
- Category: Future / security

Explicitly low priority; defer until sync domains are stable.

## Inventory (added 2026-09-21, from the Application-layer cycle)

The admission seam (`KernelCommandGateway`, `docs/decisions/active.md` 210) judges a member's
submission by its declared `AuthorityKind`, and that half was a no-op: every wire command
kind was mapped with `AuthorityKind.OwnerPredictedHostValidated`
(`KernelWireMapper.FromWireCommand`), including the kinds the host's own submitters author as
`HostOnly` (`RunStart`, `AdvanceLayer`, `UpsertEnemy`, `UpdatePlayerStatus`, …). The declaration
therefore carried no member-versus-host information, and a member's `RunStart` reached the run domain,
which does not check authority.

**Landed 2026-10-14 as S1 of `todo/validation-and-source-checks.md`.** The declaration is
`WireCommandAuthorityPolicy`'s per-kind table, filled from the senders the tree actually has and kept
honest by `WireCommandSenderAuthorityGateTests` in both directions, so a member's submission of a
host-only kind is refused by name at the seam; and the seam gained the item-report eligibility family the
same ticket names (a member may report on a world item or on its own carried/installed/contained item,
never on one the record puts in another member's hands — a destroy report, a state report and a
container sync alike).

What is still deferred, and lives in the ticket's own stages: a CONSOLE command's own identifiable wire
path with the host rule that admits it (S1b), the drop-table check (S2) and the state preconditions (S3).
