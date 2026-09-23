# Logs

[Documentation](../README.md) > [Reference](README.md) > Logs

---

**After this page** you know which file to open first, what a CUO line looks like, which setting raises
the detail, and the strings worth searching for. The commands that produce a deployment and run the
tests are in [Build, test and deploy the plugin](../contributing/build-and-test.md); what CUO logs at
each step is [How a world is saved](../internals/save-archive.md) and
[The life of a mod](../internals/mod-loading-lifecycle.md).

## The three channels

| File | Written by | What it answers |
|---|---|---|
| `BepInEx/LogOutput.log` | BepInEx itself: the chainloader, plugin load, and every CUO line that passes through the BepInEx sink | did the plugin load at all, and what happened before the CUO log existed |
| `BepInEx/logs/latest.log` | CUO's own rolling file log — the channel the `[Logging]` setting controls | what the session did: start-up, session and world events, mod discovery and handshake, warnings and errors |
| `BepInEx/CUO.log` | nothing today | the pre-rollover single-file log. If it exists it is compressed into `BepInEx/logs/` once at the next start-up and deleted; it is history, not a live channel |

- `latest.log` is appended to during a session (`FileShare.Read`, so an external tailer can follow it),
  and at the next start-up a non-empty previous file is compressed to
  `BepInEx/logs/<yyyy-MM-dd>-<n>.log.gz` — the archive name is the first free number for that date.
- A failed compression leaves `latest.log` in place and the session appends to it, so a crash tail is
  never lost to a failed rotation.
- Logging never throws: a locked or unwritable directory disables the file sink instead of breaking the
  game.

## The line shape

`BepInEx/logs/latest.log` writes one line per entry:

```text
[2026-09-23 14:05:31.482] [INF] [CasualtiesUnknownOnline.Plugin] Plugin CasualtiesUnknownOnline is loaded!
```

- The level codes are `TRC`, `DBG`, `INF`, `WRN`, `ERR` and `CRT`.
- An exception is appended below the message, with its stack trace — the formatter used here does not
  include the exception on the message line itself.
- The third field is the logger's category, normally the class that logged the line, so a search for a
  class name narrows a file quickly.
- Unity's own errors are forwarded into this log: `Error`, `Exception` and `Assert` arrive at error
  level with a `[Unity:<Type>]` marker, and Unity warnings arrive at warning level with `[Unity]`.
  `[ERR]` lines carrying `[Unity:Exception]` are runtime exceptions raised on the Unity side.
- A mod's own lines carry its id: `[Mod:<id>] …`.
- Mod discovery is one line per mod — quoted from
  `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRegistry.cs`:

```text
[Mods] discovered {Id} {Version} ({Mode}, permissions {Permissions}, namespace {Namespace}, binds {NativeBinding}) — {DisplayName}.
```

## What raises the detail

`[Logging] MinimumLevel` (default `Information`) sets the minimum level written to both sinks, and the
providers enforce it, so a change applies immediately without a restart. `Debug` adds the high-frequency
per-frame and per-event traces (clone inventory, character relay, block and sound events); `None`
silences the CUO sinks entirely. The keys and their ranges are in [Configuration keys](configuration.md).

Pacing questions are answered by the opt-in `[Diagnostics]` instrumentation, which is off by default:
`LatencyInstrumentation` collects per-domain update-pump timing, `LatencyLogIntervalSeconds` sets how
often the aggregate line is written and `SlowFrameThresholdMs` decides what counts as a slow frame.

## What to search for

| Symptom | Channel | Search |
|---|---|---|
| The plugin never loaded | `LogOutput.log` | the plugin GUID line, and the chainloader's own error |
| A Unity-side crash | `latest.log` | `[ERR]` lines carrying `[Unity:Exception]` |
| A mod did not load, or loaded at the wrong version | `latest.log` | `[Mods] discovered` and the handshake verdict line |
| A specific mod's behaviour | `latest.log` | `[Mod:` plus the mod id |
| Frame pacing under load | `latest.log` | the `[Diagnostics]` latency summary lines |
| A world would not restore | `latest.log` | the archive and lease warnings written while the world opens |
| A previous session for comparison | `BepInEx/logs/*.log.gz` | decompress the dated archive next to `latest.log` |

## Triage order

1. **`LogOutput.log`** — did the plugin load, and did the composition root fail before the CUO log
   existed? A start-up exception also gets appended to `latest.log` directly so the CUO log is not
   empty in that case.
2. **`latest.log`** — read the first `[ERR]` (not the last): the later lines are usually its
   consequences. Warnings name what CUO skipped and why it kept running.
3. **The dated archives** — a symptom that appeared after an update is fastest to localise by comparing
   the previous session's file with the current one.

Green tests and a clean log are development evidence: they prove the logic and the path that ran, never
that a two-client session looks right on screen. That check is the user's acceptance run.

## Related reading

- [Configuration keys](configuration.md) — the `[Logging]` and `[Diagnostics]` settings behind these files
- [Build, test and deploy the plugin](../contributing/build-and-test.md) — the commands, the tiers and the deployment check
- [The life of a mod](../internals/mod-loading-lifecycle.md) — the lifecycle the discovery lines record
- [Review and delivery](../contributing/review-and-delivery.md) — what evidence a change carries before delivery
- [Glossary](glossary.md) — hot reload, evidence, gate

---

[Documentation](../README.md) > [Reference](README.md) > Logs
