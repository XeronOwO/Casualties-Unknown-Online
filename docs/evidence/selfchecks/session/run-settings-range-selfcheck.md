# Co-op custom run-settings range broadening self-check

Owner cycle: backlog "Custom run-settings range broadening for co-op". Decision:
implement host-side slider-range widening only; do **not** change protocol,
transport, world generation, or the existing world-start params path.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native custom run-settings screen | `PreRunScript.ToggleCustomSettings` / `RunSettings.settingTypes`; `RunSettingDisplay.UpdateSettingDisplay` reads `RunSettingFloat.limits` once on first display init (`RunSettingDisplay.cs:82-84`) |
| 2 | Slider limits | `RunSettingFloat.limits` is a `RangeF` (min/max) baked into the static `RunSettings.settingTypes` list (`RunSettings.cs:26-180`) |
| 3 | Run settings travel path | `WorldParamsService.CaptureAtEntry` reads `HarmonyTraverse.ReadPreRunRunSettings` and publishes `WorldStartParams.RunSettings`; `WorldParamsService.Apply` writes the host's dictionary back on the guest |
| 4 | Host-only start screen | `GuestMenuGuard` disables run-start buttons and closes `runSettingsScreen` for lobby-bound guests; a guest never opens the custom settings in a session |
| 5 | Host-rule config surface | `HostRulesOptions` / `IHostRules` / `HostRulesService` already compose host-only flags with hot reload; Online UI Admin page already has editable host rules |

## 2. Design

- New pure `RunSettingsRange` policy: a curated set of scalable tuning sliders
  (loot/trap density, loot/xp/healing/trader multipliers, time limit, etc.)
  widens the upper bound by the total player count (host + guests).
  Percentage sliders (`traderchance`, `layermodifierchance`) and fixed offsets
  (`traderrepoffset`, `temperatureoffset`) keep their semantic caps.
- New `RunSettingsRangeService` (Game Adapter, Run domain) owns the original
  native limits. It applies the widened limits while this side is the active
  host with `WidenRunSettings` enabled, restores the originals when the host
  rule is disabled or the session ends, and refreshes already-created menu
  sliders directly because the game only reads `RunSettingFloat.limits` during
  each display's first initialization.
- `[HostRules] WidenRunSettings` (default true) is added to the existing
  host-rules config and Online UI Admin page. It is host-local only; no wire
  value is sent to peers.
- No protocol change: selected values continue through
  `WorldStartParams.RunSettings`, which already carries the full dictionary to
  every guest.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Slider limits | `RunSettingsRange.ForCoOp` computes scaled upper bounds | `RunSettingsRangeTests` (7 tests) |
| Host flag | New `HostRules.WidenRunSettings` surface + config + Admin toggle | `HostRulesOptions`, `IHostRules`, `HostRulesService`, `PluginDependencyRegistrar`, `HostRulesConfigEditor`, `OnlineUiAdminDrawer`, `LocalizationCatalog` |
| Apply/restore | `RunSettingsRangeService.Update` detects host session + flag/member count and applies/restores original ranges | `RunSettingsRangeService` |
| Existing menu sliders | `RefreshExistingDisplays` updates slider min/max after the game's one-time init | `RunSettingsRangeService.RefreshExistingDisplays` |
| Start screen host-only | No guest-side interaction changed | `GuestMenuGuard` unchanged |
| World-start params | No change to capture/apply path | `WorldParamsService` unchanged |
| No wire change | None | No `NetMsg`/protocol edits; `ProtocolVersion.cs` unchanged |

## 4. Verification

- **L0 unit**: `RunSettingsRangeTests` (7) covers solo/2-player/3-player
  scaling, percentage caps, offset settings and unknown settings.
- **Integration**: `HostRulesPolicyTests` updated to cover the new composed
  host-rule surface.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` 1373 green,
  `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
