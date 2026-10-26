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
