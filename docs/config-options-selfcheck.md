# Config Options Bridge + Logging Levels + State-Stream Cadence — Self-Check (2026-08-16)

Delivery fact sheet for the Config-section backlog closeout: the BepInEx
`ConfigFile` → `IOptionsMonitor<T>` bridge, the runtime logging minimum, and
the formerly hard-coded 20 Hz state streams. Phase 4 Mod API has landed, so
the 2026-08-09 trigger condition for the config foundation is met.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Old behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Config surface | one plugin-scoped `TargetLobbyId` entry only | two new entries: `[Sync] StateStreamHz` (1-60, default 20) and `[Logging] MinimumLevel` (default Information) | Plugin.cs:87-96 |
| 2 | Options abstraction | absent in Runtime; no `IOptionsMonitor` consumer | `LoggingOptions` + `StateStreamOptions` (normalized clamp) are the strongly typed snapshots | Configuration/LoggingOptions.cs:13; Configuration/StateStreamOptions.cs:14 |
| 3 | Config → options bridge | absent | `BepInExOptionsMonitor<T>` subscribes to `ConfigFile.SettingChanged`, filters watched `ConfigDefinition`s, re-reads the snapshot and notifies `OnChange` listeners | Configuration/BepInExOptionsMonitor.cs:18,81 |
| 4 | Test/default monitor | absent | `MutableOptionsMonitor<T>` is the default DI registration; the plugin replaces it with the BepInEx monitor | Configuration/MutableOptionsMonitor.cs:13; CuoBootstrap.cs:51-56; Plugin.cs:96-105 |
| 5 | DI logging assembly | providers captured as instances inside `AddLogging` | providers registered as `ILoggerProvider` services and resolve `IOptionsMonitor<LoggingOptions>`; the factory minimum stays `Trace` so provider filtering is the only live policy | CuoBootstrap.cs:62-71 |
| 6 | BepInEx log sink | every level forwarded (`IsEnabled` = not None) | `IsEnabled` also requires `logLevel >= MinimumLevel` | BepInExLoggerProvider.cs:80-81 |
| 7 | Rolling file sink | every level written (`IsEnabled` = provider enabled only) | same configurable level gate | RollingFileLoggerProvider.cs:187-190 |
| 8 | Player state stream | `StateSendInterval`/`ReportSendInterval` const = 0.05f | host broadcast and guest report both read `StateStreamOptions.SendIntervalSeconds` every pump | EntitySyncService.cs:340-351 |
| 9 | Enemy state stream | `StateSendInterval` const = 0.05f | reads the same configured interval every pump | EnemySyncService.cs:253-260 |
| 10 | Attack-swing hold | fixed 300 ms (six 20 Hz ticks) | `AttackSwingState` holds `max(300 ms, 6 × configured interval)` so the rising edge keeps its six-tick resilience at any cadence; configured in `MarkLocalAttackSwing` and every `Update` | AttackSwingState.cs:33-48; EntitySyncService.cs:130-134,294-298,316-319 |
| 11 | 1 Hz character snapshot | unchanged | deliberately NOT controlled by `StateStreamHz` — it is the full-fact fallback, not a state stream | StateStreamOptions.cs:11-12 |
| 12 | Wire format | — | no message/field change, no ProtocolVersion bump (config is host-local; every peer already seq-gates cadence-agnostic snapshots) | NetMsg.cs / ProtocolVersion.cs unchanged |

## Design

- **Provider-level logging filter, factory stays Trace.** `SetMinimumLevel` is
  build-time; the configurable minimum must be able to move live. The
  providers therefore enforce `MinimumLevel`, and the logging factory never
  filters above Trace.
- **One cadence knob, two state streams.** Player entity and enemy streams are
  the same transport pattern (unreliable, seq-gated, next-tick-overwrites) and
  both were 20 Hz; `StateStreamHz` drives both. The 1 Hz character snapshot is
  explicitly excluded.
- **Range is clamped twice, deliberately.** BepInEx `AcceptableValueRange`
  clamps the config file at parse time; `StateStreamOptions` clamps
  programmatic values too, so every consumer reads one safe 1-60 Hz number.
- **Attack-hold follows cadence.** The original 300 ms window was six 20 Hz
  ticks. At slower cadences the flag is held for six configured ticks
  (never below the 300 ms clip) so the one-shot `ArmsSwing` replay survives
  the same drop profile; at faster cadences the clip span remains the floor.
- **Plugin owns declarations, Runtime owns snapshots.** The plugin is the only
  layer that knows the BepInEx `Config` instance and binds the entries; the
  Runtime owns the options types and the monitor mechanics.

## Verification design

1. L0 bridge: `BepInExOptionsMonitorTests` — `ConfigEntry.Value` change
   re-reads + notifies; an unwatched entry change does not.
2. L0 monitor contract: `MutableOptionsMonitorTests` — set + notify, disposed
   listener stops.
3. L0 normalization: `StateStreamOptionsTests` — default 20 Hz / 50 ms,
   1-60 clamp band, interval follows clamped Hz.
4. L0 sinks: `LoggingOptionsTests` — default Information suppresses Debug in
   the file sink, hot change to Debug writes it, BepInEx sink follows the
   same minimum.
5. L0 DI replacement: `LoggingOptionsTests.Bootstrap_ExtraRegistrations_...`
   proves the plugin's replacement path reaches a DI-resolved provider.
6. L0 cadence simulation: `StateStreamFrequencyTests` runs the production
   EntitySync + EnemySync pumps over the fake network at 20/10/5 Hz and
   counts the actual `PlayerState`/`EnemyState` frames in a 1 s window.
7. L0 swing hold: `AttackSwingStateTests` — slow cadence extends the hold,
   fast cadence never shrinks below the visible clip.
8. Runtime evidence (no manual acceptance): deployment smoke is limited to
   plugin load + config generation; the cadence/level behavior is covered by
   the simulations above. Logs will show the two `Config.Bind` entries in
   `BepInEx/config/CasualtiesUnknownOnline.cfg`.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Config declarations | two entries, range + defensive parse | Plugin.cs:87-105,315-319 |
| Bridge hot reload | watched definition → re-read + notify | BepInExOptionsMonitor.cs:81-100; BepInExOptionsMonitorTests |
| Default DI monitor | mutable in-memory default, replaced later | CuoBootstrap.cs:51-56; MutableOptionsMonitor.cs |
| Log providers | live provider-level minimum | BepInExLoggerProvider.cs:80-81; RollingFileLoggerProvider.cs:187-190 |
| Player stream | interval from options each pump | EntitySyncService.cs:340-351 |
| Enemy stream | interval from options each pump | EnemySyncService.cs:253-260 |
| Swing presentation | six-tick hold at configured cadence | AttackSwingState.cs:33-48; EntitySyncService.cs:294-298 |
| Character snapshot | excluded from cadence knob | StateStreamOptions.cs:11-12 |
| Wire compatibility | no protocol change | NetMsg.cs / ProtocolVersion.cs unchanged |
| Structure | all touched classes under 600-line gate | tools/check-architecture.ps1 |
| Test suite | 839 green (21 new) | dotnet test |
