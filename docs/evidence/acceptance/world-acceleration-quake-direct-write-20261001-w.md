# Acceptance record — An earthquake's direct Time.timeScale write ends a session acceleration

- Ticket: `world-acceleration-quake-direct-write` — verdict: **moved to `done/`** (row 3's scene-reload
  instance passes this run; its run-start instance and rows 1, 2, 4 and 5 were judged in batch
  `20261001-u`)
- Batch: `20261001-w` — ticket `world-acceleration-quake-direct-write`
- Commit: `a78a59bd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a78a59bd…` (`tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 20:21 → 20:24 · Host: physical machine · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `logs`, `artifacts`, `input`
  (preflight: 11 present, exit 0)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir` (`batch-w`)

## The capability this run adds

- `tools/acceptance/recipes/scene-reload.cs` (landed `a78a59bd`): starts the game's own private
  `WorldGeneration.ReloadScene()` coroutine through the evaluator. The site has no caller in the
  decompiled assembly and no reference in the shipped `CasualtiesUnknown_Data\*` files, so the trigger
  is forced and declared; the method lookup precedes the world check, so the lobby invocation returns
  `no-live-world` without reloading anything (the smoke, `w-host-smoke-no-world.json`).

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 3 | The scene-reload and run-start writes keep their meaning — scene-reload instance | machine | **pass** | one host + guest run; the host's standing acceleration reads 5×/Fast on both ends (`w-host-timescale-after.json`, `w-guest-timescale-after.json`); the guest leaves the world (`w-guest-leave-world.json`, `inWorld` false) and the host re-arms the production gate (`w-host-gate-arm.json`: `armed` true, `startGateActive` true, `remainingMs` 30000) with the clock frozen at the gate (`w-host-timescale-at-gate.json`: `timeScale` 0, `curTimeScale` Fast); `w-host-scene-reload.json` records the game's own coroutine started (`ok` true, `scene` SampleScene) and the reload runs: host `Scene state: InMenu (PreGen)` 20:23:26.115 → reloaded SampleScene `InWorld` 20:23:36.245 (`w-host-latest.log`). Inside the gate window the pump logged no `host direct timeScale write … adopted` line at all, and the gate's rule is independently visible in the same window on the ROUTED path — a `PlayerCamera.SetTimeScale` reset, which is a different writer from the direct field write the pump's own rule reads — swallowed without a write: `[WorldTime] silent Normal reset swallowed — the start gate owns the world clock.` at 20:23:26.353; the gate re-arms at the reloaded world entry 20:23:36.244. It releases only when the guest rejoins (20:23:51.250 `Start gate released — everyone is in the world.`); 6 ms later the ordinary value-blind rule adopts the world's standing 1× (`[WorldTime] host direct timeScale write 1 adopted as Normal.` 20:23:51.256), and both ends end on 1×/`Normal` (`w-host-timescale-final.json`, `w-guest-timescale-final.json`): the native write is never suppressed and never fought. Guest side: `w-guest-latest.log` carries the same gate-window swallow at 20:23:37.661 and the adoption broadcast at 20:23:51.869 (`World-time broadcast: Normal.`) |
| — | Ladder — this run's build, the focused gate, the normative gates, the full suite and `dotnet format` | machine | **pass** | `AcceptanceDriverGateTests` 4/4 (`w-focus-gate.log`); the gate project 299/299 with the mid-cycle checklist gate excluded (`w-gates-final.log`, exit 0); the full suite with build 4,573 + 299, 0 failed (`w-full-suite-final.log`); `dotnet format` exit 0 (`w-format-final.log`); preflight 11 present, exit 0 (`w-preflight-final.txt`) and the deployed artifact re-verified after the run as `0.1.0+a78a59bd…` (`w-verify-deploy.log`, exit 0) |

## What this run leaves open

Nothing on this ticket. The reachability finding below is part of row 3's judgement, not a gap.

## Reachability of the write site (row 3's static half)

- `WorldGeneration.ReloadScene` has no caller in the decompiled assembly: a full scan of
  `reversing/Assembly-CSharp/Assembly-CSharp` reports exactly one occurrence, the definition
  (WorldGeneration.cs:1033).
- The caller census is the load-bearing half; as corroboration, a string scan of the shipped Unity scene
  and asset files finds no reference either — `findstr /m /c:"ReloadScene"` over the top-level
  `CasualtiesUnknown_Data\*` scene/asset files (level0, level1, globalgamemanagers,
  globalgamemanagers.assets, resources.assets, sharedassets0.assets, sharedassets1.assets) returns no
  match. The scan is narrow on purpose (that folder's `.resource`/`.resS` containers and `Managed/` are
  not scanned, and serialized asset data would not normally carry a private method name at all): what
  the reachability claim rests on is the census above, not this scan.
- The instance is therefore produced by the committed forced recipe, and the record declares it.

## Limits

- The trigger is forced (the game's own coroutine started by the evaluator), because no player path
  reaches the site; the reachability census is the evidence for that claim, and the run cannot claim a
  player reached it.
- The gate hold is a production re-arm after a member deliberately left the world, not a slow loader's
  natural window (batch v's declared limit).
- The world's standing 1× adopted at the gate release is the same value the gate's own release branch
  writes, so the run cannot attribute the post-release value to the reload's write alone; what it does
  prove is that no CUO line touched the clock inside the gate window and the write was never suppressed.
- One session is not a race proof, and the absence of a rare interleaving is not claimed.
- The per-invocation JSON artifacts carry no internal timestamps, so their binding to the timeline rests
  on run order plus the clients' own log timestamps — `w-run-log.md` records that order; the deployed
  identity and the preflight are backed by `w-verify-deploy.log` and `w-preflight-final.txt`.

## Residuals for the user

None.
