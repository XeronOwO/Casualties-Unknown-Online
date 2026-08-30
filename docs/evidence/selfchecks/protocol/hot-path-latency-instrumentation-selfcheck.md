# Hot-path latency instrumentation self-check

Owner cycle: backlog "Hot-path latency instrumentation". Decision: add a
small opt-in timing aggregator around the Game Adapter's update pump and gate
it behind a new `[Diagnostics]` BepInEx config section. No protocol change,
no gameplay change, no per-frame logging when disabled.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Update pump | `GameAdapter.ICuoService.Update` calls the domain pumps once per frame (run, world time, start gate, item/player state, world events, fluid, trader, renderer, enemy sync/combat) |
| 2 | Existing config bridge | `PluginDependencyRegistrar` binds BepInEx config entries into `IOptionsMonitor<T>` snapshots; `BepInExOptionsMonitor` hot-reloads on `SettingChanged` |
| 3 | Logging | CUO uses `Microsoft.Extensions.Logging` with BepInEx/rolling-file providers; a one-line-per-domain summary at Information is only emitted when the feature is on |
| 4 | Opt-in rule | Backlog requires instrumentation not affect normal play; default is off and the disabled path is a single boolean read + direct action invocation |
| 5 | Measurement-first | No optimization is made here — this only records call/frame latency for the already-identified heavy pumps |

## 2. Design

- `LatencyOptions` (Runtime) has `Enabled` and `LogIntervalSeconds`; the plugin
  default is `false` / `1.0` seconds.
- `LatencyInstrumentation` (Runtime Diagnostics) aggregates per-name call
  count, total milliseconds, average and max. `Measure(name)` returns a
  disposable scope (null when disabled, so `using` costs nothing unless the
  feature is on); `Measure(name, action)` is available for callers that prefer
  a one-line form and also records in `finally`.
- `GameAdapter.Update` wraps the compute-heavy domain pumps with named
  `using` scopes: `Run`, `WorldTime`, `StartGate`, `Respawn`,
  `ItemPosition`, `WorldEvent`, `Fluid`, `Trader`, `Renderer`,
  `EnemySync`, `EnemyCombat`. `Flush()` at the end of the pump prints one
  `[Latency]` line per name on the configured interval, sorted by total.
- The new config is `[Diagnostics] LatencyInstrumentation` and
  `[Diagnostics] LatencyLogIntervalSeconds`; both are persisted by BepInEx and
  hot-reload through the existing monitor.
- No wire/protocol change; `NetMsg`, `ProtocolVersion`, and sync semantics are
  untouched.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Config gate | New opt-in `[Diagnostics]` entries default off | `PluginDependencyRegistrar` + `LatencyOptions` |
| Disabled path | No collection, no allocation, action runs once | `LatencyInstrumentationTests.Disabled_MeasureInvokesActionAndDoesNotCollect` |
| Enabled path | Per-name calls/total/avg/max aggregation | `LatencyInstrumentationTests.Enabled_MeasureAggregatesCallsAndFlushClears` |
| Scope form | `using` scope records on dispose | `LatencyInstrumentationTests.Enabled_MeasureScopeRecordsOnDispose` |
| Hot reload | Toggling off stops new collection without losing already-collected samples until flush | `LatencyInstrumentationTests.ToggleOff_StopsCollectingWithoutLosingExistingSamplesUntilFlush` |
| Update pump | Heavy domains timed, flush at end of frame | `GameAdapter.ICuoService.Update` |
| No wire change | None | No `NetMsg`/protocol edits; `ProtocolVersion` unchanged |

## 4. Verification

- **L0 unit**: `LatencyInstrumentationTests` (4 tests) covers disabled
  no-op, aggregation/flush, scope disposal, hot-reload toggle.
- **Code gates**: `dotnet build`, `dotnet test` (1443 green),
  `dotnet format` (source, generated `obj` excluded),
  check-architecture / check-event-replay / check-entity-event-dispatch.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
