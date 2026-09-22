# Composition root: feature modules and a unified session-reset lifecycle

- Status: Review
- Priority: Medium
- Category: Architecture / maintainability / DI
- Source: Loomi architecture review (2026-09-04); updated 2026-09-20 against the tree (the cycle-detection half has landed); implemented 2026-09-22
- Related: `review/di-cycle-guard.md`, `review/application-layer-first-slice.md`, `../evidence/selfchecks/architecture/composition-root-modules-selfcheck.md`

## Problem (updated 2026-09-20)

The original ticket asked for four verifications. One of them has LANDED and must not be re-done:
"the service graph is buildable and acyclic" — `review/di-cycle-guard.md` records
`ServiceProviderOptions.ValidateOnBuild = true` on `CuoBootstrap.BuildServiceProvider`, a
`DiCycleGuard` that records factory-mediated re-entrant resolution and throws with the full chain,
and `DiCycleGuardTests` covering the contract. What remains is real:

- **One registration list, at the cap.** `src/CasualtiesUnknownOnline.Runtime/CuoBootstrap.cs` was 586
  lines and held the whole container. Decision 200 records the content-vocabulary registrations being
  moved out into `ContentVocabularyComposition` for the single reason that the file sat on the
  600-line cap — the gate, not the design, chose the seam.
- **No unified session-reset contract.** The only lifecycle interface, `ICuoService` in
  `Abstractions`, carries Initialize / Start / Update / Stop and `IDisposable` — no reset stage at
  all. In practice roughly thirty services implemented their own `Reset()`, `ResetForSessionEnd()`,
  `ResetSession()`, `ResetSessionState()` or `OnSessionEnded()`, and nothing asserted that a
  session-scoped singleton had one. Stale state surviving a session boundary is exactly the failure
  this hides.
- **Unbinding is a pattern, not a rule.** `KernelProtocolService` subscribes in the constructor
  (`SessionEnded += ResetForSessionEnd`) and unsubscribes in `Dispose`; no gate required the next
  service to do the same, and a missed unsubscribe keeps a dead session's handler alive. Four
  subscriptions in the tree had no unbind half at all.
- **Update order is explicit by convention only** (the plugin walks `ICuoService` stages in order;
  `GameAdapter.Update` documents its pump order), but nothing asserted the order the tree depends on.

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

## What landed (2026-09-22)

**Feature modules.** `CuoBootstrap.cs` is 156 lines and owns three things: the registration ORDER,
the DI-cycle guard and the startup-failure log. The registrations moved into twelve modules under
`src/CasualtiesUnknownOnline.Runtime/Composition/` (container seams, networking, session policy,
entity replication, world, presentation, console, item tables, item service, player interaction,
mods, save archive) plus the two modules earlier cycles had extracted beside their feature
(`Session/Items/KernelReplicationComposition`, `Session/Content/ContentVocabularyComposition`).
Modules are named for what they own; two expose two entry points because their registrations bracket
another feature in the order (networking: transport before the session, packet plane after it; items:
tables before the kernel replication block, the item service after it).

**The reset contract.** `Runtime/Session/ISessionReset.cs` declares the one method
(`ResetSessionState`). Every Runtime service that owns session state declares it, subscribes the
method to `ISessionControl.SessionEnded` and unsubscribes the same method when disposed. Helper
resets whose owner drives them carry the same name and declaration (item arbitration, id
coordinator, item snapshot service, item kernel authority through `IKernelBatchApplication`, player
stream exchange, world-time local initiation, adaptive stream rates, world-state messages,
guest-command reconciliation, guest checkpoint receiver).

**The gates.** `SessionLifecycleGateTests` (11 facts) enforces: Runtime session-end subscribers name
`ResetSessionState` and declare `ISessionReset`; every method of that name in Runtime + Application
lives in a type that declares the contract; every session-lifecycle subscription (`SessionEnded`,
`SessionActivated`, `MemberAdded`, `MemberRemoved`, `RemoteSceneChanged`, `EntryRepairRequested`,
`LocalSceneReported`) carries its unbind half in the same file; the retired spellings cannot come
back; the Application-layer exemption is complete and not stale; and the matchers have their own
positive/negative samples (contract shape, missed unbind, lambda or qualified handler, an unbind
that exists only in a comment, the retired-spelling names). Census floors are measured, not guessed:
94 session-lifecycle subscriptions (Runtime 66 / Application 2 / Game Adapter 26), 20 Runtime
session-end subscribers, 32 `ResetSessionState` declarations, 1,635 source files.

