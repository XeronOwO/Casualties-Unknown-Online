# Configuration keys

[Documentation](../README.md) > [Reference](README.md) > Configuration keys

---

**After this page** you can find the CUO settings file, know every key, its default and its legal
range, and tell which switches are [host rules](glossary.md) and which values are compiled-in policy.
The save keys are explained in [How a world is saved](../internals/save-archive.md); the log keys are
in [Logs](logs.md).

## Where the settings live

| What | Where |
|---|---|
| The settings file | `BepInEx/config/CasualtiesUnknownOnline.cfg` — BepInEx names it after the plugin GUID, `CasualtiesUnknownOnline` |
| Host-persistent mod state | `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` |
| The host's ban list | `BepInEx/config/CasualtiesUnknownOnline.host-bans.bin` |
| [Configuration profiles](glossary.md) | `BepInEx/config/CasualtiesUnknownOnline.Profiles/<name>.profile` |

- The plugin binds every entry at startup and saves the file, so a section added by an update (for
  example `[UI]`) appears in an existing install instead of staying invisible.
- The defaults, ranges and allowed values below are exact; the wording is this page's paraphrase of the
  description BepInEx writes next to the key in the generated file. The declarations themselves are in
  `src/CasualtiesUnknownOnline.Plugin/PluginDependencyRegistrar.cs` (`[Session] InteractionPanelKey` is
  bound in `Plugin.cs`).
- **[Hot reload](glossary.md)**: the runtime reads an options monitor at each decision, so editing the
  file takes effect without restarting the game. The online panel's admin page and the console's
  host-rule command write the same entries.
- BepInEx range annotations are the first clamp and the options object is the second: a hand-edited file
  bypasses the first, and neither "autosave every 0 minutes" nor "keep 0 archives" is a policy this
  system may execute, so the save options clamp again.
- A **configuration profile** is a named snapshot of every bound entry. Capturing writes one
  `.profile` file (schema version 1); applying sets each stored entry, counts what it applied and
  skipped, and reports the entries it could not set. A profile name is 1–64 characters and may not
  contain a path separator.

## `[Sync]`

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `StateStreamHz` | 20 | 1–60 | Player/enemy state snapshot frequency in Hz — higher is smoother and costs more bandwidth. |

## `[Logging]`

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `MinimumLevel` | `Information` | `Information`, `Trace`, `Debug`, `Warning`, `Error`, `Critical`, `None` | The minimum CUO level written to BepInEx and `latest.log`. `Information` keeps normal play quiet; `Debug` adds the high-frequency per-frame and per-event traces (clone inventory, character relay, block and sound events). |

## `[Respawn]`

Host-authoritative revive and respawn rules; they are read at decision time.

| Key | Default | What it does |
|---|---|---|
| `Permadeath` | `false` | True = death is terminal: no trader revive and no next-level auto-respawn. |
| `ReviveFromTrader` | `true` | True = a living player can revive a dead teammate at a friendly trader. |
| `ReviveOnNextLevel` | `true` | True = dead players are auto-respawned when the host finishes the next world layer. |
| `KeepInventory` | `true` | True = auto-respawn keeps the character's carried and worn items. |
| `KeepSkills` | `true` | True = auto-respawn keeps skills and experience; false resets them. |

## `[HostRules]`

The host-rule surface that is not already owned by `[Respawn]`. Read at decision time.

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `PvpEnabled` | `false` | | Reserved host-rule surface for a future PVP damage domain; no gameplay effect yet. |
| `AutoContinue` | `false` | | Reserved host-rule surface for automatic next-layer continuation; not wired yet. |
| `AllowLateJoin` | `true` | | True = a brand-new player may join the host's already-running world. |
| `AllowRemoteInventoryTake` | `true` | | True = other players may take carried items from a remote player's inventory (unconscious/dead loot remains the default rule; false disables cross-player inventory take entirely). |
| `WidenRunSettings` | `true` | | Host-only: widen the native custom run-settings sliders in co-op so the run can be tuned for the actual lobby size. Values still ride the existing world-start params. |
| `PiggybackWeightMultiplier` | `0.8` | 0.0–3.0 | Host-only: the fraction of a carried rider's full encumbrance added to the carrier while a carry relation is active. `0` disables the movement penalty. |
| `NativeBindingParity` | `warn` | `allow`, `warn`, `require` | Host-only: how a member's declared [native binding](glossary.md) is judged when it differs from the host's own for a mod both sides list. `allow` = no check; `warn` = admit and record the mismatch in the host's log; `require` = refuse the member. |

