# Typed seams: no `object` handle and no string-keyed call in the mod contract

- Status: Review — implementation landed 2026-10-09 (decision 250, see *What landed*) and an independent
  adversarial review ran in the same cycle; the acceptance batch is pending. It was **cut 2026-10-08** by
  the user's demand for a full sweep of the abstraction layer ("全量排查，抽象层还有没有其他地方使用了这种
  明显的…模式，这种东西就压根不应该存在"). The sweep found the byte envelopes (two tickets) and exactly two
  `object`-shaped holes plus one string-keyed call, all listed below.
- Priority: Critical
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-typed-registration.md` and
  `docs/backlog/review/mod-api-no-opaque-envelopes.md` (the same sweep), `docs/backlog/todo/mod-authored-effects.md`
  (its effect context needs the same answer: an engine-typed handle and named operations, never `object`),
  `docs/en/reference/modification-policy.md` (the visibility rule: only a designed capability becomes a
  contract, and `internal` plus `InternalsVisibleTo` is the normal shape for what the framework shares with
  its own tests)
- Source: the user's 2026-10-08 rulings — the `GameObject`-typed handle is acceptable, an untyped hole is not.

## The sweep's result, and what this cycle did with each row

| Surface | Level | Shape | Disposition |
|---|---|---|---|
| `IModNativeApi.TryInvoke(string operation, object?[] arguments, out object? result)` | Advanced | a call by NAME, with untyped arguments and an untyped result | **DELETED.** The registry stays — the Game Adapter owns the operation set, so a name is still the only stable handle, and `CanInvoke(string)` is still the availability probe — but the mod-facing path is now one typed projection per operation, `TryGetLocalPlayerState(out IModNativeLocalPlayerState state)`, the existing precedent extended. The ticket had left the dynamic entry standing as a "declared fallback"; that cannot survive rule 15 (`object` on a public `Abstractions` member), and retyping it as `ModValue` would have been worse than deleting it: the Game Adapter could then put a new operation in front of mod authors by editing `IsRegistered` alone, a capability becoming a third-party contract without the review the baseline gate exists to force. `IModNativeApiProvider.TryInvoke` becomes `TryGetLocalPlayerState` on the Runtime → Game Adapter seam, and `ModNativeApiPolicy` (133 → 39 lines) shrinks to the operation-id shape rail. |
| `IStartingSupplyBehaviour.LocalBody` / `Create(string)` / `TryPlace(object body, object item, int slot)` | Stable | `object` handles for a body and an item | **NOT A MOD CONTRACT — moved out.** The test this ticket set decides it: its only implementation is the Game Adapter's `GameStartingSupplyTarget` and its only caller is that adapter's own `StartingSupplyCoordinator`, so no mod path reaches it — and its doc already names `IModItemSpawner` as its shape, which lives in the Runtime. It now sits in `CasualtiesUnknownOnline.Runtime.Session.World`, beside `INativeWorldFacts`. Its handles STAY `object`: identity is the whole of what its caller reads (the grant's once-per-body rule), an engine-typed parameter is not available to an assembly that may not reference the game, and the no-erased-type rule governs the mod-visible contract rather than a framework seam. |
| `IModCommands.TryExecute(string name, IReadOnlyList<string> arguments, …)` with `ModCommand(name, handler, …)` | Stable | a host command by name, with text arguments | KEPT, and recorded as checked: this is a console, and the command is the mod's own — a name and its text arguments are its natural shape, and a typed call per command would have to be declared by the mod itself. |

## What this settles for the effects surface

The rule the sweep produces, which the effect context is designed against: **a handle the framework hands a
mod is either a CUO-defined type or an engine type; it is never `object`.** The target of an effect is an
engine handle, what the effect may write are CUO's named operations, and what the framework reports is a CUO
type. It is already the *handle* question of the review every baseline addition goes through
(`docs/en/reference/modification-policy.md`), and decision 250 records it as this ticket's answer, so
`mod-authored-effects.md` inherits it rather than re-deciding it.

## What landed

- `IModNativeApi` — `CanAccess`, `CanInvoke(string)`, `TryGetLocalPlayerState(out IModNativeLocalPlayerState)`;
  there is no untyped way in, and the two removals are tombstoned in
  `docs/contracts/abstractions-api-baseline.txt`.
- `IModNativeApiProvider` — `IsRegistered(string)` plus `TryGetLocalPlayerState(out …)`;
  `DisabledModNativeApiProvider` and the Game Adapter's implementation follow the seam.
- `ModNativeApiPolicy` — the operation-id shape rail only: its argument/result value surface, its four caps
  and its rank-and-element-type array rule are deleted rather than moved, because a declaration now states
  what a post-hoc scan could only restate after a value had already reached the boundary. The net48 array
  measurement decision 249 recorded still stands; it simply has no `src/` consumer left.
- `IStartingSupplyBehaviour` — `CasualtiesUnknownOnline.Runtime.Session.World` (was `Abstractions`), with the
  reason it is a framework seam written into its own doc.
- Tests — `ModNativeApiTests` (7 cases, reworked rather than dropped: the permission gate, the typed
  projection, no-body, an all-refusing provider, an unregistered operation, malformed ids, and the policy
  rails) and `FakeModNativeApiProvider`; the starting-supply suite's fake needed no edit, because it already
  imported the new namespace.
- Docs — the native-operations section of both blocks (the sample no longer calls `TryInvoke`), decision 250,
  and the contract baseline's five tombstones.

## Limits

- Nothing here needs a game process: every case drives the real composition root through the fake provider,
  and the one Game Adapter implementation is covered by the reflective contract test
  (`GameAdapterNativeApiContractTests`). The acceptance batch re-runs the mod-API rows against deployed DLLs
  as usual.
- `ContentId.Equals(object?)` and `ModValue.Equals(object?)` stay: a BCL override is not an untyped hole,
  and those two are the only `object` left in any signature on the whole contract (measured; the self-check
  §2 records the scan).
- `GameAdapter.cs` is 588 lines after this cycle — 12 lines from the architecture gate's 600. This cycle made
  it smaller, not larger, but the margin is thin enough to name here.

## Non-goals

- Not the byte payloads and not the content kind vocabulary — those are the other two tickets of the sweep.
- No compatibility shim: a typed projection replaces the dynamic entry where one exists, and the old shape
  does not survive beside it.
