# Mod UI — mechanism inventory and self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "UI". Decision: implement
the local mod UI as a **per-mod immediate-mode window registry** whose only
Unity knowledge lives in the plugin — the mod-facing API stays in
`CUO.Abstractions` and never exposes UnityEngine. Local-only, no wire change,
no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Public API | `IModContext.Ui` + `IModUi` + `IModUiWindow` in `CUO.Abstractions` — the only assembly mods may reference. |
| 2 | Local-only | Windows draw locally; they cannot touch network/session/game-authoritative state, so no permission is required and every network mode may use them. |
| 3 | Control alphabet | Label / Button / TextField / Separator — deliberately tiny; Unity/GUILayout never leaks into mod-facing types. |
| 4 | Per-mod scope | Each mod's context owns its own `ModUiAdapter`; duplicate ids are refused within that mod. |
| 5 | Plugin bridge | `IModUiControl.Windows` (Runtime) → `ModUiDrawing`/`ModUiRenderer` (Plugin) project callbacks into Unity `GUI.Window` + `GUILayout`. |
| 6 | Failure isolation | A mod draw callback that throws shows an inline error and is logged by the plugin; the frame/other windows continue. |
| 7 | Wire | No new NetMsg — local presentation only. ProtocolVersion stays 29. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContext` | Added `Ui` property (new public API surface). |
| `IModUi` / `IModUiWindow` | New binding contract for local mod windows. |
| `ModService` | New `ModService.Ui.cs` partial: per-mod `ModUiAdapter`, `IModUiControl.Windows` snapshot. |
| `IModUiControl` / `ModUiWindow` | New Runtime control surface for the plugin-facing list (mod id, window id/title, draw callback). |
| `CuoBootstrap` | Registered `IModUiControl` as a factory over `ModService`. |
| `CasualtiesUnknownOnline.Plugin` | `ModUiDrawing` + `ModUiRenderer` + OnGUI call — the Unity IMGUI bridge. |
| `ExampleMod` | Registers a small "CUO Example" window demonstrating the surface. |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Registration API | `ModUiAdapter.Register` validates id/title/draw and rejects duplicates | `ModUiTests.BindRegistersWindow_ContextExposesIt`, `DuplicateRegistration_IsRefused`, `InvalidRegistration_IsRefused`. |
| Per-mod scope | The adapter lives inside one mod's `ModContext`; window ids are per-mod | Code path (`ModService.Ui.cs`, `ModContext.UiAdapter`); no cross-mod shared list exists. |
| Unregister | `Unregister` removes from the per-mod list and the plugin-facing snapshot | `ModUiTests.Unregister_RemovesWindow`, `Unregister_UpdatesThePluginFacingControlList`. |
| Draw callback intact | `IModUiControl.Windows` carries the exact mod callback and the correct mod/window metadata | `ModUiTests.ControlList_ExposesWindowWithModIdAndDrawCallback` (drives the callback through a recording `IModUiWindow`). |
| Local-only / no permission | No permission check exists for registration; the surface exposes no network/session writes | API docs (`../mod-api.md` §4e); no wire path in `ModService.Ui.cs`. |
| No wire/protocol regression | No new NetMsg; only local Runtime + Plugin files changed | `../mod-api.md` §7 still says ProtocolVersion 29; full suite green. |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real `ModService` / `TestNode` stack: registration
  semantics, per-mod id scoping, unregister, and the plugin-facing control list
  with the draw callback driven by a recording fake.
- Static evidence: the plugin bridge is the only Unity touch point
  (`ModUiDrawing` / `ModUiRenderer`), the mod-facing API stays in
  `CUO.Abstractions`, and the local-only/no-wire contract is documented in
  `../mod-api.md` §4e.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1085 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean for tracked/untracked source (only ignored `obj/MyPluginInfo.cs` reports; outside git) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass (arch 600-line/state-bool/one-type gates) |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| `check-delivery.ps1` | pass (checked boxes tracked in `../delivery-checklist.md`) |
| No manual acceptance | per development-period rule |
