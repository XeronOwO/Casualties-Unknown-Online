# Typed seams: no `object` handle and no string-keyed call in the mod contract

- Status: Todo — **cut 2026-10-08** by the user's demand for a full sweep of the abstraction layer ("全量排查，
  抽象层还有没有其他地方使用了这种明显的…模式，这种东西就压根不应该存在"). The sweep found the byte envelopes
  (two tickets) and exactly two `object`-shaped holes plus one string-keyed call, all listed below.
- Priority: Critical
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-typed-registration.md` and
  `docs/backlog/todo/mod-api-no-opaque-envelopes.md` (the same sweep), `docs/backlog/todo/mod-authored-effects.md`
  (its effect context needs the same answer: an engine-typed handle and named operations, never `object`),
  `docs/en/reference/modification-policy.md` (the visibility rule: only a designed capability becomes a
  contract, and `internal` plus `InternalsVisibleTo` is the normal shape for what the framework shares with
  its own tests)
- Source: the user's 2026-10-08 rulings — the `GameObject`-typed handle is acceptable, an untyped hole is not.

## The sweep's result

The whole public surface contains exactly three occurrences of `object`: one BCL override
(`ContentId.Equals(object?)`, not a hole) and the two below.

| Surface | Level | Shape | Verdict |
|---|---|---|---|
| `IModNativeApi.TryInvoke(string operation, object?[] arguments, out object? result)` | Advanced | a call by NAME, with untyped arguments and an untyped result | the registry itself may stay — the Game Adapter owns the operation set, so a name is the only stable handle — but the mod-facing path must be a typed wrapper per operation. `TryGetLocalPlayerState(out IModNativeLocalPlayerState state)` is the existing precedent and the shape to extend; `CanInvoke(string)` stays as the availability probe, and the dynamic `TryInvoke` becomes the declared fallback rather than the way in. |
| `IStartingSupplyBehaviour.LocalBody` / `Create(string)` / `TryPlace(object body, object item, int slot)` | Stable | `object` handles for a body and an item | two questions, and the first decides the shape: is this a MOD contract at all? Its own doc says the adapter implements it and a composition without an engine supplies its own, i.e. it is a framework seam. If it is framework-internal (visibility rule), it does not belong on the mod-visible contract; if it stays visible, its handles follow the user's ruling — an engine type (`GameObject`), never `object`. |
| `IModCommands.TryExecute(string name, IReadOnlyList<string> arguments, …)` with `ModCommand(name, handler, …)` | Stable | a host command by name, with text arguments | KEPT, and recorded as checked: this is a console, and the command is the mod's own — a name and its text arguments are its natural shape, and a typed call per command would have to be declared by the mod itself. |

## What this ticket must settle for the effects surface

The rule the sweep produces, which the effect context is designed against: **a handle the framework hands a mod
is either a CUO-defined type or an engine type; it is never `object`.** The target of an effect is an engine
handle, what the effect may write are CUO's named operations, and what the framework reports is a CUO type.

## Order

After the two payload tickets of the same sweep — the content contract is what an operation writes into, and the
data model is what a dynamic operation's arguments and a mod's reported value are made of — and before
`mod-authored-effects.md`, which is the surface that would otherwise inherit this shape.

## Non-goals

- Not the byte payloads and not the content kind vocabulary — those are the other two tickets of the sweep.
- No compatibility shim: a typed wrapper replaces the dynamic entry where one exists, and the old shape does not
  survive beside it.
