# Agent acceptance workflow

The procedure this repository uses to accept a ticket that sits in `review/`. It replaces the
previous "wait for the user's unified acceptance pass": the agent runs the real game, judges every
acceptance row, writes a record, and moves the ticket. The rules that bind this procedure are in
[AGENTS.md](AGENTS.md); the dependency table is [dependencies.md](dependencies.md).

Read the two kinds of value from [AGENTS.local.md](AGENTS.local.md) (this directory, gitignored,
auto-loaded only while working here) and never write one into a committed file:

| Key | What it is |
|---|---|
| `game-dir` | the physical machine's game install |
| `steam-exe` / `game-app-id` | how the host client is launched |
| `sandboxie-exe`, `sandbox-guest-root` | the second client's environment |
| `sandbox-alt-root`, `sandbox-alt-box`, `hotrepl-alt-url` | the third client's environment and its probe endpoint (third-party rows) |
| `hotrepl-host-url`, `hotrepl-guest-url` | the two in-process probe endpoints |
| `acceptance-artifacts-dir` | where frames, recordings and probe dumps are written (never committed) |

## 1. Entry and scope

The unit of acceptance is a **batch**: the tickets waiting in `docs/backlog/review/` that the available
dependencies can serve, accepted in one run. Acceptance costs a build, a deploy, two game clients and
the setup of every scenario, so the run is planned across tickets — deploy once, launch once, work
through every ticket's scenarios in that session — and is never started for a single ticket while
another one waits on the same capability.

A batch can also serve a ticket that is not in `review/` at all: a ticket kept in `todo/` by its
remaining rows can still owe the machine reading of a row that landed and was tested in code. That row
is accepted by the same procedure and recorded on its own (`docs/evidence/acceptance/`), and the ticket
does not move — the rows that keep it in `todo/` decide its folder, never this reading.

Each ticket keeps its own rows: the tables headed `Goal and acceptance criteria`,
`Reproduction / acceptance matrix` or `Final-acceptance procedure`, plus every behavioural claim its
`What landed` section makes. When a ticket states its expectations only in prose, the run writes the
row table first and records it, so the verdict cannot be reverse-engineered from the result.

A batch is bounded by the world it needs: tickets whose scenarios need different worlds, saves or
player counts are split into separate runs, and that split is part of the plan. Runs are serialised —
only one two-client session exists on the machine at a time.

