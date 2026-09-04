# Runtime DI feature registration and lifecycle contract

- Status: Future
- Priority: Medium
- Category: Architecture / maintainability / DI
- Source: Loomi architecture review (2026-09-04)

`CuoBootstrap` contains a large registration list, and many singleton services
coordinate through registration order, event subscriptions, update order, and
session reset. As features grow, hidden initialization order, missing resets, leaked
event handlers, stale singleton state, and test/production composition drift become
likely.

Goal: split registrations into feature modules such as `AddNetworkingFeature`,
`AddKernelReplicationFeature`, `AddWorldFeature`, `AddItemFeature`, and
`AddModFrameworkFeature`, then add automated verification for:

- Session-scoped singletons implement a unified reset lifecycle.
- Event subscribers can be unbound.
- The service graph is buildable and acyclic.
- Update order is explicit and testable.

This is not a request for another business layer; the goal is to make the existing
composition root internally structured and verifiable.
