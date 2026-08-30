# ModService split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (continued). Decision: split the recorded 1590-line logical
`ModService` (eight physical partials) into a thin facade plus real top-level
responsibility classes. No behavior, DI surface, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `ModService.cs` | Was a 419-line partial plus seven more partials (1590 aggregate); now a 98-line normal facade implementing `ICuoService`, `IModsControl`, `IModUiControl`, `IModContentControl`. It composes the internal domain objects and delegates their public surfaces. |
| `ModLifecycle.cs` | New 258-line top-level class owns discovery/load, update/stop/dispose pump, session-event bridge, mod-message routing and the loaded-mod table via `ModCatalog`. |
| `ModCommandService.cs` | New 438-line top-level class owns host-command request/result handling, registration/execution, pending guest callbacks and command rate limits. |
| `ModStateStore.cs` | New 309-line top-level class owns the per-mod key/value state table, versioned/persistent store and the host-gated per-mod `IModState` adapter. |
| `ModContext.cs` | New 485-line top-level class owns one mod's framework surface: network, content, UI, entity-spawn, game-state and native-API adapters plus the lifecycle events. |
| `ModCatalog.cs` | New 26-line top-level class owns the loaded-mod collection (internal state, not DI). |
| `ModPermissionGate.cs` | New 30-line top-level class owns the shared manifest permission bit test + missing-permission log. |
| `ModSessionSnapshot.cs` | New 37-line top-level class owns the bind-time/command-time `ISessionInfo` projection. |
| Deleted partials | `ModService.Commands.cs`, `ModService.Content.cs`, `ModService.EntitySpawn.cs`, `ModService.GameState.cs`, `ModService.NativeApi.cs`, `ModService.State.cs`, `ModService.Ui.cs` removed. |
| DI registration | Unchanged — `ModService` remains the single registered facade/control implementation; the new classes are internal owned dependencies. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old logical class mixed lifecycle, command, state, content, UI,
  entity-spawn, game-state and native-API logic in one partial family. The new
  split gives each responsibility a first-class real top-level owner.
- State ownership: loaded-mod list moves to `ModCatalog`; mod-state table moves
  to `ModStateStore`; per-mod adapters stay inside `ModContext`; command
  pending-callback state stays inside `ModCommandService.ModCommandAdapter`.
- Event forwarding is explicit: `ModLifecycle` owns session-event fan-out and
  message routing; `ModCommandService.FailAllPending` is called on session end
  before the contexts receive `SessionEnded` (preserving the original order).
- No dead mechanism: all moved method bodies are verbatim or explicit
  delegation; the old physical partials are gone.
- No new expression-state bool fields; `ModService` keeps only the idempotent
  dispose flag.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `ModService` | 1590 aggregate → thin 98-line facade | `ModService.cs` |
| Lifecycle | New 258-line top-level class | `ModLifecycle.cs` |
| Host commands | New 438-line top-level class | `ModCommandService.cs` |
| Mod state | New 309-line top-level class | `ModStateStore.cs` |
| Per-mod context/adapters | New 485-line top-level class | `ModContext.cs` |
| Loaded-mod table | New 26-line top-level class | `ModCatalog.cs` |
| Permission gate | New 30-line top-level class | `ModPermissionGate.cs` |
| Old partials | Removed | `git status` / deleted files |
| One top-level type per file | New files contain exactly one top-level type | `check-architecture.ps1` |
| DI / readonly wiring | Unchanged | Source diff; build |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `ModService` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split; the moved method bodies are
  verbatim in the new classes, and the facade is explicit delegation.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to autonomously pick an architecture backlog
item and complete it. This cycle's plan is approved without a separate
interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `ModService` is no longer in
  `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The new classes are internal owned dependencies, not DI-visible shared state.
- Backlog updated: `ModService` removed from the remaining flattening list;
  only `GameAdapter` remains recorded.
