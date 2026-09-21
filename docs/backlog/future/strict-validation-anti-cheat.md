# Strict validation / anti-cheat hardening

- Status: Future
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
bumping `ProtocolVersion.Current` (decision 188) for the behaviour change, not a new policy inside
the gateway. This is inventory, not a promotion request: the item is still deferred.
