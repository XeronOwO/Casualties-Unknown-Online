# Acceptance record — An earthquake's direct Time.timeScale write ends a session acceleration

- Ticket: `world-acceleration-quake-direct-write` — verdict: **stays in `review/`** (rows 1, 2, 4 and 5
  pass this run; **row 3's scene-reload instance stays `unproven`** — its run-start instance was
  observed good and is named below)
- Batch: `20261001-u` — tickets `world-acceleration-survives-movement`, `world-time-local-initiation`,
  `world-acceleration-quake-direct-write`
- Commit: `8a0ddafb` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+8a0ddafb…` (`tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 18:31 → 18:46 · Host: physical machine · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `logs`, `artifacts`, `capture`,
  `input` (preflight: 11 present, exit 0)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir` (`batch-u`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | An earthquake during a standing acceleration ends it on every screen, and the HUD follows the actual clock | machine + visual | **pass** | the world produced the quake itself while an acceleration stood: host log `[Earthquake] host quake started (19.4s, next in 1149s) — broadcasting.` at 18:34:34.010, then `host direct timeScale write 1 adopted as Normal.` 11 ms later; `u-f3`/`u-f4` read both ends at 1× `Normal` with the quake running (`earthquakeTime` 11.9/8.8, intensity 0.76/1.0), so the HUD's own field follows; the guest log carries `[Earthquake] guest: host quake (19.4s) — showing effect, timer re-aligned (1149s).` A second quake (10.6 s) was observed during the sleep scenario at 18:39:16 with the same adoption line |
| 2 | The console's direct write keeps its meaning | machine | **pass** | `u-n5-host-direct-20` writes 20 directly; host log `host direct timeScale write 20 adopted as SuperFast.` and `u-n6`/`u-n7` read 20× `SuperFast` on both ends. The negative sample `u-o2-host-direct-7` (a value outside the domain) is **not** adopted — `u-o3` reads the host at 7 with `curTimeScale` still Fast, and `u-o4` shows the guest still on the session's value |
| 3 | The scene-reload and run-start writes keep their meaning | machine | **unproven** (scene-reload instance) | the run-start instance is observed good: the host's pump logs nothing before the world entry (the log census's first line is the start-gate swallow at 18:32:55.444) and the clock after the start is the game's own 1×; the `WorldGeneration.ReloadScene()` instance (layer-end scene reload) was not produced in this session — the run never ended a layer |
| 4 | The rejected alternative stays rejected: CUO never swallows the game's own write | machine | **pass** | both direct writes above moved the shared clock (1× adopted at 18:34:34, 20× adopted at 18:40:12) and were then carried to the guest (the guest's log burst ends `… stands at SuperFast.` and `u-n7` reads 20×) — nothing suppresses them |
| 5 | Build, tests, gates and format | machine | **pass** | this cycle's own ladder, captured in the batch directory: `u-build.log` (0 warnings / 0 errors), `u-format.log` (exit 0), `u-gate.log` (`AcceptanceDriverGateTests` 4/4), `u-full-suite.log` (the main suite and the normative gates, 0 failed) |

## What this run leaves open

Row 3's scene-reload instance needs a layer end (`WorldGeneration.ReloadScene()`) inside a live
session — a setup this run did not reach. The ticket stays in `review/` with that instance named; the
run-start instance and the census rule are recorded above.

## Limits

- The quake in row 1 was the world's own (its countdown reached zero during the acceleration); the
  committed `quake-force` recipe was not needed and is not claimed as evidence.
- Row 1's HUD half is judged from the game's own HUD field (`curTimeScale`) and the widget reads in the
  sibling record, not from a frame: the speed widget is not composited into the window-level capture.
- A single session cannot exclude a rare race; no row here claims to.