An acceptance run is not a development cycle: it does not reset and does not fill
`docs/evidence/delivery-checklist.md`, whose gate governs the development commit ("Deployment and
multiplayer acceptance are the agent's actions, run after the commit per `docs/acceptance/` and outside
this gate"), and it records that judgement in its own scope page (§7). What the run does owe the
checklist is a verdict on every row it claims, which is the same evidence the checklist's own step 9
names.

## 2. Dependency preflight — a gate, not a formality

```powershell
powershell -ExecutionPolicy Bypass -File tools/acceptance/preflight.ps1
```

The preflight is read-only. It reports, per dependency: present / missing / unknown, plus the local
facts it could not resolve. Its exit code is `0` when a full two-client run is possible, `2` when a
capability is missing and `1` on its own error.

- **Everything present** → continue.
- **Something missing** → the run does not degrade silently. Ask the user in plain language (what is
  missing, why acceptance needs it, the options: install it, point the agent at an existing copy, or
  leave this capability out), record the dependency on the ticket as an external blocker, mark every
  row that depends on it `blocked`, and carry on with the rows that do not. A missing capability is
  never replaced by a weaker check.
- **The install's BepInEx trees are not all the run's.** An install can carry CUO's tree and whatever the
  machine's owner keeps for their own play, and which one is ACTIVE is decided by the folder the game
  loads. Before any build, deploy or launch, the run asks `tools/acceptance/session-environment.ps1`:
  `-Mode status` reports the machine (each tree classified by its own marker DLL, never by a folder name),
  and `-Mode ensure-cuo` makes CUO's tree active. It refuses while any game process is running — a running
  game means its owner is playing — and in that case it kills nothing and moves nothing. The marker names,
  the process name and the parking name are machine facts (`AGENTS.local.md`).
- **A game or Steam instance is already running** → do not kill it. Report it and ask. Only processes
  this run started may be stopped by this run.

## 3. Acceptance plan

For every ticket in the batch, classify each row before running, and write the classification into
that ticket's record:

| Class | Meaning | Evidence it needs |
|---|---|---|
| `machine` | the outcome is a state, a message or a number (a report arrived once, a value converged, a log line appeared) | a log excerpt or a probe result |
| `visual` | the outcome is something on screen (a flash, a pose, a panel, a layout) | a captured frame the agent reads |
| `feel` | the outcome is how something plays (smoothness, lag, absence of teleporting) | a recording, analysed frame by frame; what stays subjective becomes a residual for the user |
| `blocked` | the capability it needs is missing (preflight) | the missing dependency and the question asked |

The plan then groups the batch into shared setups: which client is host, which scenarios are reached
in the same world, which tickets reuse one another's setup, and what has to be true in the world before
a row can be observed (a world entered, an item held, a peer in range). A ticket whose setup cannot
share the session already running gets its own run, and that decision is recorded.

## 4. Build, deploy, artifact identity

Acceptance runs against a commit, never against a dirty tree with an unrecorded build.

```powershell
dotnet build CasualtiesUnknownOnline.slnx
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
powershell -ExecutionPolicy Bypass -File tools/verify-deploy.ps1 -GameDir "<game-dir>"
```

`verify-deploy.ps1` prints the deployed plugin's `ProductVersion` with its `+<sha>`; that value is the
artifact identity the record carries. A build that passed without the latest artifacts running is not
acceptance. Before a sandboxed client starts, apply the shadow rule in `AGENTS.local.md`: a sandbox
copy that is older than the physical one runs old code, and the shadow files are removed file by file
(never recursively).

## 5. The two-client session

One session serves the whole batch: the plan's setups run in order, and each ticket's evidence is
collected while its scenario is up.

- The **host** runs on the physical machine, launched through Steam (`game-app-id`) so Steamworks
  initialises.
- The **guest** runs in the Sandboxie sandbox named by `sandbox-guest-root`, started after the deploy.
- The **third client**, when the `sandbox-alt` capability is present, runs in the alternate Sandboxie
  sandbox and joins the same lobby through the same Online UI; it supplies the third-peer view and is
  driven through `hotrepl-alt-url`. When the capability is absent, every third-peer row is `blocked`,
  never guessed from the two participant views.
- Both join through the game's own Online UI (host creates the lobby, guest joins with the lobby id).
  The native UI is the only lobby surface CUO has; the run reuses it. Getting the clients that far is
  [`tools/acceptance/launch-clients.ps1`](../../tools/acceptance/launch-clients.ps1): it reuses a client whose
  driver endpoint already answers (never starts a second), starts a box's Steam only when the box has none and
  never stops it, waits for Steam before the game (a box Steam needs up to a minute; the game's own
  `Steam initialized` line is the check, and a game that came up without it restarts that box's client only),
  and then creates the lobby, joins guest and alt and starts the run. Every machine value is a parameter; a value
  left out comes from the gitignored local facts file, and a value in neither place is a named error. Pass
  `-NoJoin` to only start the three clients.
- **The lobby join is not the session.** The join's own answer reads `role: Guest` while the member's
  session is still inactive — no handshake has completed, the host has no member yet, and a host-only
  console verb is refused in that state ("Permission denied: this command is host-only", the host's own
  role arriving only with `create-lobby`). Anything a row reads on the join's answer is a reading of
  nothing: the batch takes `create-lobby` before anything that needs the host role, and gives the session
  its own bounded wait — the member's own session/announcement reading is what says it exists — before the
  first row's step.
- Probes and actuation use the in-process evaluator at `hotrepl-host-url` / `hotrepl-guest-url`:
  reading live state, forcing a setup, or asserting that a message was handled. The committed driver
  helper `tools/acceptance/drive-in-process.ps1` carries the scenario vocabulary — `state`,
  `open-window`, `goto-page`, `click`, `set-text`, `create-lobby`, `join-lobby`, `start-run`,
  `continue-run`, `leave-world`, `console`, `quit` (`-ListActions` prints it) — and reproduces a setup
  through the Online UI's own registered controls, the game's own run entries and the CUO console,
  never through OS-level keyboard or mouse. Gameplay states that vocabulary cannot reach — a carry
  relation, a forced body state, a movement window, a key a gesture reads — are the committed scenario
  recipes under
  `tools/acceptance/recipes/`, run one eval per invocation with
  `drive-in-process.ps1 -Action recipe -Recipe <name> -RecipeArg <key=value>`; each recipe declares the
  arguments it uses and the tickets it serves, and a multi-step scenario is the run's own loop of
  invocations. A recipe that needs an eval declaration says so in its own header (`// requires: <name>`),
  and the run loads that declaration once per client with
  `drive-in-process.ps1 -Action declare -Declare <name>` before the first step that uses it. Ad-hoc
  probes stay evals of the same channel, and a batch-local sender of the `probe.ps1` shape substitutes
  `{{key}}` text VERBATIM: its template spells a string placeholder with its own quotes
  (`var mode = "{{s:mode}}"`) and its `-Arg` takes the full typed name (`s:mode=probe`), where the
  committed driver's `{{s:key}}` inserts the quotes and its `-RecipeArg` takes `key=value`; a bare word the
  raw replacement leaves in the code comes back as `(1,1): InteractiveHost` (batch `20261020-a`). A window
  whose two carriers are written at different moments — an item report at once, the member's 1 Hz character
  report up to a second later — is held OPEN by parking the receiving family (`net-drop-message` on the
  host), never raced: one driver call's own gap exceeds the 1 Hz period, so a cut issued "right after" the
  action lands outside the window and reads as agreement (batch `20261022-c`; the stored carrier is
  `tools/acceptance/recipes/saved-character-read.cs`). The socket
  dies with the game process, so a restart means reconnecting before the next probe. A probe's answer
  travels as ONE eval value, so a full table of a dense world does not fit: batch `20261021-f`'s
  `enemy-table-read` and `enemy-scene` on a 956-enemy world both answered `answered with a truncated value;
  the answer is not usable`, and the rows were judged from a batch-local probe that filtered to the ids or
  the radius under test. Read a large table through a probe that filters or aggregates it, and cite the
  compact answer and the probe source in the record.
- **A key message reaches the game only while the client's own window is the foreground window.** A batch
  that plans key rows checks that fact FIRST; when the client the run started is behind another window, the
  run asks the user for a one-time switch of THAT window (the message channel itself never activates a
  window), and records the switch. `posted=true` with `heldAtEnd=false` is the tell that the game did not
  see the key.
- Capture is per row, not per run: one frame (or recording) per `visual` / `feel` row, captured from the
  client's own window (never the desktop, so the run never needs the clients in front), written to `acceptance-artifacts-dir` under an artifact id, plus the log excerpts a `machine` row needs.
  The capture itself is [`tools/acceptance/capture-window.ps1`](../../tools/acceptance/capture-window.ps1)
  (`-Name <process> -Out <artifacts-dir>\<artifact-id>.png`): it finds the client's own window, renders it with
  `PrintWindow` + `PW_RENDERFULLCONTENT` (no activation), and refuses a hidden or zero-size window rather than
  writing an empty frame (verified 2026-10-24 on the 1280x720 window: 1298x767 with the CUO overlay legible —
  the bitmap is the whole window, title bar included, so a box row is judged on the canvas inside it).
  **A sandboxed client is capturable once the box stops hiding it**: Sandboxie's `CoverBoxedWindows` (and its
  sibling `BlockScreenCapture`) must be OFF for that box, or the host sees a covered window and
  `Get-Process` reports a zero `MainWindowHandle` (measured 2026-10-24: a guest client ran with its plugin
  loaded and handle 0). Turn them off per box with
  `SbieIni.exe set <box> CoverBoxedWindows n` and `SbieIni.exe set <box> BlockScreenCapture n` (the tool writes
  `Sandboxie.ini` without elevation), then restart the CLIENT — its settings are taken at process start — and
  leave the box's Steam running; a running client keeps the old setting, so only the client is restarted. After
  that the sandboxed window captures like any other (verified the same day: 1298x767 with the CUO overlay
  legible). A visual row whose evidence must be on a GUEST is therefore capturable, not a residual, once the two
  settings are off.
  On a DPI-scaled display the capturing process sets its DPI awareness before `GetWindowRect`/`PrintWindow`;
  otherwise the bitmap is the physical window and the canvas's right/bottom — the launcher, a docked panel —
  are cropped out of the frame. **A frame's pixels are not the canvas's units**: the UI layer a row is about
  may be laid out on its own reference resolution (batch `20261020-a` read a 1920x1080 overlay canvas on a
  1280x720 window), so a box row is judged on the live rect's own canvas coordinates and the frame carries the
  look, never a pixel measurement of the label.
- **The characters are part of the setup, and a restore is read back.** Restore each character BEFORE the
  long row work, and keep checking: an unconscious healer is refused, so a heal attempted after a character
  has already collapsed does nothing. There are three INDEPENDENT heal surfaces: the game's own `heal`
  console command (item-free, on the LOCAL body; CUO extends it with severed limbs and hollow), CUO's own
  console `/heal` cheat command (the immediate full reset of a selected player, cheat-family permission;
  `done/heal-cheat-command-state-reset.md`, accepted by batch `20261018-u`), and the remote-medical UI's
  item-based cross-player treatment. The run restores with whichever is deployed, gives the item for the
  medical path, and reads the vitals back (`body-read`, `wound-read`). A host left in a near-death state is
  a setup defect, not a row's finding: rows judged on its frame are re-read once it is restored.
- **A forced critical state names the body's role.** A VIEWER that must keep a native panel open is forced
  through the septic-shock clause (`septicShock` 90: 0.07/s decay, no fibrillation trigger), because the
  hunger/limb clause (`hunger < 0` and limb-0 `muscleHealth` < 25) drops consciousness within seconds and
  the game closes the WoundView with it; write the game's own public `Body` fields the 1 Hz snapshot
  captures, and restore and read them back like any other forced state.
- **A panel sound row names each AudioSource's own clip.** A panel's native critical loop can be its own
  clip (`WoundView`'s is `healthpanel-alert`, which already reads the displayed body) while the heartbeat
  family is `heartthump*`: read the clip off each AudioSource and call a panel "double sound" only when the
  same native sound is duplicated, not when two different native sounds play.
- The run closes cleanly: quit both clients through the game, then stop what the run started.

## 6. Verdicts

Every row ends in exactly one of: `pass`, `fail`, `unproven`, `blocked`, `residual`.

- `pass` requires evidence produced by this run, against the deployed artifact, and the evidence is
  named in the record. Reading the captured frame is part of judging a visual row.
- `fail` names what was observed against what was expected; it is the input to the rejection path.
- `unproven` is honest, not a soft pass: the run could not put the world into the state the row
  needs, or the evidence is ambiguous. An `unproven` row keeps the ticket open.
- `blocked` names the missing dependency.
- `residual` is a row only a person can judge; it goes to the user's list, never to a self-issued
  pass.

A row that needs a running client is **not** a residual: the run starts one. A ticket's own sentence that a
surface is "verified by the user on the physical machine" describes what its writing cycle could not do, not
what this run cannot do; whether the run can reach the row is what the preflight's
`RESULT: OK - a full two-client run is possible` answers, and a run that can start clients must start them.
Only a judgement no run can produce — a taste, a feel, a call only the owner can make — is a residual. A row
the run genuinely cannot produce because a dependency is missing is `blocked`, with the question asked (§2) —
never a residual, and never a pass.

- A row whose expectation is an absence (zero `<warning>`) is read from the run mark through at least one
  full periodic cycle of the mechanism that can emit it; an entry-edge-only read is not evidence. A mark is
  a byte offset into the file it named, so a member's rolling log that rotates under Debug-level volume
  makes the old offset land in unrelated lines: print the window's first and last timestamp and re-mark
  after a rotation before judging an absence (batch `20261021-e`'s guest log).
- An "the event arrived" condition names the MOVED item: a container whose contents moved is watched for
  divergence only and owns no `[CarriedSync] applied` line when its own row did not change. A judgement
  that demands an applied line for the parent fails a clean run — batch `20261025-c`'s driver did, and the
  window's evidence was re-judged against the child's fact.

## 7. The record

One record per ticket — `docs/evidence/acceptance/<ticket-slug>-<yyyymmdd>.md` — committed, English,
no machine values: the local artifact directory is referenced by the key name, the frames by artifact
id. Every record names the batch it belongs to, so a later reader can find the sibling tickets, the
shared build and the session they came from.

```text
# Acceptance record — <ticket title>

- Ticket: <slug> — verdict: moved to done/ | back to todo/ (status field: `- Status: Todo — Rejected (…)`) | stays in review/
- Batch: <run id, e.g. 20260927-a> — tickets <slug>, <slug>, …
- Commit: <sha> · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion +<sha>
- Run: <start> → <end> · Host: physical machine · Guest: sandbox
- Dependencies: <ids used, from dependencies.md>
- Artifacts: <artifact-id> (<kind>) in the directory named by acceptance-artifacts-dir

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | … | visual | pass | <artifact-id> |

## Residuals for the user
<the rows a person must judge, each with its artifact id, in plain language>

## Limits
<what this run could not prove, and why>
```

## 8. Ticket transition

- Every ticket in the batch whose rows all `pass`, or whose remainder are `residual`, moves to
  `docs/backlog/done/`; the index rows move in the same change and each ticket links its record.
- A ticket with a `fail` or `unproven` row moves back to `docs/backlog/todo/` with
  `- Status: Todo — Rejected (…)` (the field repeats the folder's label) and the failing row named;
  the fix is ordinary development work.
- A ticket with a `blocked` row stays in `review/`, the blocker and the question asked are recorded
  in the ticket, and the acceptance record says so.
- `BacklogIntegrityGateTests` refuses a ticket that is not listed exactly once in its own section, so
  the moves and the index are one edit.

## 9. Residuals to the user

Residuals are batched: one message per run, plain language, each item saying what to look at and
which artifact shows it. The user's answer is recorded back into the records — a rejected residual
returns that ticket to `todo/` with the same rejection marking as a failed row.

## 10. Lessons and the closing harvest

Before the run closes, fold what it taught ([AGENTS.md](AGENTS.md) rule 9): reusable lessons into
[lessons.md](lessons.md), machine values and local gotchas into `AGENTS.local.md`, and anything the run
proved wrong or missing about this page into this page. That ledger holds only what has no other home —
an entry is ONE bounded line naming the carrier its reader opens, a rule a page, a recipe or a tool can
hold goes there instead, and an entry whose conclusion has landed elsewhere is deleted rather than
repeated (`AcceptanceLessonsGateTests` refuses the narrative shape). A batch that ends without a lessons
pass is not finished — the next batch pays for whatever this one noticed and did not write down.

An acceptance conversation runs the same pass on its own before it ends, unasked: the agent summarizes
what the run taught — what worked, what it stepped on, and the scripts or snippets it wrote. A helper
general enough to reuse moves into `tools/acceptance/` (or the local artifact area named in the closing
report when it is machine-specific) so the next run starts from it instead of rediscovering it; a script
that stays only in a transcript is lost. The closing report names what was harvested and where it landed.

## Capability status

Which steps of this page are executable today, and which are still being built:

| Step | Status |
|---|---|
| 2 — dependency preflight | **executable**: `tools/acceptance/preflight.ps1` |
| 1, 3, 6, 7, 8 — plan, verdicts, record, transition | **executable** as a procedure; the run is driven by the agent, not yet by a script |
| 4 — build, deploy, identity, shadow rule | **executable**: existing `tools/*.ps1` plus the local shadow rule |
| 5 — session, probes, capture | **partly executable**: a running client is driven through the committed in-process helper (`tools/acceptance/drive-in-process.ps1`), which acts through the Online UI's own registered controls and runs the committed scenario recipes (`tools/acceptance/recipes/`) for gameplay states the UI cannot reach, one eval per invocation, loading the eval declaration a recipe names (`-Action declare`, once per client) before the first step that needs it; launching both clients, frame capture and the log channels keep their steps, and each run records which mechanism produced each piece of evidence |

## Limits

- A run proves what it observed; it cannot prove the absence of a rare race by playing once. A row
  about timing or frequency gets several repetitions and says how many it ran.
- Frames are read, not measured: a judgement call that depends on millisecond feel stays a residual.
- The dependency table is the boundary of what a run can do. Anything outside it is not "untested" —
  it is `blocked`, with the question asked.
- **A host rule cannot be changed from inside a run.** The admin page's rule rows are native
  `TMP_Dropdown`s whose options carry no CUO control ids (an invoked row highlights the field, and the
  driver has no value-carrying selection), and no console verb writes them; a row that needs a non-default
  rule is `unproven` with the gap recorded, never approximated by editing the config under a live session
  (batch `20261023-f`'s row 7).

## The drag batch (the input sequence, written down before it runs)

The remaining rows of the drag family (`cross-player-drag-use-feedback`,
`cross-player-drag-use-model-bounds`, `topical-limb-action-shape`, `remote-medical-treatment-operations`)
all need the same gesture, so they are ONE batch with one launch and four verdicts:

1. **Launch both clients once** (`tools/acceptance/launch-clients.ps1`), form the session, enter the world.
2. **Point at the target**: `drive-in-process.ps1 -Action recipe -Recipe screen-project -RecipeArg x=…,y=…`
   on the dragging client answers the target body's CLIENT pixel and the panel's own screen rect. The
   recipe exists because the projection has to come from the game's own camera; a guessed pixel is not a
   gesture.
3. **Find the item's slot** with the inventory probe (the item's held position on the client), because a
   drag starts on the item, not on a control id.
4. **Perform the gesture** with `tools/acceptance/send-input.ps1 -Action drag -ProcessId <dragging client>
   -FromX … -FromY … -ToX … -ToY …`: press on the item, move to the target body's pixel, release. Every
   point is CLIENT coordinates; the helper brings the window forward itself.
5. **Read the verdict from the clients' own logs**, never from the picture: the cue line and the refusal
   line are on the operator's side, the applied/inventory result on the target's side.

The sequence is recorded here because a gesture is a call site and a claim about it must be falsifiable
before the run: a step this list cannot perform is a design defect, not a batch failure.

