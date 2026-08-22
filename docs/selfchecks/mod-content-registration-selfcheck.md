# Mod content registration — mechanism inventory and self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "content registration".
Decision: implement the surface as a **per-mod opaque content definition
registry** — mods register static content facts (items, recipes, NPC types,
etc.) with the framework; the framework stores them as opaque bytes and never
interprets the payload. Content is process-local and does not ride the wire,
so the existing Mod API handshake (mod id / SemVer / permissions / mode)
remains the consistency boundary. No wire change, no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Public API | `IModContext.Content` + `IModContent` + `ModContentDefinition` in `CUO.Abstractions` — the only assembly mods may reference. |
| 2 | Opaque payloads | The framework never interprets or migrates definition bytes; the mod owns its own content schema/versioning (same principle as `IModState`). |
| 3 | Permission | `ModPermission.RegisterContent` is the existing host/state flag; this slice gives it its first live enforcement point (`ModService.Content`). The permission policy already refuses that flag on `ClientOnly`/`Cosmetic`. |
| 4 | Per-mod scope | Each mod's context owns its own `ModContentAdapter`; ids are unique within that mod only. |
| 5 | Safety rails | `ModContentPolicy`: id ≤128, kind ≤64, payload ≤64 KiB, ≤1024 definitions per mod. Errors are refused with a log, never silently truncated. |
| 6 | Framework read view | `IModContentControl.Entries` gives the plugin / future native-content consumers a read-only snapshot across all mods. |
| 7 | Wire | No new NetMsg and no content bytes cross the wire. ProtocolVersion stays 29. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContext` | Added `Content` property (new public API surface). |
| `IModContent` / `ModContentDefinition` | New binding contract for mod content registration (opaque per-definition payloads, defensive copies). |
| `ModService` | New `ModService.Content.cs` partial: per-mod `ModContentAdapter` and `IModContentControl.Entries` snapshot. |
| `IModContentControl` / `ModContentRegistration` | New Runtime control surface for framework-wide content reads (mod id + definition). |
| `ModContentPolicy` | New pure safety rails for ids/kinds/payloads/count. |
| `CuoBootstrap` | Registered `IModContentControl` as a factory over `ModService`. |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Registration API | `ModContentAdapter.TryRegister` validates id/kind/data, permission, duplicate, and count cap | `ModContentTests.BindRegistersContent_ContextExposesIt`, `InvalidRegistration_IsRefused`, `DuplicateRegistration_IsRefused`, `PolicyCaps_AreExactAndNoSilentTruncation`. |
| Permission enforcement | `CanRegister` and `TryRegister` require `ModPermission.RegisterContent` | `ModContentTests.MissingRegisterContentPermission_IsRefused`. |
| Per-mod scope | The adapter lives inside one mod's `ModContext`; ids are per-mod | Code path (`ModService.Content.cs`, `ModContext.ContentAdapter`); no cross-mod shared id list exists. |
| Unregister | `TryUnregister` removes from the per-mod list | `ModContentTests.Unregister_RemovesDefinition`. |
| Defensive copies | Payloads are copied on write and on every read | `ModContentTests.PayloadsAreDefensivelyCopied_OnWriteAndRead`. |
| Framework read view | `IModContentControl.Entries` aggregates every mod with mod id + definition | `ModContentTests.ControlSurface_AggregatesEveryModsEntries`. |
| No wire/protocol regression | No new NetMsg; content is process-local | `../mod-api.md` §7 still says ProtocolVersion 29; full suite green. |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real `ModService` / `TestNode` stack: registration
  semantics, permission refusal, invalid/duplicate/over-cap refusal,
  unregister, defensive copies, policy caps, and the framework-wide control
  snapshot.
- Static evidence: the mod-facing API stays in `CUO.Abstractions`, the
  registry is process-local by design, and the no-wire/no-protocol contract is
  documented in `../mod-api.md` §4f.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1093 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean for tracked/untracked source (only ignored `obj/MyPluginInfo.cs` outside git) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass (arch 600-line/state-bool/one-type gates) |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| `check-delivery.ps1` | pass (checked boxes tracked in `../delivery-checklist.md`) |
| No manual acceptance | per development-period rule |
