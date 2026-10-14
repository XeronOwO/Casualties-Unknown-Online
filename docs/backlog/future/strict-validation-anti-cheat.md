# Strict validation / anti-cheat hardening

- Status: Future — **promoted in part on 2026-10-14**: the user put the basic checks on the work list, and
  the stages live in `todo/validation-and-source-checks.md` (source/permission, the drop table, the state
  preconditions). This file stays as the inventory behind that ticket, not as the work item.
- Priority: Low
- Category: Future / security

Explicitly low priority; defer until sync domains are stable.

## Inventory (added 2026-09-21, from the Application-layer cycle)

The admission seam (`KernelCommandGateway`, `docs/decisions/active.md` 210) judges a member's
submission by its declared `AuthorityKind`, and that half is currently a no-op: every wire command
kind is mapped with `AuthorityKind.OwnerPredictedHostValidated`
(`KernelWireMapper.FromWireCommand`), including the kinds the host's own submitters author as
`HostOnly` (`RunStart`, `AdvanceLayer`, `UpsertEnemy`, `UpdatePlayerStatus`, …). The declaration
therefore carries no member-versus-host information, and the kernel never reads it either
(`docs/architecture/current.md` §7.1) — a member's `RunStart` reaches the run domain, which does not
check authority. Closing it means declaring the real authority per wire kind in the mapper and
bumping `ProtocolVersion.Current` (decisions 188/241 — the number is frozen until the first official
release and bumps per wire change after it) for the behaviour change, not a new policy inside
the gateway. This is inventory, not a promotion request: the item is still deferred.
