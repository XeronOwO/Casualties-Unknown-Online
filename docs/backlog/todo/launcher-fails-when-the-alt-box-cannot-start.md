# The launcher reports failure when the alt box cannot start, and takes the run with it

- Status: Todo — opened 2026-10-26 from the batch that hit it: the host and the Steam1 guest came up and answered on their ports, the Steam2 alt box did not, and `launch-clients.ps1` exited non-zero, so the launcher read as "no session" when two usable clients were running.
- Priority: Low — the workaround is one command per client, and the run itself is unaffected once the clients are up.
- Category: Acceptance tooling / launcher
- Source: user report (2026-10-26, translated): "the script has a problem: Steam2's Steam starts slowly, the game started and exited several times, and then it just gave up".

## Design

No corpus page governs the launcher itself: it is the tool that makes the surfaces under `docs/design/surfaces/` judgeable on the machine, which is what an acceptance batch does row by row. The contract this ticket changes is the launcher's own header (`tools/acceptance/launch-clients.ps1`), read together with `docs/acceptance/workflow.md`.

The launcher's contract is in its own header (`tools/acceptance/launch-clients.ps1`): a client whose driver endpoint already answers is REUSED, Steam is only started when a box has none, and the exit codes name which client failed (6 host, 7 guest, 8 alt, 9 the world never had all three). Nothing in that contract says the ALT is optional, and nothing says a partial success is a session — so today a slow alt box turns a working host+guest pair into "failed".

## The fix

Two rules, both about what the caller needs:

1. **The alt box is optional.** A run that asks for two clients (the default for most acceptance work) must not fail because a third box is slow: the launcher starts the boxes it was asked for, and "alt missing" is a warning plus a distinct exit code (or a switch that says "two clients only"), not the same failure as "the host never answered".
2. **A client that keeps starting and exiting is named as such.** The game refuses to run twice and a box whose Steam is not ready exits immediately; the launcher should count those restarts and report "the alt box's Steam2 was not usable after N s" instead of surfacing only the final aggregate exit.

Rows below are judged from the launcher's own log lines plus one run of each shape (two clients, three clients, alt box down).

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | A two-client run on this machine succeeds and leaves the host + guest pair in one world, with the alt absent and said so in one line | not run |
| 2 | A three-client run still succeeds when the alt box is healthy, unchanged from today | not run |
| 3 | With the alt box's Steam deliberately not started, the launcher reports "the alt box was not usable" and exits with a code that is NOT the one for "the host never answered" | not run |

## Limits

- The launcher is tooling, not shipping code: its rows are judged from its own output and the clients' log lines, never from a frame.
- Which boxes exist is a machine fact (`docs/acceptance/AGENTS.local.md`); this ticket does not name them.

## Rebuilt 2026-10-26 after the user's second report

The first shape of this ticket ("make the alt optional") was wrong, and the user said why: **all three start together** (the launcher has no reason to
be a two-client tool), and the real faults are in the launcher's own logic:

1. **It started sequentially.** Host, wait up to 300 s, guest, wait, alt, wait — the launch cost the SUM of the three, so one slow box made the whole
   run look dead. All three now start in one pass (`Start-ClientProcess` per client, no waiting inside it: Steam start, game start, return).
2. **Every readiness poll spawned a powershell.** `Test-Endpoint` ran `drive-in-process.ps1 -Action ping` through a new `powershell` process, every
   three seconds, per client. Readiness is now a **TCP connect** to the driver port (`Test-Port`, a 400 ms budget), and the plug-in is confirmed with
   ONE `ping` per client afterwards.
3. **Waiting was one loop, not three.** `Wait-Endpoints` polls every listed client in a single 250 ms loop against one deadline
   (`-ReadyTimeoutSeconds`, default 300), so the slowest box decides the launch and the others are reported as they answer.
4. **Sleeps that were graces are now poll intervals.** The 5 s Steam poll, the 10 s restart pauses and the 3 s join poll are 250-500 ms polls; there is
   no fixed grace left in the path (the only remaining `SteamGraceSeconds` use is the "steam did not settle" report).
5. **A box whose game died before answering gets ONE restart**, decided in the wait loop (`Restart-BoxGame`), instead of the old fixed-look retries.
6. **`SbieSvc` is checked before any box starts.** After a machine restart the service is stopped; an unelevated `Start.exe` cannot start it, so the
   sandbox initialisation failed and the processes inside it errored (user's report: "no UAC prompt, the sandbox errored, the processes inside spat
   errors"). `Ensure-SandboxService` starts it once with `-Verb RunAs` (the one UAC prompt), waits for `Running`, and never elevates again.

## Judged (2026-10-26, `.acceptance/batch-launcher-fix2`)

With the host and the Steam1 guest already up and the Steam2 alt down:

```
19:04:39 host already running (reused)
19:04:40 guest already running (reused)
19:04:40 alt started
19:04:40 host endpoint up
19:04:40 guest endpoint up
19:04:49 alt endpoint up
19:04:54 all three up
```

**16 seconds end to end, exit 0** — reuse of two clients, a cold alt start, and the three endpoints, with no grace periods. The old path could not do this
in under a minute even when everything was healthy, and one slow box turned it into a failure.

| Row | The check | State |
|---|---|---|
| 1 | All three clients start together and answer within one deadline, and the launch is reported as each one answers | **judged** by the 16 s run above |
| 2 | A readiness poll costs no process spawn (the old path spawned a powershell per poll) | **judged by construction**: `Test-Port` is a socket connect; the only spawn is one `ping` per client after its port answers |
| 3 | After a machine restart, the launcher starts `SbieSvc` elevated once and proceeds | **owed**: needs a reboot to exercise, and it is exactly the path the user hit |
| 4 | The join half of the launcher (create lobby, join two, start the run, all three in world) still works | **owed**: this batch ran `-NoJoin`, because the host was already in a world from the earlier session |