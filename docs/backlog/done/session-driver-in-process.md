# In-process session driver for the Online UI

- Status: Done
- Priority: High
- Category: Acceptance tooling / session automation
- Source: Handoff after batch `20260927-b` (2026-09-27): 106 of the 113 `review/` tickets need a real two-client session, and the run has no committed way to reproduce a setup without a person at the keyboard. The boundary is already settled — the agent may drive a client from inside its own process through the HotRepl evaluator and must never take over OS-level keyboard or mouse.
- Related: `done/agent-acceptance-workflow-foundation.md` (Stages 2–3), `docs/acceptance/workflow.md`, `docs/acceptance/dependencies.md`, `docs/acceptance/lessons.md` ("In-process control is the driver this machine allows"), `tools/acceptance/preflight.ps1`
- Acceptance record: `docs/evidence/acceptance/session-driver-in-process-20260927.md` (batch `20260927-c`: rows 1–8 `pass`; `join-lobby` and `start-run` judged live)

## Problem

An acceptance run can build, deploy, launch and observe, but it cannot put a scenario into the state its
rows need. The old probe called `SteamService.CreateLobby()` and `SteamService.JoinLobby(id)` directly,
which bypasses every policy the Online UI owns; the one attempt to click a native scene object shut the
client down, because a title-screen object's handler cannot be identified from its name. The `input` row
in `docs/acceptance/dependencies.md` is therefore declared and reported `pending`, and every review
ticket that needs a driven setup stays undecidable.

## What done looks like

1. `tools/acceptance/drive-in-process.ps1` — committed, parameterized and free of machine facts: it
   reads the endpoint from its caller, speaks the HotRepl `eval` protocol, and carries one closed
   vocabulary (`ping`, `state`, `open-window`, `goto-page`, `click`, `set-text`, `create-lobby`,
   `join-lobby`, `start-run`, `quit`) whose member is named in its usage, its failure text and its
   tests.
2. `tools/acceptance/driver/InProcessDriver.cs` — the in-process half: it finds the live CUO plugin and
   enters the Online UI at the frame's registered action table, handing the real control ids
   (`home.create_lobby`, `home.lobby_id`, `home.join`, `tab.*`) the same `OnlineUiIntent` payloads the
   native view emits, so `LobbySwitchActions` and the native run-start gate do the deciding. No
   production test seam, no scene-object click, no OS-level input.
3. The driver sequences across eval round trips — HotRepl evaluates at most one snippet per frame, on
   the main thread — with bounded retries, so a page or frame switch is waited out instead of raced,
   and the failure names the control that was never offered.
4. `tools/acceptance/preflight.ps1` reports `input` as `present` when the helper resolves, and
   `dependencies.md` / `workflow.md` say what the capability reaches and what it does not.
5. A black-box suite drives the script against a fake HotRepl WebSocket server (frame shape,
   vocabulary, retry, timeout and exit codes), and a gate keeps the template parseable as C# 7 with no
   OS-input API anywhere under `tools/acceptance/`.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | `tools/acceptance/preflight.ps1` with the helper committed | The `input` row reads `present`, and the row still decides no exit code |
| 2 | `drive-in-process.ps1 -ListActions` with no client | The closed vocabulary and its parameters, exit 0 |
| 3 | `-Action state` against a running client | One eval frame; the client's lobby/role/session/world/window/page/control snapshot |
| 4 | `-Action create-lobby` on the host | `home.create_lobby` applied; the client reports a non-zero lobby id |
| 5 | `-Action join-lobby <id>` on the guest | `home.lobby_id` set, then `home.join` applied; role `Guest` in that lobby |
| 6 | `-Action start-run` on the host | The game's own `PreRunScript.StartRun` entry is called through `GuestMenuGuard`; the world starts generating |
| 7 | Every action | Never OS-level keyboard or mouse; a setup that cannot be reached exits non-zero with the reason and the last offered control ids |
| 8 | The test suite | Fake-server contract tests and the C# 7 / no-OS-input gate are green |

## What landed

- **`tools/acceptance/drive-in-process.ps1`** — the committed driver: one eval per step over the
  HotRepl websocket, a closed vocabulary (`ping`, `state`, `open-window`, `goto-page`, `click`,
  `set-text`, `create-lobby`, `join-lobby`, `start-run`, `quit`; `-ListActions` prints it), bounded
  retries across round trips, and exit codes `0` ok / `1` client refusal or unreached setup / `2`
  transport / `3` timeout / `64` usage. It carries no machine value: the endpoint comes from its caller,
  and the run reads `hotrepl-host-url` / `hotrepl-guest-url` from `AGENTS.local.md`.