**Order pin.** `CuoServiceOrderTests` pins the `GetServices<ICuoService>()` order (18 names), the
`IResourceLocationSource` order and that packet-handler discovery still reaches the assembly — the
composition split is proven order-preserving rather than assumed.

**Repairs in the same round.** Four subscriptions had no unbind half: `WorldEntityKernelProjection`
and `ChatService` (session end), `PendingReportFallback` (local scene report), `ItemIdCoordinator`
(member added). Each gained the unbind and the owner wiring that reaches it (`WorldService.Dispose`,
the container's singleton disposal, `CraftSyncService`/`RuntimeEntityChannel` disposal). Two dead
members were removed with their reason: `SessionPeerMaintenance.ResetForSessionEnd` (no caller —
`SessionService.TeardownSession` already performs both of its steps, with the lobby nuance) and
`IKernelProtocolControl.ResetForSessionEnd` (no caller at HEAD; the session-end reaction is the
service's own contract method).

## Design decisions (recorded)

- **The contract is a declaration plus a gate, not a container-driven reset driver.** The
  alternative — a coordinator resolving `IEnumerable<ISessionReset>` on the session end — was
  rejected on evidence: the Application layer cannot reference Runtime or Abstractions
  (`ProjectDirectionPolicy` allows GameState and Protocol only), so its two state holders would need
  Runtime-side bridge classes; and a coordinator reorders teardown (registration order instead of
  subscription order) and resolves services the session may never have used. The ticket asked for
  one contract and a gate, and the declaration plus gate delivers exactly that with the runtime
  behaviour unchanged.
- **The Application layer's kernel protocol service is the single declared exemption**, asserted by
  the gate: it keeps the method name and the paired subscription and cannot implement a Runtime
  interface without breaking the project-direction gate. `GuestCommandReconciliation`, previously
  believed to be in the same position, is a Runtime type and implements the contract.
- **The Game Adapter keeps its own session wiring** (its session binding plus three domain
  `BindToSession`/`Unbind` pairs), declared as a census of four in the gate: its teardown performs
  native writes and runs through its own Bind/Unbind surface.
- **The gate's blind spots are stated in its doc comment** rather than implied: a session-scoped
  service that neither subscribes nor declares the contract is invisible to a source scan; event
  accessor forwards (`+= value`) are skipped; a nested type is validated against its file; comment
  stripping is textual; pairing is file-scoped; shape is proven, not the reset body.
- **`WorldService`'s aggregate is 599 lines against the 600-line cap** (545 plus 54 in its partial) —
  pre-existing headroom of one line, recorded here rather than discovered by the next change.

## Verification (measured)

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0.
- Normative gates — 139/139 (the new gate contributes 11).
- Full suite with build — 3,797 + 139 all passing.
- Independent adversarial review in a fresh context: 0 blocker / 4 major / 7 minor / 5 nit; all
  findings fixed in this cycle (gate teeth widened with six matcher contracts, census numbers
  re-measured, the interface doc corrected, the fifth spelling retired, live evidence prose updated).
  The report is kept at `%TEMP%\cuo-review-composition-modules.md`.

## Not verified here

No game-internal, dual-client or physical-machine evidence: the change is architectural (module
boundaries, one reset method name, teardown wiring) and this cycle's proof is the pinned composition
order, the gates and the full suite. Release-cycle deployment and acceptance remain the user's.

## Notes

The 2026-09-20 review's item 9 applies to this ticket directly: a split is decided by who owns state,
who decides policy, who performs the native write, who manages the lifecycle, who maps DTOs and who
observes and retries — never by line count. The module boundaries above follow that rule; the
line-count effect is a consequence, not the reason.
