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
  `/gamerule` command write the same entries.
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

Host-authoritative revive and respawn rules; they are read at decision time. Four of them —
`KeepInventory`, `ReviveFromTrader`, `ReviveOnNextLevel` and `Permadeath` — travel as host rules (see
`[HostRules]` below) because a member's Admin page shows that surface; `KeepSkills` stays a host-local
entry (it is not a host rule and is not announced).

| Key | Default | What it does |
|---|---|---|
| `Permadeath` | `false` | True = death is terminal: no trader revive and no next-level auto-respawn. |
| `ReviveFromTrader` | `true` | True = a living player can revive a dead teammate at a friendly trader. |
| `ReviveOnNextLevel` | `true` | True = dead players are auto-respawned when the host finishes the next world layer. |
| `KeepInventory` | `true` | True = auto-respawn keeps the character's carried and worn items. |
| `KeepSkills` | `true` | True = auto-respawn keeps skills and experience; false resets them. |

## `[HostRules]`

The host-rule surface that is not already owned by `[Respawn]`. Read at decision time. The console's
`/gamerule <rule name> [<value>]` reads or sets one host rule by name — the lowercase snake-case
name the rule carries (`remote_backpack_access`, `save_inventory`; an unknown name lists every rule) — and
omitting the value queries it. It is a cheat verb: the host uses it directly, and a member the host granted
(the cheat switch plus the `/op` roster) changes the HOST's entry through a host round trip — the member's
own config is never written, because the announced set would override it. The four `[Respawn]` rules that
travel as host rules are addressed the same way: `save_inventory`, `revive_from_trader`, `revive_on_next_level`
and `permadeath`.

**The values reach the members.** A member's client acts on them — the member list's take
affordance, its own console's item-data write, the encumbrance its body gains while it carries
someone, and the read-only Admin summary — so the host announces the whole set (absolute, one field
per rule the census marks member-known) to a member the moment its handshake completes, again with
the world-entry group and the 60 s in-session repair group, and on every change while the session
runs. The rules stay the host's: a member refuses locally only what the announced set forbids, and
the host checks the same thing behind it, so a stale, lost or forged belief is never the reason
something is allowed. A member that received no announcement reads its own entries and skips the
local check — the behaviour of a build without the announcement, never a guess at the host's.
The `Host-only` prefix below says the rule is the host's to set and enforce, NOT that its value
stays on the host: those are exactly the rows a member's client reads.

| Key | Default | Allowed | What it does |
|---|---|---|---|
| `PvpEnabled` | `false` | | Reserved host-rule surface for a future PVP damage domain; no gameplay effect yet. |
| `AutoContinue` | `false` | | Reserved host-rule surface for automatic next-layer continuation; not wired yet. |
| `AllowLateJoin` | `true` | | True = a brand-new player may join the host's already-running world. |
| `RemoteBackpackAccess` | `allow` | `allow`, `unconscious_or_dead`, `deny` | Host-only: who may open and operate another player's backpack. `allow` = any visible player (the cooperative rule the mod has always had). `unconscious_or_dead` = a conscious owner's backpack opens VIEW-ONLY — its items are visible, every operation (take, move, drop, use, give) is refused, and the game's own red slot state marks it — while an unconscious or dead owner can be operated on. `deny` = nobody; the view does not open and every intent is refused. Enforced on the host, read by a member's client for the member card's backpack and take buttons, and every refusal names it. |
| `AllowMemberCheatVerbs` | `false` | | Host-only: the cheat SWITCH — true = members may run the cheat verbs at all (`/data`'s write verbs, `/give`, `/summon`, the `/heal` reset and `/gamerule`). It is one of TWO host rulings: the member must ALSO be on this host's cheat roster, which `/op` and `/deop` edit. A granted member is refused while the switch is off, and the switch alone admits nobody; the host's own verbs ignore both (its standing is the online session). False = every member's cheat verb is refused by name; a member holding the announced value is refused on its own client BEFORE anything is applied, and one that received no announcement applies, reports and puts the value back when the host's refusal arrives. |
| `WidenRunSettings` | `true` | | Host-only: widen the native custom run-settings sliders in co-op so the run can be tuned for the actual lobby size. Values still ride the existing world-start params. |
| `PiggybackWeightMultiplier` | `0.8` | 0.0–3.0 | Host-only: the fraction of a carried rider's full encumbrance added to the carrier while a carry relation is active. `0` disables the movement penalty. Read by the carrying player's own client, so a member carrying someone uses the host's value. |
| `NativeBindingParity` | `warn` | `allow`, `warn`, `require` | Host-only: how a member's declared [native binding](glossary.md) is judged when it differs from the host's own for a mod both sides list. `allow` = no check; `warn` = admit and record the mismatch in the host's log; `require` = refuse the member. |
| `RadiationLineStopsAboveTheFloor` | `true` | `true`, `false` | Host-only: the radiation line stops a margin above the layer floor instead of descending past it, so the bottom of a layer stays livable and a group can share supplies and rest. `false` = the game's own uncapped descent. |
| `SpectatorFreeFly` | `true` | | Host-only: a spectating player may free-fly the camera instead of only following a teammate. On by default, aligned with the reference mod. |
| `SpectateWhileUnconscious` | `false` | | Host-only: an UNCONSCIOUS player may spectate. Off by default: an unconscious body is still its owner's to act on. |
| `SpectateWhileWaitingToJoin` | `false` | | Host-only: a client that has not entered the world yet may spectate. Off by default: that is the late-join case. |

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
| `PlayerColor` | *(empty)* | *(empty)*, `#RRGGBB`, `#RRGGBBAA` | Player marker colour. Empty = an automatic, stable per-SteamId palette colour; otherwise the colour itself, in the game's own hex idiom (`#RRGGBB`, or `#RRGGBBAA` for a translucent marker). It is a local preference shared through the handshake and roster messages, and a saved configuration profile carries it like every other entry. |

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
