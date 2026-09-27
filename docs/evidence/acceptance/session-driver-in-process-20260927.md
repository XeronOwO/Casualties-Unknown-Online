# Acceptance record — In-process session driver for the Online UI

- Ticket: `session-driver-in-process` — verdict: moved to `done/`
- Batch: `20260927-c` (first two-client session) — tickets `session-driver-in-process`; scope: `20260927-c-scope.md`
- Commit: `97173282f27cd365a09cdde3fbf6c214d1072a57` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+97173282f27cd365a09cdde3fbf6c214d1072a57` (`deploy.log`: "Deployment matches this tree's build output")
- Run: 2026-09-27 19:22 → 19:31 (+08:00) · Host: physical machine, launched through Steam (`steam://rungameid/4576510`) · Guest: Sandboxie sandbox with its own Steam instance
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `dotnet`, `capture`, `input`, `logs`, `artifacts` — preflight `10 present`, `RESULT: OK`, exit `0`
- Artifacts: JSON probe results (`c-*.json`), window frames (`c-*-window.png`, `c-*-world-view.png`) and log excerpts (`c-*-log-evidence.txt`) in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `preflight.ps1` reports the `input` row `present`, and the row decides no exit code | machine | pass | preflight output (`10 present`, `RESULT: OK - a full two-client run is possible`), exit `0` |
| 2 | `drive-in-process.ps1 -ListActions` with no client names the closed vocabulary, exit `0` | machine | pass | `list-actions.txt` (the nine verbs and their parameters) |
| 3 | `-Action state` against a running client | machine | pass | `c-state-host-before.json`, `c-state-guest-before.json` (window, page, role, lobby, transport, control ids) |
| 4 | `-Action create-lobby` on the host applies `home.create_lobby` | machine | pass | `c-create-lobby-host.json` (lobby `109775243068709933`, role `Host`, `active: true`), host log `Requesting lobby creation...` → `Lobby created: 109775243068709933` → `Session role: Host` |
| 5 | `-Action join-lobby <id>` on the guest sets `home.lobby_id`, then applies `home.join` | machine | pass | `c-join-lobby-guest.json` (role `Guest`, lobby `109775243068709933`, host id `…46659`), guest log `Requesting join of lobby 109775243068709933...` → `Entered lobby` → `Session role: Guest`, host state `Waiting for 1 player(s) to load…` |
| 6 | `-Action start-run` on the host calls the game's own `PreRunScript.StartRun` through `GuestMenuGuard`; the world starts generating | machine | pass | `c-start-run-host.json` (host `inWorld: true`), `c-state-guest-inworld.json` (guest `inWorld: true`), host log `Host entered the world …` / `Scene state: InWorld (SampleScene)` / trap scan and item publish, `c-host-world-view.png` + `c-guest-world-view.png` (both clients render the same world and each other's name tag) |
| 7 | Every action is in-process only; a setup that cannot be reached exits non-zero with the reason and the last offered control ids | machine | pass | this run drove `ping`/`state`/`create-lobby`/`click`/`join-lobby`/`start-run`/`quit` through the evaluator channel only; the C# 7 / no-OS-input gate passed in this run (3/3) |
| 8 | The fake-server contract suite and the C# 7 / no-OS-input gate are green | machine | pass | this run: `DriverToolTests` 20/20 and `AcceptanceDriverGateTests` 3/3, exit `0` (`focused-driver-tests.log`) |

## Limits

- One session with one host and one guest; the run proves these actions, not the absence of a rare race.
- Rows 1–4 were also exercised by the previous cycle's host-only smoke; this record judges them from this run's own outputs.
- The guest ran with its own Steam instance inside the sandbox; that machine detail lives in the local acceptance facts, not here.