## `[Save]`

World-archive policy. The host owns it, and the runtime reads it at each decision.

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `AutosaveEnabled` | `true` | | Host-only: write an interval autosave while a world is being played. Off leaves the player's `/save`, the layer-end cut and the menu-return cut in place. |
| `AutosaveIntervalMinutes` | 10 | 1–1440 | Host-only: minutes between two interval autosaves (the `auto-*.cuoz` archives). The interval restarts on every committed cut, whatever triggered it. |
| `BackupRetentionCount` | 10 | 1–1000 | Host-only: how many backup archives one world keeps. The oldest are pruned after each committed cut; the newest archive is never pruned. |

## `[UI]`

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `Language` | `en` | `en`, `zh` | CUO interface language. The localization service normalizes anything starting with `zh` to Chinese and everything else to English. |
| `PlayerColorIndex` | -1 | -1–7 | Player marker colour. `-1` = an automatic per-SteamId palette; `0`–`7` = one of the shared player palette colours. It is a local preference shared through the handshake and roster messages. |

## `[IpDirect]`

The non-Steam connection path. Ports are validated by BepInEx's range.

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `ListenPort` | 7777 | 1–65535 | TCP port the IP-direct host listens on. |
| `JoinAddress` | `127.0.0.1` | | IP address or hostname of an IP-direct host to join. |
| `JoinPort` | 7777 | 1–65535 | TCP port of an IP-direct host to join. |
| `DisplayName` | *(empty)* | | Custom in-game display name for IP-direct sessions; empty means `player-<id>`. Steam sessions keep the Steam persona name. |

## `[Diagnostics]`

Opt-in hot-path instrumentation. Default off: it must not affect normal play, and when enabled it adds
only a stopwatch per measured domain call plus a one-line-per-name summary at the log interval.

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `LatencyInstrumentation` | `false` | | True = collect and log per-domain CUO update-pump timing. |
| `LatencyLogIntervalSeconds` | 1.0 | 0.1–60.0 | Seconds between aggregated hot-path latency log lines. |
| `SlowFrameThresholdMs` | 25.0 | 0.0–1000.0 | A whole frame whose total GameAdapter update time reaches this many milliseconds counts as a slow/frame-drop sample. |

## `[Session]`

| Key | Default | What it does |
|---|---|---|
| `InteractionPanelKey` | `F6` | Hotkey that toggles the standalone player-interaction quick panel. Any `UnityEngine.KeyCode` name is accepted. |

## What is not a configuration key

These values look tunable and are not: they are policy constants in code, and changing one is a code
change (and for the payload cap, a protocol-adjacent decision).

| Constant | Value | Source |
|---|---|---|
| `ModChannel.MaxPayloadBytes` | 64 KiB | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModChannel.cs` |
| `ModRateLimitPolicy` mod-message rate | 20/s sustained, burst 40 | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRateLimitPolicy.cs` |
| `ModCommandPolicy` | name ≤64, ≤16 arguments, each ≤256 characters, total ≤4 KiB, output ≤32 KiB, error ≤4 KiB, request timeout 10 s, ≤32 pending requests | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModCommandPolicy.cs` |
| `ModStatePolicy` / `ModDataPolicy` | key ≤128 characters, ≤1024 entries, value ≤64 KiB | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/` |
| `ContentId` path length | ≤95 characters | `src/CasualtiesUnknownOnline.Abstractions/ContentId.cs` |
| `WorldLease.StaleAfter` | 30 minutes | `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldLease.cs` |
| `ResourceLocationCatalog.MaxSuggestions` | 20 | `src/CasualtiesUnknownOnline.Runtime/Session/Content/ResourceLocationCatalog.cs` |

## Related reading

- [Logs](logs.md) — the `[Logging]` and `[Diagnostics]` keys in practice
- [The mod API contract](mod-api.md) — the policy constants above as a mod author meets them
- [How a world is saved](../internals/save-archive.md) — cuts, backups and what the save keys control
- [Build, test and deploy the plugin](../contributing/build-and-test.md) — deploying the plugin that reads this file
- [Glossary](glossary.md) — host rules, hot reload, configuration profile, native binding

---

[Documentation](../README.md) > [Reference](README.md) > Configuration keys
