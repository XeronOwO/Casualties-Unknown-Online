# HandlerContext per-domain narrowing — self-check (2026-08-23)

Backlog §3.3 called for the remaining `HandlerContext` god-object work:
the world-entry fan-out already moved to `WorldEntryFanout`, but every packet
handler still received the broad all-controls `HandlerContext`. This cycle
closes that remaining item by turning the handler context into a set of narrow
capability interfaces while keeping the no-constructor-deps/acyclic design.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `HandlerContext` | Broad concrete context with 11 control properties; previously passed directly to every `Handle` method. |
| `PacketHandlerBase<TPacket>` | Old single-generic base; `Process` decoded and called `Handle(ulong, T, HandlerContext)`. |
| All `Session/Handlers/*.cs` | 80-ish concrete handlers; most use only one or two controls (e.g. item handlers use `Items` only). |
| `NetMessageRegistry.FindPayloadType` | Reflection-derived payload type from the handler base; needed updating for the two generic arguments. |
| `CuoBootstrap` / `PacketDispatcher` | The composition root and route table remain unchanged; they still pass one `HandlerContext`. |
| `PingHandler` | The only handler that needs no domain control surface; previously still accepted a broad context. |

## 2. Whole-family audit

- Every concrete `IPacketHandler` now derives from
  `PacketHandlerBase<TPacket, TContext>` with a capability interface as
  `TContext`.
- Every `Handle` method now receives exactly that interface; no business
  handler references `HandlerContext` in its signature.
- `HandlerContext` implements all capability interfaces, so the dispatcher can
  still build one object and satisfy every handler.
- The registry’s payload-type derivation was updated to read the first generic
  argument of `PacketHandlerBase<,>`; the second argument is purely a
  compile-time/handler contract surface.
- No wire message, direction, protocol version, handler behavior, DI order, or
  state ownership changed. The refactor is compile-time dependency narrowing
  only.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Handler context surface | Broad `HandlerContext` in handler signatures → narrow capability interfaces | `Session/HandlerContexts/*.cs`, all `Session/Handlers/*.cs` |
| Handler base | `PacketHandlerBase<TPacket>` → `PacketHandlerBase<TPacket, TContext>` with a fail-fast `is not TContext` guard | `PacketHandlerBase.cs` |
| Composition root | `HandlerContext` implements every capability interface; still constructed once in `CuoBootstrap` | `HandlerContext.cs`, `CuoBootstrap.cs` |
| Message registry | `NetMessageRegistry` reads `PacketHandlerBase<,>` first generic argument | `NetMessageRegistry.cs`, `NetMessageMetadata.cs` |
| No-control handler | `PingHandler` uses `IEmptyHandlerContext` instead of an unused broad context | `PingHandler.cs` |
| Regression test | Reflection test over all handlers locks the narrowed-contract pattern | `HandlerContextNarrowingTests.cs` |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1251 passed / 0 failed (full suite) |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| Protocol | unchanged |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- `HandlerContextNarrowingTests` is the runtime-adjacent static proof: it
  reflects every registered handler from the compiled Runtime assembly and
  asserts the declared context is an interface implemented by `HandlerContext`
  and that `Handle` accepts exactly that interface.
- No manual dual-side acceptance is required for this compile-time narrowing
  refactor (user rule 2026-08-16).

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 7. Structure review

- New top-level types are one per file: 17 handler-context capability
  interfaces under `Session/HandlerContexts/`, plus the new test type.
- `HandlerContext` is no longer passed to business handler code; it remains the
  single internal composition root at the dispatch seam.
- Touched classes remain within the 600-line gate (architecture gate passed).
- No new expression-state bool fields.
- No mutable shared state introduced; the interfaces are pure contracts.
- No dead mechanism left behind: the old single-generic base signature and the
  broad `ctx` parameter in handler bodies are gone, not co-existing.
