# Runtime DI feature registration and lifecycle contract

- Status: Resolved (rewritten and moved 2026-09-20 to `todo/composition-root-feature-modules.md`)
- Category: Architecture / maintainability / DI
- Source: Loomi architecture review (2026-09-04)

## Disposition

Moved, with one of its four requirements already landed. Re-checked against the tree on 2026-09-20:

- **"the service graph is buildable and acyclic" — landed** in `review/di-cycle-guard.md`
  (`ServiceProviderOptions.ValidateOnBuild` on `CuoBootstrap.BuildServiceProvider`, a `DiCycleGuard`
  that records factory-mediated re-entrant resolution and throws with the full chain, and
  `DiCycleGuardTests`). The replacement ticket drops this requirement rather than re-doing it.
- **"session-scoped singletons implement a unified reset lifecycle" — still absent, and now the
  largest piece.** `ICuoService` in `Abstractions` carries Initialize / Start / Update / Stop and
  `IDisposable`, with no reset stage at all, while roughly thirty services spell their own reset four
  different ways (`Reset`, `ResetForSessionEnd`, `ResetSessionState`, `OnSessionEnded`).
- **"event subscribers can be unbound" — a pattern without a gate.** `KernelProtocolService`
  subscribes in the constructor and unsubscribes in `Dispose`; nothing requires the next service to
  do the same.
- **"update order is explicit and testable" — explicit by convention only.**

It moved to `todo/` because the remaining work is real and intended, not deferred: leaving it in
`future/` (where items are not work items and are not carried into handoff prompts) is what kept it
untouched for two weeks. The replacement carries the updated problem statement, the acceptance for
the two gated rules and the same scope limit — it is not a request for another business layer.
