# Composition root: feature modules and a unified session-reset lifecycle

- Status: Todo
- Priority: Medium
- Category: Architecture / maintainability / DI
- Source: Loomi architecture review (2026-09-04); updated 2026-09-20 against the tree (the cycle-detection half has landed)
- Related: `review/di-cycle-guard.md`, `in-progress/application-layer-first-slice.md`

## Problem (updated 2026-09-20)

The original ticket asked for four verifications. One of them has LANDED and must not be re-done:
"the service graph is buildable and acyclic" — `review/di-cycle-guard.md` records
`ServiceProviderOptions.ValidateOnBuild = true` on `CuoBootstrap.BuildServiceProvider`, a
`DiCycleGuard` that records factory-mediated re-entrant resolution and throws with the full chain,
and `DiCycleGuardTests` covering the contract. What remains is real:

- **One registration list, at the cap.** `src/CasualtiesUnknownOnline.Runtime/CuoBootstrap.cs` is 596
  lines and holds the whole container. Decision 200 records the content-vocabulary registrations
  being moved out into `ContentVocabularyComposition` for the single reason that the file sits on the
  600-line cap — the gate, not the design, chose the seam.
- **No unified session-reset contract.** The only lifecycle interface, `ICuoService` in
  `Abstractions`, carries Initialize / Start / Update / Stop and `IDisposable` — no reset stage at
  all. In practice roughly thirty services implement their own `Reset()`,
  `ResetForSessionEnd()`, `ResetSessionState()` or `OnSessionEnded()` (e.g.
  `ItemService.ResetSessionState`, `WorldService.ResetSessionState`, `ItemArbitration`,
  `WorldEntityKernelProjection`, `PlayerCarryService`), and nothing asserts that a session-scoped
  singleton has one. Stale state surviving a session boundary is exactly the failure this hides.
- **Unbinding is a pattern, not a rule.** `KernelProtocolService` subscribes in the constructor
  (`SessionEnded += ResetForSessionEnd`) and unsubscribes in `Dispose`; no gate requires the next
  service to do the same, and a missed unsubscribe keeps a dead session's handler alive.
- **Update order is explicit by convention only** (the plugin walks `ICuoService` stages in order;
  `GameAdapter.Update` documents its pump order), but nothing asserts the order the tree depends on.

## Goal

- Split the registrations into feature modules — networking, kernel replication, world, items, mod
  framework, presentation — so a feature's services register, reset and dispose together.
- Give session-scoped services ONE reset contract rather than four spellings, and gate it.
- Gate event binding: a service that subscribes must implement the unbind half.

## Acceptance

- A new session-scoped singleton that does not implement the reset contract fails the gate.
- A synthetic missed unsubscribe fails the gate.
- The composition behaviour is unchanged: the same services exist with the same lifetimes, proven by
  the existing DI and startup suites.
- `CuoBootstrap` comes off the 600-line watchlist because registrations moved, not because the file
  was split by formatting.

## Notes

The 2026-09-20 review's item 9 applies to this ticket directly: a split is decided by who owns state,
who decides policy, who performs the native write, who manages the lifecycle, who maps DTOs and who
observes and retries — never by line count.