- **`tools/acceptance/driver/InProcessDriver.cs`** — the in-process half the script sends. It finds the
  live CUO plugin (BepInEx's plugin table, a scene scan as the fallback), enters the Online UI at the
  frame's registered action table, and hands the real control ids the same `OnlineUiIntent` payloads the
  native view emits: `create-lobby` clicks `home.create_lobby`, `join-lobby` edits `home.lobby_id` and
  clicks `home.join`, and `start-run` calls the game's own `PreRunScript.StartRun` entry so
  `GuestMenuGuard` decides. Every helper is capture-free, because Mono.CSharp's REPL cannot emit a
  closure for a lambda nested in another lambda, and an outer local whose name matches an inner
  lambda's parameter silently turns the whole submission void (both limits were found live; both are
  recorded in `docs/acceptance/lessons.md`).
- **Live smoke (host only, physical install, deployed artifact `0.1.0+a23a43b1`; the drift to HEAD is
  comment-only)**: `ping` → `state` → `open-window` → `state` (the frame offered `home.create_lobby`,
  `home.join`, `home.lobby_id`, `tab.*`, `window.close`) → `create-lobby` (lobby id, role `Host`,
  offered set became the in-session controls) → `goto-page players` and back to Home → `quit`. The CUO
  log pairs every step (`Online UI modal open …`, `Requesting lobby creation…`, `Lobby created: …`,
  `Session role: Host (…)`, `Steam API shut down.`), the window frame was captured and read, and the
  client the run started was quit cleanly. Local artifacts live under `.acceptance/driver/` in the
  directory `AGENTS.local.md` names.
- **`preflight.ps1`** now reports `input` as `present` when the helper and its template resolve (the row
  still decides no exit code); `docs/acceptance/dependencies.md`, `docs/acceptance/workflow.md`, the
  acceptance index and `tools/AGENTS.md` say what the capability reaches.
- **Tests**: `DriverToolTests` (20 cases against a fake HotRepl websocket) pin the frame shape, the
  per-action vocabulary, the retry path and the failure/exit-code contract for every verb;
  `AcceptanceDriverGateTests` (3 cases) pin the template as C# 7-clean through a version-difference
  compile with a pinned unresolved-reference census, free of OS-input APIs (a capability-shaped ban),
  and acting only through the Online UI's own controls.
- **Independent adversarial review (fresh context, frozen tree)**: 0 blockers / 0 high. Its Medium and
  Low findings were folded into this same change: the exit-code taxonomy now separates transport (2)
  from driver/protocol failures (4) and maps the evaluator's own `evalTimeout` to 3; `ping` fails when
  the CUO surface is absent; `create-lobby` refuses a guest's lobby; `quit` no longer swallows a
  client's refusal; the placeholder substitution is single-pass (a text value may legally spell a
  placeholder); the C# 7 gate pins the shared-error census so an ordinary template error cannot hide
  behind the version comparison; the ban covers the input-actuation families; and the fake-server
  suite exercises every verb plus the malformed, truncated and timeout answers.

## Limits

- `join-lobby` and `start-run` are not exercised yet: the second client belongs to the session batch,
  and starting a world is a scenario, not a smoke. Their rows are judged by that batch — which is the
  state `review/` describes.
- The driver enters at the frame's registered action table (where a native click lands through
  `DrainSurfaceIntents`); the uGUI view listener and the intent queue below it stay pinned by the Online
  UI's own gates rather than re-exercised here.
- The fake-server cases prove the transport and the sequencing contract; only the live smoke proves the
  in-process template, and it ran on the host only.
- The driver's `state` snapshot is the run's machine evidence for its own steps; it is not a substitute
  for the behavioural evidence a ticket's rows need.

## Non-goals

- Not a general-purpose REPL: the vocabulary is closed and grows with a ticket; ad-hoc probes stay the
  acceptance run's own snippets.
- Not the rest of Stage 2: launching both clients, applying the shadow rule, frame capture and the log
  channels keep their own steps.
- Not OS-level input, under any circumstance.
