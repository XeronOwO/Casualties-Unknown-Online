# Minimal host rules system + late-join gate — self-check (2026-08-23)

Backlog §2.4 asked for a small independent host-rules service rather than a
broad KrokMP-style rules struct. This cycle lands that service (composed with
the existing respawn rules) and the first real behavior from it: an
`AllowLateJoin` host rule in the handshake path.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `RespawnOptions` | Existing host revive/respawn config section (Permadeath, ReviveFromTrader, ReviveOnNextLevel, KeepInventory, KeepSkills). |
| `HandshakeHandler` | The host-side new-member/reconnect entry path. A brand-new member is created only after protocol/lobby/mod checks. |
| `SessionService.ReportSceneState` / `ISessionControl.LocalInWorld` | The host's local scene state, used to distinguish "host is already in a world" (late join) from menu/generating. |
| `CuoBootstrap` | Default options monitor + `IHostRules` registration; production `Plugin` replaces the monitor with BepInEx config. |
| `Plugin.cs` | Was 611 aggregate lines after adding the host-rule config block, over the 600-line gate; the DI/config block was moved to `PluginDependencyRegistrar` as a by-product real split. |

## 2. Whole-family audit

- The new service is deliberately minimal: `HostRulesOptions` holds only the
  three host-only flags not already owned by `RespawnOptions`
  (`PvpEnabled`, `AutoContinue`, `AllowLateJoin`).
- `IHostRules` composes those three with the existing respawn/save/revive
  flags, so future consumers ask one interface instead of reaching into
  multiple config sections.
- `HostRulesPolicy` is pure and L0-tested; the handshake gate uses it.
- The one wired behavior is late-join: a brand-new member is rejected when the
  host is already in-world and `AllowLateJoin` is false. Reconnects and
  menu-side/new-run joins are unaffected.
- PVP and auto-continue remain reserved flags, not gameplay behavior: PVP has
  no player-to-player damage domain yet (backlog §2.6), and auto-continue is a
  future run-lifecycle flow.
- No wire/protocol change. Host rules are local host configuration only.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host-only flags | New `HostRulesOptions` (`PvpEnabled`, `AutoContinue`, `AllowLateJoin`) | `Runtime/Configuration/HostRulesOptions.cs` |
| Service surface | `IHostRules` + `HostRulesService` compose new flags + `RespawnOptions` | `Session/HostRules/IHostRules.cs`, `HostRulesService.cs` |
| Decision logic | Pure `HostRulesPolicy.CanAcceptNewMember` / `CanAutoContinue` | `Session/HostRules/HostRulesPolicy.cs` |
| Late-join gate | `HandshakeHandler` rejects a new member when host is in-world and late join disabled | `HandshakeHandler.cs` |
| DI/config | Default `MutableOptionsMonitor` in `CuoBootstrap`; BepInEx config bindings in `PluginDependencyRegistrar` | `CuoBootstrap.cs`, `PluginDependencyRegistrar.cs` |
| Tests | Pure policy + service composition + fake-network handshake gate | `HostRulesPolicyTests`, `HostRulesHandshakeTests` |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1261 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| Protocol | unchanged |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Pure policy tests cover the `AllowLateJoin` decision matrix.
- `HostRulesHandshakeTests` drives the real host+guest fake-network stack:
  late-join disabled + host in-world rejects, disabled + host in menu allows,
  enabled + host in-world allows.
- No manual dual-side acceptance is required for this host-only config/rule
  surface (user rule 2026-08-16).

## 6. Plan approval

The user continued the autonomous backlog-completion round ("继续") and
instructed this session to keep picking and completing backlog items without a
separate interactive approval step.

## 7. Structure review

- New top-level types are one per file: `HostRulesOptions`, `IHostRules`,
  `HostRulesService`, `HostRulesPolicy`, `PluginDependencyRegistrar`, plus the
  two test types.
- `Plugin.cs` was over the 600-line gate after the config additions; the
  BepInEx DI/config responsibility moved to `PluginDependencyRegistrar`,
  reducing `Plugin.cs` from 611 to 522 lines.
- No new expression-state bool fields.
- No mutable shared state: `HostRulesService` is stateless and reads the
  option monitors per access.
