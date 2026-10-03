# Acceptance lessons

What the runs keep teaching. Every acceptance run folds its reusable lessons back here before it
closes ([AGENTS.md](AGENTS.md) rule 9); a lesson that stays in a transcript is lost, and the next
batch pays for it again. Machine values and local gotchas belong in `AGENTS.local.md`, not here; a
rule that binds the whole repository graduates to `docs/AGENTS.md` or `AGENTS.md`.

## How to add an entry

One entry per lesson, newest last, in this shape:

```text
## <date> — <what happened, as a headline>

- Symptom: <what the run saw>
- Cause: <the mechanism, not the story>
- Change: <the file and rule that now carry it>
```

An entry is worth adding when it would change what the next run does: a preflight check that lied, a
scenario setup that turned out to need something extra, an evidence kind that was not convincing, a
dependency the table did not name, a step that cost more than it returned.

## 2026-09-27 — The preflight matched nothing on its first run (PowerShell 5.1 code pages)

- Symptom: `preflight.ps1` reported `no 验收环境 section resolved` and then `FACT-MISSING` for every
  key, while the facts file plainly carried the section.
- Cause: Windows PowerShell 5.1 reads a UTF-8 file without a BOM — the script itself, and the facts
  file it read — through the system code page, so the Chinese heading in the script and the one in
  the file were mangled into two different strings that could never match.
- Change: the script is ASCII-only and matches an ASCII section marker
  (`## 验收环境 / acceptance environment`), and reads the facts with an explicit `-Encoding UTF8`.
  A committed script must never depend on how the host guesses its own encoding.

## 2026-09-27 — Auto-loaded instruction files are a budget, and it was nearly spent

- Symptom: adding the area's two instruction files (measured on the working tree, 2026-09-27:
  `docs/acceptance/AGENTS.md` 4,799 bytes + `docs/acceptance/AGENTS.local.md` 2,670 bytes = 7,469) put
  the sum of every instruction file 906 bytes over the 65,536-byte budget, while the always-present root
  pair already used 90% of it.
- Cause: nobody had measured the budget before adding two auto-loaded files, and the gate counted every
  nested file into the same budget — which is not what the loader charges. It charges the always-present
  pair and drops the user-level instruction file when they leave no room (the session reported exactly
  that).
- Change: `AgentInstructionBudgetGateTests` now measures what the loader charges — the always-loaded root
  pair against the budget (58,973 bytes that day: `AGENTS.md` 18,746 + `AGENTS.local.md` 40,227, leaving
  6,563), and every other instruction file, tracked or area-local, against its own 5,120-byte ceiling.
  That headroom is less than the 19,456-byte user-level file, so the loader still drops it; restoring it
  needs 12,893 bytes of cuts from the root pair. Measure a file's bytes before adding one, and prefer a
  page over a router.

## 2026-09-27 — An instruction file can be at its ceiling before you touch it

- Symptom: naming the new area in `docs/AGENTS.md` §1 pushed the file to 5,447 bytes and the nested
  ceiling gate refused the build; `docs/AGENTS.md` now sits at 5,1xx of its 5,120-byte allowance.
- Cause: the router had grown to within ~90 bytes of its own ceiling, so any added sentence breaks it —
  and the fix is not to raise the ceiling but to say the same thing in fewer words.
- Change: the fact was folded into the existing bullets and redundancy trimmed in §1/§2/§4/§7. Measure a
  file's byte size before editing an instruction file, and prefer moving detail into a page over growing
  the router.

## 2026-09-27 — A preflight that only reports is not yet a run

- Symptom: the first green-ish preflight reported `artifacts` missing because the artifact directory
  did not exist; nothing was wrong with the machine.
- Cause: a writable evidence directory is a dependency like any other, and creating it is setup the
  run owns — not a question for the user.
- Change: a missing artifact directory is created by the run before it asks anything; the preflight
  keeps reporting it, because "not created yet" and "not writable" look the same from outside.

## 2026-09-27 — A machine fact can be green and still be the wrong game

- Symptom: `steam://rungameid/<game-app-id>` launched an unrelated game (Forts) on the acceptance
  machine, while the `steam` preflight row read `present` and even echoed the app id.
- Cause: the local facts file carried the wrong `game-app-id`. The preflight proves only that the key
  is set; it never proves that the id maps to the `game-dir` install, and the launch step consumes the
  fact directly, so the wrong value reached a real launch. The correct id is in the install's own
  `steam_appid.txt` and in the app manifest's name.
- Change: the fact is corrected, and a run must cross-check the id against the install before
  launching. The preflight now does that: the `steam` row compares `game-app-id` with the install's
  own `steam_appid.txt` and with the library app manifest's `installdir`, requires every source that
  exists to confirm it, and blocks a launch when one contradicts or none can confirm.

## 2026-09-27 — An interrupted test run leaves a testhost that reddens the next one

- Symptom: a later full-suite run failed to build its test project (MSB3026/MSB3027, "file is being
  used by another process", `testhost.net48`), while the gate project passed 288/288 in the same run.
- Cause: an earlier `dotnet test` invocation that never completed left `testhost.net48` holding
  `xunit.abstractions.dll` in the test project's `bin` directory.
- Change: check for `testhost*` processes started by this run and stop them before re-running the
  suite. A full-suite red that is a file lock is an environment finding, not a product finding.

## 2026-09-27 — In-process control is the driver this machine allows

- Symptom: the session batch looked blocked on the staged `input` capability; asking the user settled
  it: the agent may drive the game from inside its process, but must never take over the physical
  mouse and keyboard.
- Cause: the `input` row describes scripted keyboard/mouse driving — neither built nor permitted here.
  What the run actually needs is the in-process evaluator (`hotrepl-*`), which is present.
- Change: session setups are driven through the evaluator (create/join were both proven this way);
  the `input` row's capability and degradation now name in-process control instead of OS input, and its
  committed driver helper under `tools/acceptance/` stays staged until the harness lands.

## 2026-09-27 — A title-screen click is not "start the game"

- Symptom: the probe created the lobby (`Session role: Host`) and the guest joined
  (`role=Guest`), but invoking the title screen's SleepingBag `AdaptiveButton.Clicked()` shut the
  host client down cleanly instead of starting a run.
- Cause: each title-screen object carries its own action, and nothing in the object's name says which
  one starts a run; calling a handler outside a real pointer event exercised whatever that object
  does. "It ran" is not "it did what the scenario needed".
- Change: session actions go through the Online UI's own entry points; before any in-process click of
  a scene object is used as a setup step, its handler's target must be identified from evidence.

## 2026-09-27 — A delegated triage needs a machine-checkable contract

- Symptom: three read-only triage subagents classified the 142 `review/` tickets; two reports listed
  slugs outside their own input slice and their headline counts disagreed with their own lists (one
  headlined "24 offline" while listing 44 slugs).
- Cause: the prompts fixed the rubric but not a verifiable contract, so a report could be internally
  inconsistent and still look finished.
- Change: a triage delegation must echo its exact input list, emit exactly one class line per slug,
  derive every count from those lines, and the orchestrator re-derives the counts from the file
  before any batch is built on it.

## 2026-09-27 — A fact value carries no prose

- Symptom: the corrected `game-app-id` fact carried its verification note inside the value, so the new
  cross-check reported the id as contradicting the very install it had just been verified against.
- Cause: everything after the colon is the value — the note was appended to it, and the launch line
  would have consumed the same string.
- Change: `AGENTS.local.md` carries the bare value with its note on a line of its own, and
  `dependencies.md`'s local-facts contract says that a value is taken literally.

## 2026-09-27 — Moving tickets must move their references

- Symptom: the 30-ticket move went red on three gate checks at once — 45 prose references
  (`review/<slug>.md` across 22 documents), five sibling relative links, and one test anchor in a new
  record that a substring check had passed.
- Cause: the folder is the status, but references are hand-written prose; the move script updated the
  index and the tickets only. The cross-reference gate exists for exactly this rot, and a substring
  match is not the anchor gate's member check.
- Change: a batch that moves tickets fixes the prose references, the sibling links and the anchors in
  the same change — including the ones inside the moved tickets, whose exempt record folders the gates
  do not scan — and validates anchors against the gate's own member set.

## 2026-09-27 — An offline batch is still a run

- Symptom: the offline batch needed the whole `workflow.md` §4 chain minus the clients — preflight,
  build, both suites, format, and the deploy + hash verification; a ticket whose acceptance names a
  deploy row was only decidable because that deploy was part of this run.
- Cause: "offline" describes the rows, not the run, and an earlier suite run is never this run's
  evidence.
- Change: batch `20260927-b` ran the chain itself and its records cite only its outputs; a candidate
  whose row needs a measurement or a comparison the run does not produce is dropped and stays in
  `review/`, with the reason in the batch scope page.

## 2026-09-27 — A row that names a past event is judged by its durable outcomes

- Symptom: a ticket's acceptance carried "an independent adversarial review in a fresh context
  covering ...", an event no later run can re-create.
- Cause: some rows are process records, not behaviours; reading them as "re-run the event" makes the
  ticket permanently un-acceptable, and reading them as "trust the ticket" is no evidence at all.
- Change: such a row is judged by the durable outcomes the event produced (its findings pinned by
  regression cases that pass in this run), the record's Limits says the event itself was not re-run,
  and no record claims the event happened in the batch.

## 2026-09-27 — A header bullet can span lines; an insertion must not split it

- Symptom: the `- Acceptance record:` bullet landed inside a multi-line `- Related:`/`- Source:`
  bullet in 11 of the 30 moved tickets; five had their original bullet truncated and its continuation
  lines re-parented onto the new bullet, and no gate could see it (`backlog/done/` is exempt).
- Cause: the move script anchored on the first line matching `^- Related:` and inserted at
  `anchor + 1` without checking whether the bullet continues on the following lines.
- Change: insert after the last continuation line of the header block (or before the blank line that
  ends it) and verify the insertion point on every ticket the script touches — rebuilding the ticket
  from its `review/` revision plus the two intended lines is the cheap way to make that exact.

## 2026-09-27 — A rejected ticket's status field still repeats its folder

- Symptom: `- Status: Rejected` in `todo/` turned `EveryTicketStatusFieldAgreesWithItsFolder` red —
  the field must start with the folder's label (`Todo`), so the first rejection this workflow
  executed could not be committed in the shape the pages prescribed.
- Cause: the acceptance pages wrote the verdict as if it were the field; `BacklogIntegrityGateTests`
  derives the expected prefix from the folder's canonical map ("the folder is the status; the field
  repeats it for a reader who opens the file").
- Change: a rejected ticket carries `- Status: Todo — Rejected (…)`, and the four pages that
  prescribed the bare form are corrected.

## 2026-09-27 — In-process driving is one eval per frame

- Symptom: a driver cannot "open the window and then click a page control" inside one snippet: the
  Online UI's registered controls are rebuilt every frame, and a snippet that waits inside itself would
  stop the update it is waiting for.
- Cause: HotRepl drains at most one eval per `Tick()` on the Unity main thread, and the Online UI
  rebuilds its action table in `OnlineUiHost.Update`; the frame that would offer the control runs only
  after the snippet returns.
- Change: `tools/acceptance/drive-in-process.ps1` performs one eval per step and waits across round
  trips with bounded retries, so a control the current frame does not carry is waited out, never raced;
  the in-process half stays synchronous and returns an `offered` / `applied` verdict per call.

## 2026-09-27 — The evaluator's lambdas must be capture-free, and inner parameter names must not shadow outer locals

- Symptom: the driver's first live eval answered `eval-error` with the opaque message
  `(12,26): <InteractiveExpressionClass 2>.<Host2>m__0()`; a cut with capture-free helpers then answered
  `ok` with no value at all.
- Cause: two Mono.CSharp REPL limits, both silent at compile time. A lambda nested in another lambda
  cannot emit a closure for captured locals (it fails at run time as an internal exception), and an
  outer local whose name matches an inner lambda's parameter name makes the whole submission return void
  instead of its value.
- Change: every helper in `tools/acceptance/driver/InProcessDriver.cs` is capture-free (state travels as
  a parameter, flags as a `const`), and its inner lambda parameters avoid the outer locals' names. A
  future snippet that needs a closure should declare it at the snippet's top level instead of inside
  another lambda.

## 2026-09-27 — A driver vocabulary is not a scenario library

- Symptom: the first two-client batch judged every row of the driver's session surface
  (`create-lobby`, `join-lobby`, `start-run`) but could not open one session-basic ticket: each of
  those needs a world state the closed vocabulary does not reach (a carry relation with a limp rider,
  a forced severe-sleepiness state, a native right-click menu, a key press, an eating sound).
- Cause: "in-process control is available" was read as "the scenarios are reachable". The helper proves
  the channel and the Online UI's own controls; it carries no setup path for the game's gameplay states,
  and no per-scenario recipe was recorded anywhere the run could reuse.
- Change: plan a session ticket into a batch only when its setup path exists as a committed or recorded
  recipe; otherwise record it as a setup gap and leave it in `review/` (batch page
  `docs/evidence/acceptance/20260927-c-scope.md`).

## 2026-09-27 — The sandboxed second Steam is already running; process counts are not readiness

- Symptom: the guest bring-up waited 120 s for a new `steam.exe` and timed out, while the guest was
  fine — the sandbox already ran a Steam instance (a different account, auto-login) and the launch had
  been forwarded to it.
- Cause: Steam is single-instance per sandbox namespace; re-launching it creates no process, so a
  "new process" readiness check can never pass.
- Change: judge the guest side ready by its own evaluator endpoint (`hotrepl-guest-url`), never by
  process counts; the launch recipe is in the local acceptance facts.

## 2026-09-27 — A client window is captured without moving it in front

- Symptom: the desktop capture showed the user's browser and chat windows; both clients were occluded,
  so the run had no readable frame of the Online UI or the world.
- Cause: a screen-rectangle copy cannot see an occluded window.
- Change: capture each client's own window (`PrintWindow` with `PW_RENDERFULLCONTENT`, helper
  `.acceptance/tools/capture-window.ps1`): it reads an occluded window without activating it or
  injecting input. Window-level capture is the default for every run (workflow §5), and a client is
  never brought to the front.

## 2026-09-27 — A layout pin is not a layout

- Symptom: the layout pass's pins were green and the shell declared `TabHeight = 30`, but the live window's
  tab strip rendered 333 units tall — ten times the declared height — with the tab buttons stretched to it,
  and only a probe of the live rects found it (the user's own second pass reported the buttons as "very
  large").
- Cause: a layout group's reported height is composed, not declared. The tab row's own
  `HorizontalLayoutGroup` reported its children's forced-flexible sum, so the shell's vertical group saw a
  flexible band and fed it the window's leftover height; a source pin held the declaration while the
  engine's composition still leaked.
- Change: bands zero their flexible height (only the page's band is flexible), pinned by
  `OnlineUiLayoutDetailPinTests.TheTabStripKeepsItsOwnHeight` with its mutation; and a run that judges a UI
  row reads the RENDERED rects, not only the source pins — the live probe is what decides.

## 2026-09-27 — A five-second connect cap turned a loaded machine into a "timeout"

- Symptom: the driver's black-box tests passed 20/20 in isolation but failed three of four full-suite runs
  with exit 3 (timeout), a different case each run.
- Cause: two layers. The driver capped its connection budget at five seconds regardless of `-TimeoutMs`,
  and the class spawns `powershell.exe` per case while the other 4,500 tests run — under that load the
  process start plus the connect stretched past the cap, so a slow endpoint was reported as an unreachable
  one.
- Change: the connect budget follows the action budget up to the eval ceiling
  (`tools/acceptance/drive-in-process.ps1`, pinned by `DriverToolTests.TheConnectTimeoutFollowsTheActionBudget`),
  and the driver tests run in a non-parallel collection (`ToolProcessCollection`). The other two PowerShell
  harnesses carry no internal budget and stay parallel: a slow machine only makes them slow.

## 2026-09-27 — A recipe smoke belongs inside the session it verifies

- Symptom: batch `20260927-c` named per-scenario recipes as the missing capability and left 45 tickets
  waiting; a separate "recipe cycle" then looked like work that must precede a session.
- Cause: recipes were planned as a capability, not as steps of the run. The first smoke (a piggyback
  relation, a 25-call movement window, a forced idle state) used the same two clients the batch needs and
  answered the whole question in minutes.
- Change: smoke the recipe set in the session it will run in — `carry-start` → `carry-read` →
  `move-drive` window → `carry-read` → `carry-stop` — and treat the probe JSON as the layer's proof. The
  smoke ran on deployed `0.1.0+631a8d82`: the carrier's own view read `mountedToLocalCarrier=true`,
  `pinnedToCarrier=true`, `riderDriftMax=0`, `limbSeparationMax=0` while the carrier walked 15 units.

## 2026-09-27 — A forced body state only survives on a still body

- Symptom: `body-force idleTime=13` returned `idleTime=13`, and two seconds later the same client read
  `idleTime=0` with a locomotion clip.
- Cause: the game's own `Body.Update` zeroes `idleTime` whenever the body is moving (move input or
  velocity above rest); the smoke had just driven the carrier.
- Change: a forced-state scenario sets the state and reads it back in the same step, and the run keeps the
  body still between the write and the read — driving it only after the reading.

## 2026-09-28 — A body that spawns inside the terrain freezes movement and pose alike

- Symptom: both bodies read `crouching=true` forever and `move-drive` produced zero displacement; a
  `velocity=3` write and a 12000-unit impulse left the position unchanged, and the run nearly blamed the
  movement path. The earlier probe with `col.size` had answered "no overlap" and hid the blocker.
- Cause: both bodies spawned overlapping the `Ground` collider. The game's own check (`Body.cs:3089`:
  `OverlapBox` at the body with `origColSize` over the `Ground` layer) then sets `crouching = true`
  every frame and the body is physically caught. The crouch shrinks the collider, so a check run with
  the shrunk size can never see the overlap the game's own dimensions see.
- Change: check the overlap with the game's own `origColSize` before blaming a movement path, and free a
  caught body (lift it a few units and let it settle) before any movement scenario. The check, the
  escape and the batch's readings are in `docs/evidence/acceptance/20260927-d-scope.md` and the batch
  run log.

## 2026-09-28 — `move-drive` walk is frame-window-dependent; slide is the displacement source

- Symptom: 29 back-to-back `walk` calls moved the carrier 0 units, while the earlier smoke recorded 15
  units after 25 calls; `slide` (a real velocity write) accumulated 4-5 units per window.
- Cause: the game's `PlayerCamera.HandleInput` rewrites `body.moveDir` from the keyboard every frame
  (`PlayerCamera.cs:901-920`), so a walk write reaches the physics step only when the eval lands in the
  narrow window after that rewrite. The previous session's "15 units" was the game's own scene
  placement, not the calls.
- Change: movement windows use `slide` and confirm travel from the positions before and after; a
  walk-only window that shows no travel is `unproven`, not a pass.

## 2026-09-28 — World frames do not resolve poses at the game's own zoom; the native panels do

- Symptom: the batch's world captures render each character a few pixels wide (22.5-unit orthographic
  half-height), so rider attachment, slouch and mouth rows were unreadable in them, while the native
  `WoundView` medical panel captured and read cleanly (heart rate and an advancing ECG).
- Cause: the camera never zooms in for the local player, and an evaluator write to
  `Camera.main.orthographicSize` is reverted by the game within a second.
- Change: judge a visual row from the largest readable surface the scenario has (the native panel), crop
  and enlarge through the window geometry when that helps, and record a row the frames cannot resolve as
  a residual or `unproven` — never as a pass.

## 2026-09-30 — A third client is a capability: declare it, port it, and start its sandbox Steam cold

- Symptom: the carry family's third-peer rows had no way to be judged; the alternate sandbox existed but
  its evaluator would have bound the host's port, and the machine restart had left both sandbox Steam
  instances down.
- Cause: `sandbox-alt` was not a dependency the preflight checked, and a sandboxed client reads the
  physical install's HotRepl config unless its own shadow carries one.
- Change: `sandbox-alt` is now a preflight row — the alternate client's own sandboxed HotRepl config
  must carry `hotrepl-alt-url`'s port, distinct from the host's and the guest's — and the launch
  procedure starts the box's Steam first when it is cold. Verified this run: three distinct accounts in
  one world, members 3, and every client seeing the other two clones.

## 2026-09-30 — Close the Online UI before a world capture

- Symptom: the first third-client capture showed the Online UI's Network page instead of the world; the
  driver's create-lobby / join-lobby leaves the window open.
- Cause: window-level capture faithfully renders whatever the window shows, and the Online UI is a
  full-window panel.
- Change: close the window (`click window.close`) on every client before a world frame. The UI itself
  stays evidence for the session state (it showed `Members: 3`) and is captured before closing.

## 2026-09-30 — A third-party clone's drift reading is pre-re-pin; a small non-zero is the smoothing path

- Symptom: the third client's logs carried four reportable `riderDrift` windows (0.018–0.079 world
  units) during one movement burst, while both participant views read zero in every window.
- Cause: a third-party view deliberately mounts nothing and re-pins after `SessionStatePump.Apply`, so
  the reading taken before the re-pin sees the interpolation remainder the row's "same tolerance as
  normal remote-player smoothing" names; the participant views' mount keeps the reading at zero.
- Change: record such windows with their magnitudes, judge the row against the smoothing tolerance it
  names, and keep the pixel-level remainder a residual. A participant-view warning remains the
  meaningful defect direction.

## 2026-09-30 — The live evaluator refuses a nested lambda that captures a local

- Symptom: a recipe the offline gate accepted failed in the client with an `eval_error` that named only
  the outer lambda's position; the same recipe's neighbours ran.
- Cause: the gate parses recipes with Roslyn, the client compiles them with Mono's REPL compiler, and
  that compiler refuses a nested lambda capturing a non-const local (a `const` is inlined, which is why
  the existing helpers use one).
- Change: pass every value a nested helper needs as a parameter, or inline the logic at the call sites; a
  recipe that passes the gate can still be rejected by the client.

## 2026-09-30 — A local container window needs the radial state or the game closes it

- Symptom: the guest's container capture showed an empty window while the recipe reported `opened=true`.
- Cause: `PlayerCamera.OpenContainer` only sets `currentContainer` and activates the menu; the per-frame
  `HandleRadialMenu` scales the radial down and calls `CloseContainer()` when `radialOpen` is false.
- Change: set `radialOpen = true` before opening a local container window (the remote-backpack path
  already does this through its own focus), and read `PlayerCamera.currentContainer` back as the open
  verdict.

## 2026-09-30 — Clearing the transfer table needs an idle observation window

- Symptom: the host's carried-id table can be restored between a clear and the registration report the
  clear is meant to test.
- Cause: the host rebuilds the per-guest table from the kernel after every external (guest) batch
  (`ItemService.OnExternalBatchCommitted`), so any other client's action can re-create the entries.
- Change: keep every other client idle through the window, read the host's `Registered … carried items`
  line as the registration evidence, and say in the record that the kernel rebuild path exists and was
  not the mechanism observed.

## 2026-09-30 — A relaunched guest rejoined the lobby but did not re-activate its session

- Symptom: the reconnect half of batch f could not start — after quit, relaunch and rejoin of the same
  lobby, the client's session facts stayed `active=false, members=[]` with scene `PreGen`.
- Cause (observed, not yet diagnosed): the guest's log repeats `Retrying handshake` for the whole process
  life while the host's member list never regains it; the join reaches the lobby layer but not the CUO
  session layer.
- Change: the reconnect rows stay `unproven` and the rejoin path needs its own verified checkpoints (one
  short step at a time, with a single observable checkpoint per step) before group C is attempted again; do not
  chain launch → wait → rejoin → read into one long procedure.

## 2026-09-30 — The reconnect blocker did not reproduce, so it cannot be fixed yet

- Symptom: after batch f's wedge (the guest rejoined the lobby but its session never
  activated), three reconnect attempts in batch g completed the full handshake and world
  entry; both ends read the peer in the lobby and the session state `Connected`.
- Cause: the failure is not deterministic in this environment. The batch-f logs show sends
  accepted with zero delivery, and nothing in the tree closes a wedged P2P session
  (`CloseSessionWithUser` has no caller), but one observation cannot name the trigger — the
  mechanism is a candidate, not a finding.
- Change: an unreproduced intermittent is recorded as such; the reconnect rows stay
  `unproven`, and no product change is written against a mechanism nobody has seen twice.

## 2026-09-30 — Clearing the arbitration table also removes the reconnect's restore input

- Symptom: after the declared offline clear and a rejoin, the guest's body came back empty
  (the bag and dogfood objects gone) although the kernel still listed them as carried; a
  control reconnect brought the bag and light back but not the dogfood.
- Cause: the host's saved-character restore merges the per-guest transfer table, so the
  clear removes the restore's input for the guest's carried items; the kernel rebuild then
  repopulates the table, and the guest applies the restore through the native pickup path,
  whose commands the host refuses as conflicts — after which the guest's reconciliation
  removes the local objects. The control could not separate the two halves because its
  table had already been flattened by the first cycle.
- Change: a reconnect scenario that judges carried items must keep the table populated or
  re-establish the carried set before the row is judged, and the restore/pickup conflict
  path needs its own focused run; clearing the table alone is not a valid container test.

## 2026-09-30 — On a rejoin the dense registration window can spend itself before the body exists

- Symptom: the host granted the watermark at handshake completion, but no registration
  frame followed for minutes; the guest log stayed quiet until after the world restore.
- Cause: the window opens on the watermark grant, which arrives before the guest's world
  restore; the dense 12 × 5 s budget is spent on empty captures (no body yet), and the
  steady minute only reports once the body exists. On the first reconnect the body was
  still empty at capture time, which is why the kernel rebuild path populated the table
  before any report could.
- Change: judge a rejoin's registration over the steady window after the body exists, and
  name the step that produced the frame; "no `Registered` line yet" is not evidence of a
  broken re-report while the body is still loading.

## 2026-09-30 — The two reconnect rows need opposite setups, so one session cannot judge both

- Symptom: batch `20260930-h` judged the container row's reconnect on a complete precondition —
  table, kernel and snapshot all nested — and the row passed; the registration row's "table
  rebuilt exactly once" still had nothing to rebuild, because the table was never emptied.
- Cause: the two rows pull in opposite directions. Clearing the per-guest transfer table also
  removes what the container row's restore merges (batch `20260930-g`), so a cleared-table cycle
  cannot judge that row, and a populated-table cycle cannot exercise a rebuild.
- Change: judge the container row on the populated-table cycle and give the registration row its
  own cleared-table run. Read the merge's own line (`Merged N transfer-table items onto the
  restore … (X appended; Y unplaceable)`) as the evidence that the new path ran, and "no
  `Conflict` and no `rejected` in BOTH logs" as the restore-report half — a machine reading of the
  item tree alone would not have named which path produced it.

## 2026-10-01 — Sizing a control is not laying out its internals

- Symptom: batch `20261001-j`'s window run showed every Preferences text box as an empty frame and
  every dropdown's caption missing, while the model carried everything — the hex text followed each
  pick (`#E54D47` → `#4D8CF2` → `#B873E5` → `#40FF80`) and a typed name was accepted.
- Cause: CUO sizes the control's own RectTransform, but the game prefab's internals keep their
  authored rects: a text field's `Text Area` reads 0×0 and the field's own rect reads hundreds of
  thousands of units wide, and a dropdown's caption sits hundreds of thousands of units off-canvas.
  The value is in the model and off the screen at the same time, so a behaviour row passes while the
  player sees nothing.
- Change: judge a UI row on the live rects of the control's INTERNALS (the `Text Area`, the caption,
  the caret), not only the outer box. `online-ui-layout-and-input-detail-pass` carries the rejection,
  and `j-probe-texts.txt` / `j-probe-inputfields.txt` in batch `20261001-j` name the shape.

## 2026-10-01 — A local window family is a one-client batch

- Symptom: the batch's scope page planned a guest half (the medical-panel corner, the world
  overlays), and the run finished the whole window family on the host alone.
- Cause: the Online UI window is a surface of its own process, and a second client is needed only for
  third-party views and world states; nothing in this family's rows depended on the guest.
- Change: plan a window/panel batch as a one-client run, and give peer-dependent rows their own batch
  with the setup they need — they stay `unproven` meanwhile, never guessed.

## 2026-10-01 — Probe output must land in the batch directory

- Symptom: this batch's records first cited probe numbers that existed only in the terminal, so a
  later reader could not re-open them.
- Cause: an ad-hoc eval prints its JSON to the caller, and nothing writes it to the artifact area.
- Change: write every probe a record cites into the batch directory as its own file (`j-probe-*.txt`)
  and cite the file name beside the reading; the numbers drift between reads, so the file — not the
  transcript — is the evidence.


## 2026-10-01 — A control's width cannot be measured, and a box needs one owner

- Symptom: the window's text fields showed empty frames while their model held the right value, and a
  dropdown's box was hundreds of thousands of units wide (batch `20261001-k`'s baseline probe: 690,623
  units, then 849,678 on a reading minutes later — the same control, inside a 984-unit page).
- Cause: uGUI measures a layout group's child by walking the child's own content, and content that is
  STRETCHED inside the box reports the width the group just wrote — so asking for a field's preferred
  size reads the group's own output and the number grows every frame. "The engine measures, CUO declares
  only a floor" asks the engine to measure the one thing that cannot be measured.
- Change: DECLARE the width (a caption TMP measured from its text, a prefab's authored size, the layout's
  minimum under the model's floor) and place a control's insides one rect at a time. When a row's own
  label and control must still be laid out by a group, that group may only hold children whose width CUO
  has already written down.
- Also: judge a visual change on a frame BEFORE trusting the geometry — the first attempt at the control
  style put the game's own nine-slice sprite inside the control as its border and produced a white box
  with no text, because that sprite's body is opaque; the frame said so immediately while the rects all
  read correctly.

## 2026-10-01 — A forced state must be written where the game derives it

- Symptom: the sound batch's first pain probe wrote `Body.averagePain = 100` and `PantSound.painTime = -1`;
  no pain clip played on any client, while the same probe rewritten to write the limbs' pain produced two
  `Pain` replays immediately.
- Cause: `Body.averagePain` is recomputed every frame as the maximum of the limbs' pain
  (`Body.cs:2800-2808`), so the write was overwritten before `PantSound.Update` read it. The field is an
  output, not an input.
- Change: a forced-state probe writes the INPUT the game derives from (the limbs' pain), and the batch's
  `pain-force.cs` probe is the recorded shape. The same rule already covered the body-force recipe's own
  fields, which the game reads rather than derives.

## 2026-10-01 — A relative cell read names a different cell on every client

- Symptom: the third client's read of "the host's cell" (dx/dy relative to its own body) returned air where
  the host and guest held damage, and it read like a world divergence; re-reading with the offsets
  recomputed from each body's own cell and the returned `cellX`/`cellY` checked showed all three clients
  identical.
- Cause: the `block-read`/`block-hit` recipes compute the cell from the LOCAL body's position; the three
  bodies drift independently (spawn push, holes, slides), so one dx/dy pair names three different absolute
  cells.
- Change: a cross-client cell comparison recomputes dx/dy from each client's own cell and asserts the
  recipe's returned cell against the intended absolute cell before any value is compared; a whole-row
  comparison uses one absolute-coordinate probe (`world-row.cs`) instead of per-client offsets.

## 2026-10-01 — The sound family's evidence is a Debug line, so the run sets the level live

- Symptom: the run's first log read carried no `[CharacterSound]` lines; the CUO log level was the default
  `Information`.
- Cause: the receiver's evidence — `[CharacterSound] replayed {Kind} {Clip} for owner {Owner}` and the
  `[ItemImpact] reported` / `replayed` pair of the item-impact carrier — is written at `Debug`.
- Change: the run sets `Logging.MinimumLevel` to `Debug` on every client through the same editor the
  Preferences control writes (`LoggingConfigEditor.Set`, the batch's `loglevel-*.cs` snippets), reads the
  CUO rolling log (`BepInEx/logs/latest.log`, rotated to `yyyy-MM-dd-N.log.gz` on the next start), and
  restores `Information` before closing. The meal-end burp needs its own arming too: a food's own `Eat`
  arms it only above hunger 90 and with a 10% roll, so that row is driven through `Body.Burp` — the game's
  own arming method — and the record names the substitution.

## 2026-10-01 — An entity census must match the type, not the name

- Symptom: a name-substring census (`Trap`, `Enemy`, `Spider`) reported zero traps on a layer that held
  18 of them; `ConsoleScript.FindObjectsWithName` (the game's own `locate`) searches names, and the live
  objects are named `beartrap(Clone)`.
- Change: a census counts by component type (`FindObjectsOfType<BearTrap>(true)`) and the driven target is
  found through that component, never through a guessed name.

## 2026-10-01 — A relative cell read right after a Continue can name a different cell

- Symptom: the same `dx=5,dy=0` read returned cell `(517,948)` after the first restore and `(517,512)`
  right after the second, while `dx=5,dy=-4` stayed `(517,944)`; the body was still falling from the
  entry point on the first read.
- Change: a post-Continue cell comparison waits for the body to settle, or asserts the recipe's returned
  `cellX`/`cellY` against the intended absolute cell before comparing values (the rule the three-client
  note already records).

## 2026-10-01 — The one-shot fingerprint is not re-armed by a Continue

- Symptom: the batch's fingerprint procedure asks for a post-mutation re-capture, but each client's log
  held exactly one `[WorldFingerprint]` line — the world-entry pair — across four Continue cycles.
- Cause: the fingerprint is logged once per world entry and re-armed on session end, which a Continue
  inside the same session is not.
- Change: a re-capture needs a fresh session entry (or a periodic fingerprint feature, explicitly out of
  scope); the run records the bounded block reads as the substitute and leaves the step unproven.

## 2026-10-01 — "written" in the log is not proof the live field kept the value

- Symptom: the layer-timer handover logged `[RunFacts] … the layer timer 373.9s (written)` and the live
  `WorldGeneration.world.layerTimeSpent` read `0.4` a few seconds later, then accumulated from zero.
- Change: a native-field row reads the live field after the write before it is judged; a handover log line
  is evidence that the write ran, not that the game kept it.

## 2026-10-01 — A new run does not move lastOpenedWorldId

- Symptom: the run started a fresh run, cut it, and hit Continue expecting that run; the continue
  restored the repository's previous `lastOpenedWorldId` instead (a batch-m world), so the forced
  360-second precondition sat in a world the continue never read.
- Cause: only the restore/continue paths write `WorldRepository.SetLastOpenedWorld`; starting and
  leaving a run records its cut but leaves the pointer alone.
- Change: a continue-scenario run either accepts the world the pointer names (the clean cycle then cuts
  and continues THAT world in-session) or moves the pointer first; the first continue's evidence is kept
  as a bonus observation rather than counted as the row.
- Update 2026-10-02: a run's own world now becomes the Continue target on its FIRST committed cut
  (`WorldSaveService.OnCutReported`), so the workaround above is retired for the normal case — the pointer
  moves once per world, and a picker selection made after that first cut still stands.

## 2026-10-01 — An injected archive edit surfaces in the manifest check

- Symptom: dropping the optional `layerTimeSpent` property from the live snapshot to stage the "archive
  without the row" shape produced `ChecksumMismatch in run.json: the file is 22289 bytes but the
  manifest says 22326` in the restore report.
- Cause: the archive manifest records the row files' size and hash, so a hand edit is visible by
  construction.
- Change: the edit is declared as the setup in the record, its restore-side account (one content loss)
  is quoted, and the row is judged on the decoder/handover lines and the live restart it produces —
  never presented as an unmodified archive.

## 2026-10-01 — A resumed layer timer is judged on its post-generation write

- Symptom: after the fix the applier logged `the layer timer 364.8s (written) — the world finished
  generating` twelve seconds after the restore, and the live timer then read `364.77`; the pre-fix shape
  wrote before the generation and the live value read `0.4` instead.
- Cause: `FinishWorldGeneration` zeroes `layerTimeSpent` on its first line, so the surviving seams are
  the ones that run after the generation — the world-entry edge and an in-world message apply — while
  the native save slot's write is erased.
- Change: a resume row is judged by the pairing "cut value → the world-finished-generating write line →
  a live read that starts at the cut value"; a bare `written` line taken before the generation is not
  evidence for this row.

## 2026-10-01 — A compressed facts file can blind the preflight

- Symptom: `preflight.ps1` reported `no 'acceptance environment' section resolved` and `FACT-MISSING`
  for every key while the facts file plainly carried the section; once that was fixed it reported
  `game-app-id` as contradicting the install, because a parenthetical note had been folded onto its
  value.
- Cause: the previous cycle's compression of `docs/acceptance/AGENTS.local.md` dropped the ASCII half of
  the section marker (`## 验收环境 / acceptance environment` → `## 验收环境`) and re-glued the
  `game-app-id` note onto its value. Both are interfaces the tooling reads literally, and the lesson
  that recorded them (2026-09-27) was already in this file when the file was rewritten.
- Change: the marker and the bare value are restored, the note stands on a non-key line, and the run
  re-ran the preflight after editing the file instead of assuming it. A gate over the facts file's shape
  is not built yet — until then, re-run the preflight after every edit to that file.

## 2026-10-01 — The repair group re-sends the last published value, not a fresh one

- Symptom: a guest joining a run in progress read its run clock 30.0 s behind the host's (85.6 s on a
  later rejoin), and the gap did not move across a 58 s observation, while members that entered with the
  restore matched the host within 0.5 s.
- Cause: `RunFacts` carries the host's last PUBLISHED base (published at the host's own world entry and
  at generation boundaries); the 60 s repair group re-sends that same absolute value, and the receiver's
  monotone write guard keeps a lower value. The joiner's own counter starts at its own world entry, so
  it is short by the interval between the host's last publish and that entry.
- Change: a row about a "current" value that arrives as an absolute base is judged by a live comparison
  of both ends at the same moment, never by the arrival of a base; the failing row carries the fix
  direction (the publish point, not the value).

## 2026-10-01 — A race fixed for one ordering failed in the ordinary one

- Symptom: the starting-supplies row that must not grant for a player the world HAS a character for
  failed live: the guest's client announced the grant 38–338 ms BEFORE its stored character arrived,
  five times in one session, while the same run shows the opposite order taking the correct branch.
- Cause: the landed protection checks the restore queue at the grant moment — it covers "the restore is
  already queued", not "the restore has not arrived yet", and the latter is the ordinary in-session
  order (the guest's entry pump runs before the host's character delivery lands).
- Change: a race fix whose branches depend on message arrival order is judged by a run that produces
  each order; this row stays red and the ticket returns to `todo/` with the failing ordering named.

## 2026-10-01 — A row that says "no file on disk" has to look at the disk

- Symptom: the legacy-store row's precondition ("no `character-data.bin` on disk") was false — a
  pre-retirement file (last written 2026-09-06) still sat in the install's config directory, untouched
  by the current code.
- Cause: the code that wrote it was deleted, but nothing removed the file it had left on this machine,
  and no earlier run had checked the precondition's own disk state.
- Change: the run compared the file's mtime against the retirement date, renamed it aside as the
  declared setup, re-ran the reconnect cycle and confirmed nothing recreated it. A "file must be absent"
  row starts with a disk listing; a stale file it finds is setup the run owns.
## 2026-10-01 — A deny-delete ACL does not stop the game's prune; the read-only attribute does

- Symptom: an archive protected with `icacls <file> /deny "<user>:(D)"` was still deleted by the
  client's retention pass (`Pruned 1 old backup(s)`), while the same deny blocks a PowerShell deletion
  of the same file; setting the file's read-only attribute instead made the pass report
  `1 could not be pruned` and the archive stayed.
- Cause: the two deleters are not the same security context — the injection has to be the one the
  PRODUCT's delete respects. The read-only attribute is enforced by the delete call itself, whatever
  the token.
- Change: a pruning-failure injection sets `IsReadOnly` on the oldest archive
  (`.acceptance/batch-p/run-c-log.txt`, lines for 12:29:23 and 12:29:24); the run reads the failure
  back from the product's own account line before judging, and clears the attribute to prove recovery.

## 2026-10-01 — A checksum mismatch is salvaged; the decode refusal needs the run baseline

- Symptom: appending a byte to `live/items.json` produced `restored with damage: 2 item(s) reported`
  (the §6 per-entry salvage) instead of the refusal-and-promotion the row was after.
- Cause: salvage handles a payload that still parses; the snapshot decoder only refuses when the run
  baseline cannot be read (`WorldSnapshotDecoder.Finish` — `the snapshot has no readable run
  baseline (run.json)`).
- Change: a decode-level-refusal row corrupts `live/run.json`, not a payload file
  (`.acceptance/batch-p/run-c-log.txt` at 12:32:10); the record names the file it damaged and quotes
  the refusal line.

## 2026-10-01 — BepInEx 5.4's ConfigFile has no file watcher: a text edit of the .cfg is not a hot-reload

- Symptom: editing `CasualtiesUnknownOnline.cfg` on disk (interval 10 → 1) left the live
  `IOptionsMonitor<SaveOptions>` reading the old value (`IntervalSeconds 600`); setting the same value
  through the plugin's own `ConfigEntry` took effect at the next decision (the next cut used it).
- Cause: `BepInExOptionsMonitor` updates on `ConfigFile.SettingChanged`, and BepInEx 5.4's assembly
  carries no `FileSystemWatcher` — an external file edit is only read at a reload or restart.
- Change: a "config edit hot-reloads" row names the edit PATH — the run drives edits through the
  config system's own entry (what an in-game config editor does) and records the on-disk path as a
  limit (`.acceptance/batch-p/p-saveoptions-after-disk-edit.json`).

## 2026-10-01 — The Worlds page's backup section lives below the world list; scroll the window's own ScrollRect

- Symptom: clicking `Backups` registered the archive controls and a restore id, but the captured frame
  showed only the world rows — with thirteen worlds the archive section sits below the viewport.
- Cause: the window's page band is a real `ScrollRect` (`Scroll`, under the CUO Online UI Window) and
  a window capture shows only its viewport; the page had scrolled to the top.
- Change: an in-process eval sets the ScrollRect's `verticalNormalizedPosition` (1 = top, 0 = bottom)
  before a capture — no OS input (`.acceptance/batch-p/scroll-*.cs`,
  `p-worlds-backups-scrolled.png` / `p-worlds-marker-857c.png`).

## 2026-10-01 — When the pre-restore archive cannot be written, the promotion keeps the replaced folder as the only copy

- Symptom: the decode-refusal recovery logged `the pre-restore archive of … could not be written.
  InvalidDataException: archive entry 'run.json' is 10 bytes but the manifest says 22323`, then
  `the refused live snapshot is preserved at damaged-20261001-043214`; the backup was promoted and the
  world loaded.
- Cause: the promotion archives the snapshot it replaces best-effort; a corrupt live snapshot cannot
  round-trip into an archive, so the preserved folder becomes that state's only copy — the fallback
  the promotion documents.
- Change: a damaged-live row quotes the account line that names which copy exists
  (`its preserved folder is the only copy`) instead of expecting a pre-restore archive in every case
  (`.acceptance/batch-p/run-c-host-console-after-refusal-recovery.json`).

## 2026-10-01 — A HotRepl eval runs on a worker thread, so it cannot stop the game's own frame

- Symptom: batch `20261001-r` sent `guest-freeze.cs` (a 30 s `Thread.Sleep` inside the eval) to stop
  the guest draining its Steam receive queue; the eval call hung and the guest kept playing normally.
- Cause: the evaluator executes on a worker thread. The Unity main thread — the game's `Update`, the
  transport pump and the watchdogs — never entered the sleep, so nothing the scenario needed changed.
- Change: an injection that must stop the game's own frame goes through the code that frame runs (a
  Harmony prefix, or a value the frame reads such as `SteamService.IsInitialized`), never a
  `Thread.Sleep` inside an eval. Recorded with the batch in `.acceptance/batch-r/run-r-log.md`.

## 2026-10-01 — A suspended peer refuses everything but cannot build a sustained stall episode

- Symptom: `suspend-process.ps1` (`NtSuspendProcess`) on the guest produced
  `k_EResultLimitExceeded` immediately, but the session read
  `k_ESteamNetworkingConnectionState_Connecting` for the rest of the window, and
  `AutoRestartBrokenSession` let one send succeed per restart, so each recovery cleared the episode.
- Cause: a suspended process stops answering Steam's callbacks for the whole connection, so the link
  breaks instead of staying up and unable to drain. The shape a stall row needs is a peer that is
  *connected but not consuming*, not a peer that is gone.
- Change: a stall scenario keeps the connection alive (stop only the transport pump, or overload the
  peer's own frame) and records the link state it was in; the auto-restart behaviour is a named limit
  of the suspend injection, not a product finding.

## 2026-10-01 — Send-queue refusal is a rate condition; a light session never reaches it

- Symptom: with the guest's transport pump stopped, the host's refusals were intermittent (31 → 38
  lines over the following minute) instead of continuous, while the same failure had arrived
  thousands of times per second in Run E's dense world.
- Cause: `k_EResultLimitExceeded` needs the sender's rate to exceed what the link drains for as long
  as the episode takes to ripen; a fresh session's ≈56–62 kB/s (measured that day) refills the queue
  more slowly than Steam drains it, while Run E's dense layer pushed ≈1.5 MB/s.
- Change: load generation is part of the run's setup, with the host→guest rate as the checkpoint —
  reproduce the dense shape (the game's own animal spawns, or a fresh dense layer) and measure
  hundreds of kB/s before injecting the non-draining peer.

## 2026-10-01 — `SteamService.IsInitialized` is a clean, reversible pump switch

- Symptom: the run needed a guest that stays in the session while it stops consuming; the two heavier
  injections (main-thread sleep, process suspension) both failed for their own reasons.
- Cause: `SteamTransport.Poll` returns early and `SendTo` returns false when the service's
  `IsInitialized` is false, while the Steam client keeps the P2P session itself — flipping the
  property's backing field stops both pumps without touching the session.
- Change: `guest-pump-off.cs` / `pump-on.cs` are the recorded injection pair (batch `20261001-r`), and
  the guest's own watchdogs still observe the silence — which is how the same injection judged row 6;
  a sustained-stall run pairs it with the dense load above.

## 2026-10-01 — A peer that stops reading does not back-pressure a healthy Steam link

- Symptom: batch `20261001-s` stopped the guest's transport pump with a dense host→guest flow
  (≈231 kB/s) and the host pushed >20 MB into that peer over 100 s with **zero**
  `k_EResultLimitExceeded`; the only effect was the guest's own silence watchdog ending its session
  after 15 s, after which the host kept sending to a member it still believed was there.
- Cause: an application that stops calling `ReceiveMessagesOnChannel` does not close the peer's queue
  on a healthy connection — the send path keeps accepting — so "the peer cannot drain" is not, by
  itself, the state `k_EResultLimitExceeded` reports. The refusal state needs the transport-level link
  to stop acking (a peer process blocked at that level), which on this machine only suspension
  produced — and suspension collapses the session to `Connecting` within seconds.
- Change: a run that needs a sustained refusal episode has to produce a peer blocked at the transport
  level while its session stays connected; stopping the application's drain is not a substitute.
  Recorded in `docs/evidence/acceptance/steam-transport-send-limit-runaway-20261001-s.md`.

## 2026-10-01 — The adaptive stream budgets cap the load a run can generate

- Symptom: the dense world's send rate plateaued at ≈200–250 kB/s however many animals were spawned
  (589 animals, enemy frames of 46–70 kB), and a guest pinned to one of twenty logical cores still
  drained that rate and stayed in the world.
- Cause: `AdaptiveRatePolicy` caps each adapted stream by its profile's `MaxBytesPerSecond`
  (`EnemyStateBroadcast` 256 kB/s, `WorldItemMoveStream` 512 kB/s, …), so adding rows raises the
  per-frame bytes and the policy answers by lowering the cadence; the product of the two stays under
  the budget. A run cannot raise the total by spawning more of one kind unless another stream is also
  driven toward its budget.
- Change: before designing a load scenario, read the profile budgets of the streams it will drive and
  state which stream is expected to carry the rate; "denser world" alone does not imply a higher send
  rate. Machine readings are in batch `20261001-s`'s record and its `run-s-log.md`.

## 2026-10-01 — An abrupt guest session end leaves the host a stale member that a rejoin does not revive

- Symptom: the guest's silence watchdog ended its session and returned it to the main menu, while the
  host's logs showed no member-left line and its traffic monitor kept addressing the peer for minutes;
  a `join-lobby` on the guest then returned the same inactive session.
- Cause: the guest's local end does not tell the host (the send path to a peer that stopped reading is
  exactly what does not work), so the host's member table keeps the entry; the rejoin then hits the
  known rejoin wedge (recorded 2026-09-30) and never re-activates.
- Change: after an injected session end, close and relaunch the clients for the next scenario instead
  of trying to revive the pair, and record the stale-member window as a limit of the ended session,
  not as evidence. Observed in batch `20261001-s`.

## 2026-10-01 — An offline row that names a measurement is re-derived, not waived

- Symptom: batch `20260927-b` classified seven tickets `offline` and left them in `review/` because
  their deciding evidence was not a suite result — a comparison against a pre-change revision, a
  lifetime assertion, a `< 50 ms` bound, a byte measurement.
- Cause: "offline" means the repository can produce the evidence, not that a named test already
  produces it; a claim about a measurement or a pre-change state needs the run to reconstruct that
  state (`git show <revision>:<path>`) or take the measurement itself.
- Change: batch `20261001-t` re-derived all seven — the index/matrix measured tables and the
  registration multisets from `git show`, the checkpoint sizes and the composition build time from a
  run-local probe whose output lands in the batch directory (the probe file is deleted before the
  final ladder). The method is recorded in `docs/evidence/acceptance/20261001-t-scope.md`.

## 2026-10-01 — A measured table is re-derived at its own revision; a figure that does not match is named

- Symptom: the matrix ticket's measured table re-derived six of seven figures exactly (data-row text,
  E3 row, longest row, line count, JSON count, after-bytes), but the recorded before-bytes (250,032)
  does not match the blob at the revision the table names (`9bc8dea7`: 250,647 bytes LF / 251,076
  CRLF).
- Cause: a recorded figure can be a stale or differently-measured reading (bytes vs characters, LF
  vs CRLF, a mid-edit working copy), and no check in the tree holds historical numbers.
- Change: the acceptance record names the discrepancy and passes the row on the re-derived figures
  (`docs/evidence/acceptance/evidence-matrix-fat-rows-split-20261001.md`); a re-derivation names its
  revision and its measurement form, and a mismatch is recorded, never smoothed.

## 2026-10-01 — A fact check names the owner it found, not just pass/fail

- Symptom: the backlog-index fact check found 91 of 95 fact tokens in the exact ticket the old row
  pointed at; the other four (the `S3`/`S3.3` stage tags and decision 181) live in the family's
  umbrella or in the sibling the row itself cites.
- Cause: an index row cites a ticket family, and stage or decision vocabulary is owned by the umbrella,
  so a literal "in its ticket" reading reports a loss where the information is one hop away.
- Change: the acceptance record passes the row and names the four exceptions and their owners
  (`docs/evidence/acceptance/backlog-index-summary-duplication-20261001.md`); a mechanical check has to
  say where each unmatched token actually lives.

## 2026-10-01 — A HUD widget can be live, readable and still absent from the window capture

- Symptom: the run's frames at 5× and 1× showed the world and the game's other HUD parts, but the region
  the speed indicator's own RectTransform names was empty; the widget read `x5` with icon 1 lit and `x1`
  with icon 0 lit on BOTH clients, and `ScreenCapture.CaptureScreenshot` wrote no file from either process.
- Cause: the widget is active, white and laid out exactly where the transform says, but the window-level
  `PrintWindow` capture does not composite it; the game's own screenshot API is not a substitute here.
- Change: judge an indicator row from the widget's live objects (its text and its icon colors) and classify
  that row `machine`, not `visual`; keep the frames as context and say in the record why the frame cannot
  carry the row. The speed SOUND stays a residual — no frame carries audio.

## 2026-10-01 — A decimal recipe argument passes the gate and fails in the client

- Symptom: `time-scale-direct -RecipeArg scale=7.5` answered `eval-error (1,1): InteractiveHost` while
  `scale=20` and `scale=7` ran; the offline gate was green for the same file.
- Cause: the driver's numeric check accepts a decimal and substitutes it literally, and the gate's own
  substitution uses `0` — so the decimal form is never exercised offline, and the client's compiler
  refused the submitted snippet at run time.
- Change: exercise a numeric recipe with an integer, or have the recipe format the value itself; record
  the failed form as a limitation rather than re-running until it passes. The driver's numeric contract
  deserves its own negative sample offline.

## 2026-10-01 — The sleep gate needs bodies held down, not just consciousness 0

- Symptom: `body-force consciousness=0` (leaving `brainHealth` alone) produced a body that read
  `conscious:false` in the same call and `conscious:true` a few seconds later; the all-unconscious gate
  never applied its 25× and the run's first sleep block had to be thrown away.
- Cause: the game's own sleep simulation restores a healthy body's consciousness within seconds; the
  recipe's own header already names the shape that stays down (`brainHealth=1 consciousness=0`).
- Change: a sleep-gate scenario sets `brainHealth=1` with `consciousness=0` on EVERY client, waits for the
  1 Hz character data to reach the host, and reads both ends; restore with `brainHealth=100` afterwards.

## 2026-10-01 — A late joiner is a lobby rejoin, not a re-entry control

- Symptom: after `leave-world` the guest sat in the lobby (role Guest, out of world) and its Online UI
  Home page offered no control that enters the host's running world — only `home.copy_lobby_id`,
  `home.leave` and `home.open_players`.
- Cause: a guest's world entry is driven by the host's fan-out, so the client that wants back in has to
  come back through the lobby; there is no local "enter world" button to press.
- Change: the late-joiner setup clicks `home.leave` (which empties the lobby; `leave-world` alone keeps it)
  and then runs `join-lobby` with the same id. Verified with a 5× acceleration standing: the rejoining
  guest's log read `Scene state: InWorld (SampleScene)` and `World-time broadcast: Fast.` 30 ms later,
  then the same broadcast again 3.3 s on (the resend), and both ends read 5×.

## 2026-10-01 — A WorldJoin starts every handshaken member, so a parked lobby client cannot hold the gate

- Symptom: batch `20261001-v`'s first plan parked the third client in the lobby so the host's start gate
  would wait for it; at the host's world entry both guests' scene-state reports already read InWorld
  (their own generations had started from the host's `WorldJoin`), arming found `No confirmed members
  waiting` and released the gate in the same second.
- Cause: the host's click sends `WorldJoin` to every handshaken member and each guest starts its own run;
  the world-entry edge re-invites members that missed the click, so a lobby member is not a member "not in
  world" for long enough to hold the gate.
- Change: hold the gate with `leave-world` on a member plus the production arm
  (`tools/acceptance/recipes/gate-arm.cs` -> `IWorldControl.StartStartGate`); the gate then waits for that
  member and its own 30 s fallback releases it. Recorded in
  `docs/evidence/acceptance/world-time-local-initiation-20261001-v.md`.

## 2026-10-01 — A recipe whose expression ends at the cast returns the delegate

- Symptom: the new request recipe's eval answered an empty value and the driver failed with
  `Invalid JSON primitive: .`; the raw HotRepl frame showed the result object was the delegate itself —
  the snippet had ended at the cast, without the trailing invocation.
- Cause: the offline gate parses a recipe for syntax and its argument contract only, and a missing `()`
  still parses — it simply returns the lambda. The driver's parse then reports a protocol-level failure
  that names nothing about the real cause.
- Change: every recipe ends with `}))()` and a new recipe is smoked through the live evaluator (one
  invocation) before the run; when a driver answers `driver-error` on an empty value, read the raw frame
  before suspecting the channel.

## 2026-10-01 — The start gate's 30 s fallback is wall-clock and runs while the world is frozen

- Symptom: a request sent 26 s after the gate was re-armed was still answered by the gate guard; the arm
  line and the `Start gate forced after 30 s` line bracket exactly 30.0 s while the gate held the host's
  world frozen (`StartGateCoordinator` writes `Time.timeScale = 0` for the hold).
- Cause: `WorldStartGate.PumpTimeout` compares `ITimeSource.NowMs` (`SystemTimeSource` =
  `Environment.TickCount`), which keeps advancing at timeScale 0, and the host's pump keeps running.
- Change: time a gate-guard instance against the arm's own wall clock and cite the arm line plus the
  `forced after 30 s` line as the hold's bounds; do not assume the freeze stops the fallback.

## 2026-10-01 — A direct-write row needs its site reachability, not just its name

- Symptom: the open scene-reload instance was planned as "a layer end", but the layer-end path
  (`WorldGeneration.ContinueRun` → `RegenerateWorld`) never touches the `WorldGeneration.cs:1036`
  write, and `ReloadScene` itself has no caller in the decompiled assembly and no reference in the
  shipped `CasualtiesUnknown_Data\*` files — a row built on the layer end would have measured a path
  that does not contain the write.
- Cause: the site was named from the code that contains it ("scene reload") without a caller census,
  so reachability was assumed from the name.
- Change: before designing a scenario for a write site, run the caller census and the shipped-data
  string scan; an unreachable site is exercised through a declared forced recipe of the game's own code
  (`tools/acceptance/recipes/scene-reload.cs`) and the record carries the reachability half beside the
  live half.

## 2026-10-01 — The gate window ends at its release line, and the pump's first act after it is an adoption

- Symptom: the reload window logged no `host direct timeScale write … adopted` line, but 6 ms after
  `Start gate released — everyone is in the world.` the pump logged `host direct timeScale write 1
  adopted as Normal.` — the world's standing 1× (the same value the gate's release branch writes)
  reaching the ordinary value-blind rule.
- Cause: `WorldTimeSync.Update` returns while the start gate waits, and the gate's own release writes
  1×; the pump resumes on the next frame and adopts the world clock, as decision 226 requires.
- Change: a "the pump returns before the rule" row cites the arm and release lines as its window and
  reads a post-release adoption as the ordinary rule, not as the write reaching the rule inside the
  window.

## 2026-10-01 — Two "parallel" tool calls are not a same-instant race

- Symptom: the first two-sender race attempt issued the two block actions from two parallel shell
  calls; the host's relay had already reached the guest before the guest's call ran (`blockBefore`
  read 0), so the guest produced no report at all and no race existed.
- Cause: the two calls' dispatch difference (hundreds of ms across two shells) is larger than the
  relay's travel time, so the second sender always acts on an already-changed world.
- Change: the batch's helper opens BOTH evaluator sockets first and sends both evals in ONE
  event-loop tick; every race round after that landed two writes in one window.
  `.acceptance/batch-x/x-race.mjs` is the shape (a local artifact; general enough to move into
  `tools/acceptance/` when a batch stages races again).

## 2026-10-01 — A substituted numeric literal is a double, and the REPL only says `InteractiveHost`

- Symptom: a teleport template that substituted coordinates into `new Vector3(x, y, z)` answered
  `eval_error: (1,1): InteractiveHost`, a message that names nothing; the same template with integer
  arguments worked.
- Cause: a literal like `-106.5` is a `double` in C#, Unity's `Vector3` takes `float`, and the
  submission fails to compile — the evaluator reports the failure at the expression's first column.
- Change: a numeric template casts explicitly (`(float)(__TX__)`); when an eval answers
  `(1,1): InteractiveHost`, suspect the substituted literals' TYPES before the logic. The same run
  also showed that two evals in flight on ONE client collide (one `client timeout`, one
  `InteractiveHost`): a single-client action is one eval on one connection.

## 2026-10-01 — A same-eval read after a break is too early for the drops

- Symptom: a probe that broke a pad's support and listed the area's items in the same eval found no
  new item; the host's own log shows the drops appearing tens of milliseconds later (`OnBlockDamaged`
  → `OnItemInstantiated` → `FlushPendingBlockBreak` inside 37 ms).
- Change: the probe polls (40 × 100 ms) for a new item id and returns the first snapshot that has one,
  so the fresh-drop component is read in that snapshot.

## 2026-10-01 — The fresh-drop flag was never caught, and the component's own setting is a limit

- Symptom: three catches (a 4 s poll on the breaker right after the break, and a peer-side watch on the
  observer) all read `fresh:false`, while the same drops carried an initial spin (`av=-0.156`).
- Cause: `FreshItemDrop` self-destroys ("the glowing floating pickup effect (self-destroys when the
  setting is off)"), so the flag is observable only inside a short window and can be absent by design.
- Change: the visual rows stay `unproven` and name the limit; a future run that judges them arms the
  effect's own setting first and captures a frame sequence, instead of reading the component.

## 2026-10-01 — A leave→continue recovery keeps the block differences and changes the item set

- Symptom: after the host left the world and continued, the seven cells the batch had mined still read
  air on all three clients, while the drops created before the recovery were gone and one new item
  stood in the same area (identical on all three clients).
- Change: a row about what the world holds is judged against reads taken on the SAME side of a
  recovery; the batch's trap-drop row 1 is judged from its pre-recovery sample only, and the
  post-recovery item set is recorded as an observation.

## 2026-10-01 — The rejoin wedge reproduced twice more: a lobby rejoin does not re-activate the session

- Symptom: both guests died to traps; their lobby rejoin left `active:false, inWorld:false`
  ("Waiting for the host to load…"), and a clean relaunch that rejoined the same lobby stayed inactive
  as well. The world only came together when the HOST left and continued — its world-entry fan-out
  pulled both back in.
- Cause: still unnamed (the 2026-09-30 entry recorded the same shape as an unreproduced intermittent);
  this run adds two observations, both on a rejoin into a RUNNING world.
- Change: the reconnect row stays `unproven` with the observations named, and a batch that needs a late
  joiner plans the host's re-entry as the delivery path — or verifies that the rejoin actually
  re-activated the session before any row is built on it.

## 2026-10-01 — A harvest written while the review runs breaks the freeze the review depends on

- Symptom: this batch's lessons harvest and its run log were written while the independent review was
  still running; the reviewer's own `git status` saw `docs/acceptance/lessons.md` appear mid-review and
  it had to judge which tree it was reviewing (its F-11).
- Cause: "freeze the working tree" was read as "do not touch the files under review", while the
  reviewer's contract is the WHOLE tree at the moment it started.
- Change: while a review runs, the only writes are gitignored artifacts. The harvest, the run log and
  every other tracked edit wait for the report; the reviewer re-ran its ladder after this change and
  still passed, but that was luck, not design.

## 2026-10-01 — The fresh-drop presentation is gated by the game at 8 units from the destroyer

- Symptom: three pads destroyed from 9.1, 35 and 5.2 world units away produced drops that read
  `fresh:false` on every client, then `fresh:true` on every client the moment the destroyer stood inside
  the radius.
- Cause: `BuildingEntity` attaches `FreshItemDrop` only when
  `Vector2.Distance(transform.position, PlayerCamera.main.body.transform.position) < 8f`
  (`BuildingEntity.cs:74`, applied at `:84/:107/:118`); the component then self-destroys after 10 s
  (`FreshItemDrop.cs:15,49-52`). A `fresh` row needs BOTH the breaker inside that radius and a read
  inside that window.
- Change: the drop family arms the game's own `itemfloating` setting (`item-floating value=1`), places the
  breaker's body inside the 8-unit gate with `body-place`, breaks, and reads within the window
  (`item-watch`'s 100 ms poll; the probe `y-fresh-screen.cs` returns the item's camera screen point so the
  frame can be cropped where the highlight must be).

## 2026-10-01 — Destroyed-entity drops need a body near the entity

- Symptom: a jump pad whose support was broken from 35 cells away vanished and produced NO drops; after a
  body was moved next to its position, its two drops stood there.
- Cause: the building-death drop loop runs on the local simulation of the death; with nobody near, the
  block event landed but the drop spawn did not.
- Change: a drop row's setup places a body beside the entity BEFORE the support is broken, exactly as the
  batch `20261001-x` approach recipe did.

## 2026-10-01 — `crush-find` returning zero is a world-state answer, not a probe failure

- Symptom: `crush-find` answered `candidates: 0, cells: []` at radius 60/120/200 on two clients in two
  layers, with `skippedOutside: 0` — the search box sat fully inside the 1024×1024 world.
- Cause: the only vanilla block types whose `BlockInfo.health` is exactly 1 are `thinice` (28) and
  `powdersnow` (29) (`WorldGeneration.cs:580,590`) — biome tiles the visited layers did not expose.
- Change: the crush family is planned per layer; a zero answer is recorded as `unproven` with the census,
  and the recipe's `skippedOutside` field is what proves the search was not clipped.

## 2026-10-01 — The absolute re-report can be a silent no-op, and that is not a defect

- Symptom: the guest's 140 outstanding cells rode the 60 s pump
  (`Partial block-damage report received from … (140 cells)`), and the host log carried ZERO
  `RefuseCap|RefuseRange|RefuseAir` lines.
- Cause: the live delta had already delivered those cells, so every merge answered `KeepHost` — nothing
  was refused because nothing needed to change.
- Change: a refusal row needs a cell the host's full table cannot take AND never saw live; without the
  swallow injection that state is not reachable, so the row stays `unproven` and the pump's delivery is
  recorded as the half that WAS observed.

## 2026-10-01 — `Settings.Get<T>` works inside the client evaluator, and a client that just changed layer answers `no-world`

- `Settings.Get<SettingBool>("itemfloating")` compiles and runs in the client's Mono.CSharp evaluator
  (the game's public accessor; `Settings.cs:543-567`), so a recipe can arm a game setting through the
  game's own path rather than only writing its static.
- Right after `game-console command=skiplayer`, the guest's first census answered
  `{"ok":false,"error":"no-world"}` and the re-read three seconds later answered `tableCount: 0`: a
  client that is still re-entering is not a failure — re-read before recording a verdict.
- Observation, unjudged: both sandbox clients logged repeated
  `System.ArgumentException: The Object you want to instantiate is null` (`UnityEngine.Object.Instantiate`)
  around the destruction family; no ticket claims it and this batch did not judge it.

## 2026-10-02 — An end-of-line simulation is proven by bytes, not by `git status`

- Symptom: after converting the 36 recipe working copies to the CRLF the attributes declare,
  `git status --porcelain` reported every one of them worktree-modified while `git diff` and
  `git diff-files` were empty and `git status --porcelain=v2` showed identical HEAD and index blob ids.
- Cause: with `core.autocrlf=true` on this machine the status comparison reports a text file modified
  when the index stat cache is stale against the new size while the working copy's clean form equals the
  blob; the attribute (`eol=crlf`) still decides what a checkout writes, so the converted bytes are the
  faithful image of a fresh checkout.
- Change: a run that simulates a checkout records the conversion as bytes — a byte-exact backup with no
  lone CR, a pure-CRLF check (CR == LF) and `git hash-object --path=<path> <file>` equal to the committed
  blob — and restores the backups afterwards, hash-compared per file; `git status` is not the evidence
  for it.

## 2026-10-02 — The shipped game cannot produce a health-1 block, so the crush family needs a declared staged tile

- Symptom: `crush-find` returned `candidates: 0` in every layer batch `20261001-y` visited; the follow-up
  whole-layer census (`probe-health1.cs`) then answered `health1: 0` for depths 0, 1, 2, 3 and 4 (plus a
  regenerated depth 1).
- Cause: the only vanilla blocks whose `BlockInfo.health` is exactly 1 are `thinice` (28) and `powdersnow`
  (29) (`WorldGeneration.cs:576-594`), and `amountOfLayers = 5` is hard-coded (`WorldGeneration.cs:128-129`),
  so the snow biome that defines them sits behind unreachable depths — `skiplayer` from depth 4 wraps to
  depth 1. A tree-wide search finds no writer of ids 26-29 at all.
- Change: a crush run declares the substitution — write the game's own `thinice` id into the three
  foot-level cells with the game's own `WorldGeneration.SetBlock` (the write path CUO relays, so every peer
  applies the placement too) and let `Body.HandleGroundedState`'s native roll run untouched. Pair a census
  probe with a validation half (`probe-blockcensus.cs`: `nonAir` plus the block table's own health rows), so
  a zero answer is told apart from a blind probe.

## 2026-10-02 — Peer-side receipt evidence lives at Debug, and the traffic top list hides small messages

- Symptom: after a staged crush the actor's `ItemTrace` lines showed the roll, but the peers' logs showed
  nothing at Information; the periodic `[NetworkTraffic]` lines list only the top ten message types by bytes,
  and an 18-byte `BlockDamaged` never makes that list in a busy window.
- Cause: the receiving side's presentation (`WorldEventSync` → `RemoteBlockWrite`) logs at Debug on purpose
  ("presented here" vs "found the cell already air"), while the local damage-report hook is silent by design.
- Change: take peer-side receipt evidence with the CUO log level raised to Debug through the same
  `LoggingConfigEditor` the Preferences control uses (`loglevel-debug.cs`), then restore it to Information
  (`loglevel-information.cs`); read the peers' `[BlockBreak] presenting a relayed break at (x,y)` lines.

## 2026-10-02 — A late-joiner plan on the leave→continue path must move the pointer world first

- Symptom: batch `20261002-c` drove the capacity family's late-joiner half by the deferred steps of
  `20261002-b-scope.md` (fill, a member leaves, the host leaves and continues): the continue restored a
  DIFFERENT world than the session had played — the host's table read 0 rows afterwards — while the
  member re-entry route in the same session delivered the identical 128-row set.
- Cause: `continue` restores the world named by `lastOpenedWorldId`, and a new run does not move that
  pointer (the 2026-10-01 entry already recorded this); the session's own leave HAD written a
  `MenuReturn (MidRun)` cut carrying `128 world-block row(s)`, but of the session's world, not of the
  pointer's.
- Change: a late-joiner plan that uses the continue seam must move the pointer to the session's world
  first (the Worlds page's `worlds.select.<worldId>`), or use the member re-entry route: the host stays
  in the world, the member leaves the world and the lobby and rejoins — the host answers the fresh
  handshake with a direct `WorldJoin`, the member's entry edge fires the world-entry fan-out (with
  `Send BlockDamageSnapshot`), and the rejoining member converges on the same set.

## 2026-10-02 — A peer's presentation write IS reported back, and one crush with one peer in world shows it

- Symptom: `unhooked-damage-block-callers` row 4 expected a remote apply to stay silent, but in two
  single-variable runs the host answered the ONLY peer's write reports for the relayed cells
  (`[BlockSync] answered <peer>'s report at (511,976) with the authoritative block 0.`) within ~30 ms
  of the peer's own `presenting a relayed break`, and the peer cleared its pending entry on the answer.
- Cause: not yet identified — the presentation write reaches the host through a path the remote-apply
  guard the ticket recorded does not cover end to end (the offline guest's log and the peer's own foot
  cells rule out a step of its own as the writer).
- Change: the reproduction is the committed probe pair plus one peer in world: place the game's own
  thin-ice id under the host and read the host's `answered …report at` lines; the ticket is back in
  `todo/` with row 4 named.

## 2026-10-02 — The sandbox exception bursts carry a stack only in the full player log

- Symptom: the sandbox clients' `[Unity:Exception]` bursts show one line (a message) in the CUO rolling
  log — not enough to name the throwing call site — while the host has none.
- Cause: the rolling log's Unity listener writes the message only; the full `LogOutput.log` carries the
  stack (`NullReferenceException … GroundBlood.Start ()` was captured there for the alternate).
- Change: a burst investigation reads the FULL log's tail (the file can be hundreds of MB, so read the
  tail by bytes and filter, never scan it whole); ticket `sandbox-client-null-reference-bursts` holds
  the evidence and the first anchor.

## 2026-10-02 — A nested scope hid the caller's RemoteApply, and the fix is the chain query

- Symptom: the row-4 echo (batch `20261002-c`) — a receiving side's presentation write reported back to
  the host — survived a guard written as `CallContext.Current == Origin.RemoteApply`, and the same
  masking had ALREADY been worked around in two other files (a guard deliberately placed before the
  scope push in `CraftingPatches`, a capture scope deliberately not opened inside a remote apply in
  `CaptureScopeGuard`).
- Cause: `CallContext.Current` answers the INNERMOST origin; the damage patch pushes
  `DamageBlockOrigin` on top of the caller's `RemoteApply` for the whole roll, so every
  mutation-attribution guard that asked the innermost origin stopped seeing the remote application —
  the roll's own `SetBlock(0)` was reported as a local player break.
- Change: `CallContext.IsWithin(Origin)` (a chain scan; `LocalAction` answers true only with no scope
  open) is the MUTATION-ATTRIBUTION query, `Current` stays the CLASSIFICATION query, all 15
  remote-apply guards were converted, and `CallContextScopeCompositionGateTests` bans the four
  innermost-origin forms under `src/`.

## 2026-10-02 — The member re-entry route needs the member to leave the LOBBY, not only the world

- Symptom: with the host in the world, a guest that had left the world alone and clicked join again
  stayed at `Starting…` with `inWorld=false`; no `WorldJoin` reached it and the host's start gate waited
  for one player.
- Cause: the host answers a FRESH handshake with a direct `WorldJoin`; a member still in the lobby never
  re-handshakes, so the member's re-entry edge never fires.
- Change: the working sequence is `leave-world` → `home.leave` (state `active=false`, lobby 0) →
  `join-lobby` the same id; the member re-enters and the host's entry fan-out delivers the snapshot
  group. The runbook records the step that must not be skipped.

## 2026-10-02 — `[BlockSync] answered …report at` also names legitimate arbitration, so attribute by cell

- Symptom: while re-running row 4, the host logged three `answered <guest>'s report at (cell) with the
  authoritative block 8` lines in the same window as a guest crush — easy to misread as the echo the
  batch was hunting.
- Cause: the staging substitution writes thin ice through the guest's own `SetBlock`; that local
  placement is reported (correctly — it is a local write), and the host refuses it because a placement
  must land on air while its cell still holds the generated block, so the host answers with its own
  block. The echo's answer names the RELAYED cells of a peer's presentation; this one names the
  crasher's own staging cells.
- Change: attribute an `answered` line by cell AND by the peer's own `presenting a relayed break` line;
  a staging write also does not propagate to the peer, so the two sides' block ids may differ until the
  break's air write lands.

## 2026-10-02 — A re-entry burst does carry a stack frame in the rolling log

- Symptom: the guest's world RE-ENTRY produced the NRE burst the open ticket describes, and the rolling
  log carried more than the message: `[ERR] [Unity:Exception] NullReferenceException` followed by
  `(wrapper dynamic-method) Item.DMD<Item::Update>(Item)`, plus `ArgumentException: The Object you want
  to instantiate is null.` lines.
- Cause: the re-entry window is the staging window the ticket named, and this throw shape writes one
  stack frame line into the rolling log (the earlier "full log only" finding was the alternate's
  `GroundBlood.Start`).
- Change: read the burst from the rolling log at the re-entry window (ticket
  `sandbox-client-null-reference-bursts` now holds this anchor); the full `LogOutput.log` tail stays the
  fallback for a message-only burst.

## 2026-10-02 — A probe's screen point is not a crop coordinate unless the frame is taken with it

- Symptom: batch `20261002-e` censused the drop items with their camera screen points and captured each
  window immediately afterwards, and every crop at the projected point was empty — the marked frame
  (`e-c-guest-marked-zoom.png`) shows neither the local body nor any drop under its mark — while a
  rehearsal crop of batch-y's own frames at its recorded point `(662,254)` is pure BLACK in both its
  fresh and its control frame.
- Cause: `Camera.main.WorldToScreenPoint` answers for the instant it runs, and this game's camera carries
  a per-client lead (the census's own body point sits ~35 px left and ~60 px below the window centre and
  moves between reads); a mapping recorded in an older batch for a different window state is not
  transferable, and a capture taken hundreds of milliseconds away from the census reads a different
  camera pose.
- Change: a crop-based visual row calibrates on an in-frame landmark first (a clone sprite or the name tag
  above it) and only then crops the probe point; when the landmark cannot be found the row is recorded
  `unproven`, never guessed. The batch scope page names the failure and keeps the rehearsal artifacts.

## 2026-10-02 — A body placed onto a jump pad consumes the pad

- Symptom: the pad chosen for the second destruction site disappeared while the three bodies were being
  moved to it (`pad-find`'s pad count fell 122 → 121 and the next read named a different pad), so the
  break had no target.
- Cause: a jump pad is consumed when a body lands on it; `body-place` teleports to a world position, and
  a position inside the pad's own cell lands the body on the pad.
- Change: choose the site from SCANNED standing spots (solid block with three air cells above, 3-7 cells
  from the pad) and re-read the destroying body's distance to the pad right before the break; the site
  finder is `site-find.cs` in the batch directory.

## 2026-10-02 — The drop family's evidence is the breaker's capture plus each peer's materialization line

- Symptom: the family's rows needed a machine half that names the same drop on every client without
  relying on pixels.
- Cause and shape: the destroying client's own log carries
  `[ItemBuildingDeathDrop] local <type> (id …) at (…) captured for pending block-break report` followed by
  `[ItemTrace] … origin=FlushPendingBlockBreak result=Committed(0+N) events=[Break, Drop, BuildingDrop]`,
  and each peer then logs `[ItemSpawn] materializing <type> (id …) at (…), vel (…)` ~90 ms later with the
  breaker's own velocity; `fresh-census.cs` reads the peer's live copy inside the 10 s window and reports
  the same id, world position and velocity.
- Change: a run judges the machine half from that pair plus the census, in BOTH directions (host-triggered
  and guest-triggered), and names the artifacts; the log markers are
  `ItemBuildingDeathDrop`, `FlushPendingBlockBreak`, `ItemSpawn` and `ItemPhysics`.

## 2026-10-02 — The fresh presentation's own clock sets the capture window

- Symptom: planning the capture needed to know how long the presentation stays readable.
- Cause (source, `reversing/Assembly-CSharp/Assembly-CSharp/FreshItemDrop.cs`): `Start` parents a copy of
  the ITEM'S OWN sprite at scale 1.125 with `Special/ItemOutline` one sorting order behind the item,
  `Update` sets the outline's alpha to `clamp01(timeLeft*0.4)` — full alpha for the first 7.5 s, faded to
  0 by 10 s — and `FixedUpdate` raises `gravityScale` from 0 only once `timeLeft < 2`.
- Change: capture inside the first ~7 s (target under 5 s); the float reads as `vy ≈ 0` with an unchanged
  position through the window, and the control frame taken after it MUST show the fall (the alt's census
  read `vy=-5.299` at 11.4 s in this batch).

## 2026-10-02 — A row that asks for zero warnings passed the entry edge and failed the first repair cycle

- Symptom: `enemy-runtime-spawn-classification` row 1, read from the entry-phase log window, showed zero
  `generation spawn pairing failed`; the in-session repair 60 s later logged it on both guests together
  with `mapping=False`, so an entry-only read would have recorded a pass.
- Cause: the row asks for the absence of a warning, and that warning has a periodic trigger, not only an
  entry trigger — the same apply path runs again each cycle and fails once the drive has moved a bound
  copy (host-anchor vs guest-current, all-or-nothing).
- Change: `workflow.md` §6 now requires an absence row to be read from the run mark through at least one
  full periodic cycle of the mechanism that can emit the warning; this page records the case.

## 2026-10-02 — A pre-staged probe had never been compiled and the eval error hid why

- Symptom: the first run of the pre-staged `spawn-animal.cs` probe failed with `eval_error: (1,1):
  Interactive Host` — no code, no line, no name — and every variant of the bad expression returned the
  same message.
- Cause: the probe walked from `UnityEngine.Object.Instantiate(...)` (returns `UnityEngine.Object`)
  straight into `GetComponent<BuildingEntity>()`, which `Object` does not have; the HotRepl error
  surface dropped the compiler detail.
- Change: probes that create entities cast first (`... as UnityEngine.GameObject`) before `GetComponent`;
  when an eval error names nothing, bisect the snippet and send its expressions one at a time instead of
  guessing. `.acceptance/20261002-f/spawn-animal.cs` carries the fix.

## 2026-10-02 — A log read can be taken before the writer flushed, and a stale build can fake a mutation red

- Symptom (two shapes, one batch): (a) `read-log-since.ps1` returned `NO MATCHING LINES` for a window
  that demonstrably held the host's 86 `host bound …` lines seconds later; (b) after reverting a
  deliberately mutated predicate, the focused tests kept failing even though the source on disk was
  correct.
- Cause: (a) the read ran ~2 s after the send, before the game had appended the lines — the script reads
  from the mark's byte offset, so a still-unflushed tail is simply absent; (b) the incremental build had
  not recompiled the Runtime assembly, and the IL still carried `return paired;` (`03 2A`) while the
  source showed the restored expression.
- Change: (a) re-read the window after a bounded wait and never treat a single empty read as "the line is
  absent" — an absence row needs the re-read plus the full cycle (`workflow.md` §6); (b) after reverting a
  mutation, rebuild the owning project with `--no-incremental` and confirm the artifact changed (hash or
  IL) before trusting the tests. The batch-g repair run's evidence was re-read this way, and the stale
  build was a red herring, not a product failure.

## 2026-10-02 — An absence row can be lost by a count that reads the newly-paired number

- Symptom: the accepted repair fix would still have printed `snapshot applied: 0 generated bound, …` on a
  healthy 60 s cycle, one field away from the rejected `0 generated bound, … mapping=False` line an
  acceptance reader judges.
- Cause: the log reported the copies PAIRED THIS CYCLE; a steady-state repair pairs nothing while holding
  the whole baseline.
- Change: report the asserted held count (host facts minus the still-unbound candidates) and pin it with a
  predicate test; when a row's expectation is a reading humans judge, the reading must describe the state
  it claims, not the work the pass happened to do. Review of the fix (fresh context, FULL tier) found
  this before the commit.

## 2026-10-02 — A member that leaves the world keeps its session, and the world-item keyframe follows it into the menu

- Symptom: staging `sandbox-client-null-reference-bursts` reproduced the burst on both sandboxed clients:
  251 `[BrokenItemUpdate] … (world-null) in 'PreGen' …` in one 80 ms window (window 1; window 2's span is
  49 ms), then a live `Item.DMD<Item::Update>` NRE storm (831,352 frame lines; the guest's rolling log
  0.86 MB → 144.30 MB) that ran until the client re-entered the world. Each client materialized 505
  `[ItemSpawn] materializing` lines in three rounds (entry, menu, re-entry) while the diagnostic reported
  only 251 objects — it dedupes per object. The host logged neither.
- Cause: the host's periodic world-item keyframe is a session BROADCAST gated only on `SessionActive`; a
  member sitting in the menu (session alive, world gone) applies it, finds no id hit and no
  generation-time bind target, and materializes the whole table into `PreGen`, where `Item.Update`
  dereferences the absent `WorldGeneration.world` every frame. Every sibling absolute table already rides
  the per-member `member.InWorld` filter; the item keyframe and the item move stream were the two
  broadcasts left.
- Change: finding and fix direction on the ticket; the run is
  `docs/evidence/acceptance/sandbox-client-null-reference-bursts-20261002-h.md`. Staging lesson: read the
  DEDUPED diagnostic FIRST — one line per object names the object, its id, the reason and the scene, which
  the bare stack never does.

## 2026-10-02 — Rows applied before the receiver's world is ready duplicate world items

- Symptom: the re-entered guest ended the session with 505 items against the host's 254 (252 carrying a
  CUO `ItemInstanceId` + 253 id-less generation items; zero duplicate ids on either side; the census probe
  read 504 at its own moment).
- Cause: the same keyframe landed while the re-entering member's own generation had not produced its items
  yet, so `FindExistingAt` found no bind target and copies were materialized; the generation then added
  its own items beside them. Applying a world-state stream must be gated on the RECEIVER's world baseline
  being ready, not only on the row's arrival.
- Change: recorded as the ticket's related finding (cover with the same gate or split).

## 2026-10-02 — An adopt-first landing that never adopts must be removed, and its probe must be read completely

- Symptom: the batch-`20261002-j` re-entry fix shipped a deferred-landing queue whose whole point was
  to adopt a late local object instead of materializing a duplicate; the acceptance probe (the
  re-entering client pinned to 5 fps) deferred the same seven rows at entry and at the re-entry and
  every `[ItemSpawn] deferred rows:` summary read `adopted 0 late`. The seven local objects existed,
  but sat 1.8–3.6 units from the authority row — outside the 1.5-unit adopt tolerance `FindExistingAt`
  and the sweep's guard share — so neither the retry nor the guard could match them, and the queue
  only delayed materialization by 2 s while the keyframe's late-local sweep did the converging.
- Cause: a position-based adopt predicate has a tolerance, and the tolerance — not the waiting —
  decides whether adoption can happen at all. A staging window that shows the wait firing but never
  the adoption is evidence the predicate cannot match, not evidence of a timing problem.
- Change: the queue, its pump, its sweep guard and its unit tests were deleted (`cfbf76d1`), the
  ticket's Fix/Limits/Next-step rewritten to the sweep-only argument, and the acceptance re-run judged
  every row against that build (`docs/evidence/acceptance/sandbox-client-null-reference-bursts-20261002-j.md`).
  Reading lesson: a `-Tail N`-capped extract can silently drop the head of a window — a fresh-context
  review recounted one window from the truncated file and reported a wrong per-client count; name the
  complete extract (or its cap) whenever an artifact is cited as a window's evidence.

## 2026-10-02 — `continue-run` restores the last cut, and that cut can move the session to another layer

- Symptom: the batch `20261002-k` staging of "leave the world and re-enter it" (guest-block rows 6/9,
  runtime-entity rows 3/4, item-creation row 5) instead moved the whole session to layer 1: the host logged
  `Projected kernel run baseline (run 1, layer 1)`, `Character … of the layer-end cut carries a position …
  from the layer being replaced; it is not restored`, and `Captured world baseline … the runtime-entity table
  is reset` — the run's last cut was a **layer-end** cut, so the Continue restored the next layer, not the
  world the run had been in.
- Cause: `continue-run` is the game's Continue entry; it loads whatever cut the run holds, and a layer-end cut
  replaces the layer by design. A world re-entry is therefore not an in-place reconnect.
- Change: read the Continue's first `Projected kernel run baseline (run N, layer L)` line before assuming the
  same world; if the rows need an in-place reconnect, stage it without leaving the world (transport-level
  reconnect) or accept the rows as `unproven` with the substitution named. The batch's records do the latter.

## 2026-10-02 — The one-shot `[WorldFingerprint]` log is re-armed on session end only

- Symptom: after the mutation pass the run tried to re-capture the fingerprint pair by leaving the world and
  Continuing; no client logged a second `[WorldFingerprint]` line (`k-F-fingerprint-only-*.log` empty).
- Cause: `RunCoordinator` re-arms `_worldFingerprintLogged` on session end, not on a world entry/leave; the
  one-shot keeps its first world's line for the whole session.
- Change: a post-mutation fingerprint pair needs a session restart (quit, relaunch, re-join) or a diagnostic
  that re-arms; record the row `unproven` when only a world re-entry is available (row 3 of
  `world-determinism-world-fingerprint` stayed unproven in batch `20261002-k` for exactly this).

## 2026-10-02 — A Continue restore can leave the members' layer-modifier baseline diverged (new finding)

- Symptom: after the layer-end Continue above, **both** members repeated every 10 s
  `[LayerMod] baseline divergence — local segment start … vs host's … (world effects may diverge)`, the host's
  enemy generation set stayed at 74 while the members held 85, and `EnemySnapshot`'s all-or-nothing pairing
  failed on every repair cycle (`generation spawn pairing failed (74 host vs 85 guest generated enemies)` +
  `snapshot applied: -11 generated bound, 0 runtime spawns, mapping=False`). The member-side replay line reads
  `[LayerMod] guest replay index=-1 depth=0`.
- Cause (attributed 2026-10-02): the Continue opened a STALE WORLD — the repository's `lastOpenedWorldId`
  still named `w-20261001-6986` while the run played `w-20261002-62b8`, and nothing moved the pointer when a
  run's world got its first snapshot. The host regenerated the old world's layer 1 while the members
  regenerated the session's layer 0; the layer baselines (`F8A3757E…` vs `30BFAA…`) and the enemy populations
  (74 vs 85) are the two worlds, not modifier or death effects. Filed and now fixed in
  `docs/backlog/done/layer-mod-baseline-divergence-on-continue.md`.
- Change: the divergence is recorded, not swallowed; `enemy-snapshot-binding-recovery` rows 3/4/8 failed in
  that state (`docs/evidence/acceptance/enemy-snapshot-binding-recovery-20261002-k.md`) and are re-run in
  batch `20261002-l`. Lesson for staging: a restore that changes the world is not a neutral environment for
  a late-join row, and a pointer that only a restore or a page selection writes leaves every played run
  behind it. The attribution method that worked: read the local CUO repository itself — `index.json`'s
  `lastOpenedWorldId`, each world's `world.json` / `live/manifest.json` kind+layer, and `live/run.json`'s
  `randomState` base64 — and match it against the baselines the run's own logs applied and reset to.
- Verified 2026-10-02 (batch `20261002-l`): with the pointer fixed, the Continue restored the run's own
  world's mid-run layer-0 cut, all three clients generated from the same decision entry state and applied
  the same modifier with zero divergence, and the enemy rows 3/4/8 bound the full 76-set over four repair
  cycles (`docs/evidence/acceptance/layer-mod-baseline-divergence-on-continue-20261002-l.md`,
  `docs/evidence/acceptance/enemy-snapshot-binding-recovery-20261002-l.md`).

## 2026-10-02 — Log lines that do not mean what they read like (batch `20261002-k`)

- `[EntitySpawn] created {id} at {pos} (creation …)` is logged **unconditionally** after the find-or-create
  block (`EntitySpawnSync.OnRemoteEntitySpawned`), so a repeated 60 s snapshot logs "created" while rebinding
  the existing copy. Do not read repeated lines as duplication; the census/probe count is the evidence.
- `[Enemy] host bound {Kind} {Id}` is written at the first bind, which can run in the same frame as the
  creation — before the entity's `Start` recorded it in `_runtimeAnimals` — so a genuine runtime spawn can be
  labelled `generation animal` once; the snapshot's `N runtime spawns` count on the next cycle is the
  functional truth (this run: 2 runtime spawns after two creations).
- `container-read` rejects the call without `guest=` even in `mode=local` (declared-argument check).

## 2026-10-02 — Cross-client cell reads need the body cell re-derived per read

- Symptom: a fixed `dx/dy` read landed on different cells on different clients once the bodies drifted; a
  freshly computed offset could still miss while the body was falling between the two evals of one helper call.
- Cause: bodies move/fall independently (mining the floor moved all three), and each recipe computes the cell
  from the LOCAL body position.
- Change: helper `.acceptance/20261002-k/read-abs-cell.ps1` reads the body cell, recomputes the offset and
  retries (bounded) until the returned `cellX/cellY` equal the requested cell; use it for every cross-client
  cell comparison. Related: a small explosion at a created entity's position also caught a nearby runtime
  geyser (1.5-unit radius) — name the collateral when a probe is cited.

## 2026-10-02 — Never leave the host out of the world while a member is still generating

- Symptom: the host's `leave-world` landed while both members' layer generation was still running; their
  `[GenStream]` tails then counted host-wait yields as segments (guest 51, alt 43, against the host's 19),
  the last empty segment overwrote the recorded segment start, and both warned `[LayerMod] baseline
  divergence` (local `D6848B6D…` against the host's `692FA317…`) while the host's snapshot corrected the
  modifier afterwards.
- Cause: the generation wrapper records a segment per coroutine yield, and a member waiting for the host
  yields without consuming randomness — the last such yield becomes the recorded start, which the
  layer-modifier replay then decides from.
- Change: wait for `[GenStream] done` on EVERY client (the host's and each member's) before the next
  host-side world transition (leave, Continue, layer switch). Recorded as
  `done/guest-generation-segments-over-host-absence.md`.

## 2026-10-02 — Cross-client world identity: compare the reset hex and the segment fingerprints

- The host's `Generation stream reset to captured baseline (16 bytes: <hex>)` and each member's reset hex
  are the adoptable identity: equal hexes mean the member generated from the host's baseline.
- `[GenStream] segment N: <hex>` lines (Debug) prove stream identity segment by segment — stronger
  evidence than the absence of a warning, and the way to separate a baseline problem from a
  tail/accounting problem.
- A census/health read taken within 60 s of a re-entry can differ by design (the enemy repair cycle);
  take the post-repair read before judging an agreement row.

## 2026-10-02 — The inbound blackout is the executable swallow, and it has an order (batch `20261002-o`)

- The capability the earlier batches lacked now exists and is proven on the machine: the blackout on the
  HOST makes the guest's block report genuinely lost (`net-receive-blackout mode=on` →
  `subscribersAfter=0, parked=true`), the session survives the window, the guest keeps sending and
  re-reporting, and every staged row converged on all three clients after `mode=off`. Rows 1–4 of the
  ticket passed on that shape, where batch `20261002-k` had to record all of them unproven.
- Order matters inside the window: read `mode=status` (`armed=false`) and the member's pending counts
  before arming, do only the marker writes while armed, then `mode=off` immediately and re-read. A
  blackout left armed silently costs every domain's inbound traffic — every row in this batch used a
  window of a few seconds.
- The reverse direction is NOT staged by this recipe: arming the guest drops the HOST's writes into it.
  A row about the guest losing the host's traffic needs the guest armed and the host writing.

## 2026-10-02 — The block-report counters read as a pair: pending before the window and after the heal

- `block-report-pending-count` is the honest baseline, but only as a pair: the pre-window read proves the
  member starts clean, and the post-window read must show the entry the swallowed write created (`block`
  rising by one per staged write). A COUNT that is only read once proves nothing about whether the report
  was lost or answered.
- The heal is read behaviourally: the counter returns to zero, every client reads the same cell, and the
  host's own line names the cell (`[BlockBreak] …remote damage broke the block at (x,y) without an
  air-write report…`). The guest's `[BlockSync] re-reported N unacknowledged block mutation(s)` line is
  at Information and does show; `[BlockSync] host answered (x,y) — dropped the pending report` is Debug
  and does NOT, so do not read its absence as a missing answer.

## 2026-10-02 — A single partial-damage roll does not appear in the run's pending counters (row 10)

- Symptom: the guest rolled 40 into a 150-health block inside the blackout window; the cell's `damage`
  row read 40 while the block stayed, the follow-up 999 broke it (and the break's report converged
  afterwards), yet `block-report-pending-count` reported `damage=0` throughout.
- Cause: not established in this batch. The damage re-report family shares the same guest bookkeeping
  shape, so the difference is most likely where the single-hit path attaches versus what the run's
  counter probe exposes — read `GuestReportRecovery`/`GuestBlockDamageReportBookkeeping` and its call
  sites at the `DamageBlock` hook before staging the row again.
- Change: row 10 stays `unproven`, and the next run must bring its own instrument (a per-cell read of the
  contribution the probe cannot see) rather than reading a zero as "nothing was recorded".

## 2026-10-02 — `quake-force` is a write, so its reading call starts a second quake

- Reading the quake clock is not free: `quake-force` sets `earthquakeDelay = -1`, so a "read" invocation
  fires the native countdown again (the batch's host went from `earthquakeTime −0.5` back to a fresh
  `8.7 s` quake). Read the quake state once, after the window, and treat a second start as the probe's
  own side effect.
- A quake's own breaks are `[Earthquake] host quake started (Ns, next in Ns) — broadcasting` on the host
  plus per-side `SetBlock(0)` breaks; a guest that sees `earthquakeIntensity > 0` while the window is
  armed is quaking on its own timer, which is exactly the air-write source row 3 needs. A block placed
  onto air near the surface is not durable evidence — the same quake removed it later, so take the
  three-end read at the convergence point and say so in the record.

## 2026-10-02 — The partial-damage counter must be read while the crack still exists (corrects the earlier entry)

- The entry above recorded row 10 as an instrument gap; that was a **read-order error**. Read immediately
  after the crack, `block-report-pending-count` does report it (`damage=1`) while the host and the third
  client still hold the undamaged cell — the crack's report was genuinely swallowed. The first session
  read the counter only after the follow-up break, and a block write clears the cell's damage row
  (`ForgetBlockDamageAccounting`), so the counter read `0` for a path that was working.
- Order for a partial-then-break row: arm, roll the partial, **read the counters before the break**, then
  break inside a second window and read again (`block=1`). Between the two windows the host adopts the
  re-reported crack (its own cell then reads `damage=40`) — that is the heal, not a second report.
- Generalisation: a pending counter measures an OUTSTANDING entry, so it is only visible while the thing
  it stands for still exists. Name the read point relative to the mutation that destroys it.

## 2026-10-02 — An in-place member re-entry needs `leave-world` → `home.leave` → `join-lobby`, and the verdict read comes after the snapshot

- The working sequence (row 6, batch `20261002-o`): the member leaves the world, leaves the LOBBY
  (`home.leave`, state `active=false`, lobby 0), then joins the same lobby id again. The host log then
  carries `Handshake … ignored: not a lobby member` → `Peer … reconnected — presence reused` →
  `Handshake confirmed end-to-end`, and `World join sent to N member(s) (… baseline follows: True)`.
- Reading the marked cells immediately after `join-lobby` returned showed `host=0 / guest=2 / alt=0` —
  the guest's regenerated world still held its own generated block while the host's table held the mined
  state. The snapshot fan-out then applied and the same cells read `0` on all three. A re-entry verdict
  read on the join call's own answer reports a divergence that is only the snapshot in flight.

## 2026-10-02 — The solo→lobby handover is counted on the MEMBER's entry, not the host's (batch `20261002-o` row 9)

- Staging works: `start-run` on a host with no lobby enters the world solo (`role=None`, `active=False`),
  `create-lobby` works from inside that world, and a member can then join the lobby — it waits at
  `Starting…` until its own world entry fires.
- The host's own world re-entry is NOT the handover: after `leave-world` + `continue-run` the host logged
  `World join sent to 0 member(s)` while a member sat in the lobby (the gate still read `0 player(s)`).
  The handover that reaches a member is its own entry edge, and the host names it per member:
  `Sending world-entry snapshot group to <steamid>.` — in this batch that line appears exactly once, and
  both members' logs show the same `Generation stream reset to captured baseline (16 bytes: <hex>)`.
- So the exactly-once denominator for this row is the per-member entry-group send count. A stricter
  review wants a per-member APPLY counter as well; the batch record names that gap instead of hiding it.
- A `Continue` restore takes the world from the live snapshot (`mid-run-*.cuoz`, `revision N`,
  `0 world-block row(s)` in this batch), which is why a solo-marked cell looks "handed over" even when
  the baseline itself would explain it: read the send side, not only the cells.

## 2026-10-02 — A wait instruction is an enumerator, and a wrapper that drives enumerators counts its frames (batch `20261002-p`)

- Symptom: a member whose layer generation spanned the host's absence logged `[GenStream] done — 41
  segments` against the host's 19 for the same generation; its layer-modifier replay entry was
  `D70305E2…` instead of the generation's own last segment start `4AFD152F…`, and the roll landed on the
  other side of the 0.4 threshold. Batch `20261002-m` had the same shape (51/43 against 19).
- Cause: `UnityEngine.CustomYieldInstruction` (WaitUntil/WaitWhile) implements `IEnumerator`, so the
  generation wrapper's `Drive` treated `FinishWorldGeneration`'s wait as a nested coroutine and drove it
  frame by frame — every waited frame ran `Save()` and became a "segment", and the first waited frame's
  save captured the state after the pre-wait work (`DistributeMiniBarrels`). The host never entered that
  recursion: its `Darken()` is skipped inside `RunCoordinator.IsInGateWindow`, so its wait resolved
  immediately and it logged no post-generation segment.
- Change: the yield classification is a pure rule (`GenerationYield`, Runtime) with the wait checked
  BEFORE the enumerator fact; a wait is handed to the engine whole, is not counted, and does not move the
  recorded start. Judging rule: read a segment census as a PAIR of the same generation and compare the
  decision entry states — a census alone does not say which side is polluted.

## 2026-10-02 — A machine fact is parsed raw, so an inline annotation becomes part of the value (batch `20261002-p`)

- Symptom: the preflight reported the `steam` dependency missing with `game-app-id 4576510(…) is not this
  install: the install's steam_appid.txt reads 4576510` — the two sources plainly agreed.
- Cause: the local fact line carried an inline annotation in the value (`- game-app-id: 4576510(与安装的
  steam_appid.txt / appmanifest 一致;410900 是 Forts,错)`); `Read-Facts` takes everything after
  `- key: ` as the value, so the manifest lookup built `appmanifest_4576510(…).acf`, found nothing, and
  the id cross-check "failed".
- Change: the value is bare and the annotation sits on its own line under it; when a fact fails its own
  cross-check while every source agrees, suspect the parse before believing a dependency is missing.

## 2026-10-02 — A mutation pass that nets to zero proves nothing (batch `20261002-p`, the fingerprint row)

- Symptom: the first "post-mutation" fingerprint re-capture equaled the entry pair on all three clients,
  so the comparison had exercised no change at all.
- Cause: the pass wrote a marker cell to gravel and then broke it back to air, and the block table the
  fingerprint hashes returned to its original state; convergence reads prove the peers agree, not that the
  world moved.
- Change: after a mutation pass, compare the re-captured fingerprint against the entry pair and require
  the affected band and the total to differ; leave at least one cell in a new state.

## 2026-10-03 — The content provider's own bind seam is the per-client template switch (batch `20261003-a`)

- The runtime-template state a shared-content mod leaves after discovery can be staged per client through
  the provider's own public `TryBind` with the same `ModBuildingDefinition` payload: bind it on the members
  that must carry it, leave the host without it for the "host cannot materialize" shape, and bind the host
  LAST — the provider has no unbind and its `_definitions`/`_templates` live for the process.
- `TryBind` only stores the definition; the template is built in the provider's own `Update`, and only
  while `WorldGeneration.world` is non-null. Read `mode=status` (the provider's own dictionaries) one
  command later; immediately after `bind` it still reports `hasTemplate=false`.
- Base prefab ids are the game's own resource names, not display names: `keypad` is NOT a resource id
  (`Utils.Create`/`Instantiate` threw "The Object you want to instantiate is null"); in this build the
  keypad prefab id is `dropcapsule`. Read the native spawn vocabulary instead of naming a prefab.

## 2026-10-03 — The entity fallback's window is shared, and one window can serve three rows (batch `20261003-a`)

- The guest's steady fallback re-sends EVERY pending entity report in one window (60 s outside the entry
  phase, 5 s inside it): three lost-rejection creations staged inside their own short armed windows and
  then read once after the shared window produce the `re-reported N unacknowledged creation(s) to the host`
  line and each key's outcome in a single pass — no per-row wait.
- Read each key's outcome from the guest's own lines: a live copy gets `removing the local copy`, a copy
  that already died gets the Debug `has no local copy left — nothing to remove`, and a moved copy is still
  found by its CREATION cell (the rejection line names the creation cell, never the moved position).
- The window is armed by the FIRST outstanding entry; a pending entry created while the window is open
  rides the same re-send, so interleaving new scenarios into an open window costs nothing.

## 2026-10-03 — `leave-world` alone leaves the client in the lobby; a real re-entry needs the lobby leave too (batch `20261003-a`)

- After `leave-world` (`PlayerCamera.ToMainMenu`) a member stays a lobby member (`role=Guest`,
  `lobby=<id>`, `inWorld=false`) and `join-lobby <same id>` is a no-op; the world-entry edge only fires
  after `home.leave` drops the lobby membership and `join-lobby` re-joins it.
- The verdict read comes AFTER the entry applied: host `Sending world-entry snapshot group to <steamid>`,
  member `World join received — starting a run to follow`, member
  `applied host runtime-entity snapshot (N entries, …)` — never on the `join-lobby` return.
- `home.leave` needs the window open and the Home page selected (`open-window` then `goto-page home`);
  `click` on the wrong page fails with exit 1.

## 2026-10-03 — The host's in-session repair group carries the runtime-entity snapshot (batch `20261003-a`)

- A host-created runtime entity whose relay was swallowed by an armed member converges WITHOUT a reconnect
  when the next `Sending the in-session repair group to <steamid>` fires; the member logs
  `applied host runtime-entity snapshot (N entries, …)`. The cadence observed was ~60 s with idle ticks
  skipped, so read the member's applied line rather than timing the cycle.
- A blackout window on that member can straddle the tick: keep armed windows short and single-stepped
  (status → arm → the one write → disarm → re-read) so the next repair tick lands outside the window.

## 2026-10-03 — The rejection forks are reachable through the product's public seams (batch `20261003-a`)

- `WorldService.FireRuntimeEntityRejectedReceived(hostSteamId, msg)` reproduces "a non-reporter receives
  the rejection": the client logs the Debug ignore line and its tables/census stay unchanged.
- `WorldService.SendEntitySpawned` with a hand-built `EntitySpawnedMsg` whose creation token names another
  member reproduces "a hand-built foreign-token report": the host rejects it, and the guest still acts
  because its OWN pending table holds the key (the creator half alone would have ignored it).
- `ISessionControl` (Role/HostSteamId) resolves from `CuoBootstrap.Services`, which is enough to build
  both probes without a forged wire frame.

## 2026-10-03 — The member's host-silence watchdog bounds every inbound blackout (batch `20261003-b`)

- An armed member (its inbound dispatch parked by `net-receive-blackout`) ends its OWN session after 15 s
  without a frame from the host: `[GuestHostSilenceWatchdog] No frame from the host <id> for 15000 ms —
  ending the session locally`, and its world unloads (`role=Guest, inWorld=false`). Keep an armed window
  well under 15 s.
- The parked state survives that session end: a member that ended its session while armed still reads
  `armed=true, subscribers=0` and cannot complete a new handshake (it keeps re-sending `Handshake`, the
  host answers `HandshakeAck`, the member's `recv` stays 0) until `net-receive-blackout mode=off`.
- Lifting the blackout converges the member within ~1 s — the kernel stream hands it the new checkpoint
  immediately (`Projected kernel run baseline (run 1, layer N)`), so a genuinely stale member cannot be
  held for a scenario. Stage the stale-report shapes by handing the frame to the product's own receive
  seam (`generation-forge`), or inside the sub-15 s window; never by keeping the blackout armed.

## 2026-10-03 — A re-entering member's entry group arrives after `inWorld` flips (batch `20261003-b`)

- After `join-lobby`, the member's `state.inWorld` can read true while the entry group's big table frames
  (`TrapLayoutSnapshot` 18 819 B, `RuntimeEntitySnapshot`, `EntitySpawned`) are still arriving. A wire
  probe read 2 s after the edge captured 0 trap frames; the same probe read ~12 s later captured the
  `TrapLayoutSnapshot` with its stamp. Read after the burst, not on the `inWorld` edge.
- The member's own log is the cross-check: `[TrapLayout] materialized … from '<prefab>'` lines prove the
  table arrived even when a probe's read window was too early.

## 2026-10-03 — A live descent chain leaves a layer-modifier divergence on the members (batch `20261003-b`)

- After three live `skiplayer` descents with both members in world, both members logged
  `[LayerMod] baseline divergence — local segment start … vs host's … (world effects may diverge)` and
  their world fingerprints differed from the host's. An in-place world re-entry healed the guest
  (host/guest fingerprint lines identical afterwards); the alt stayed diverged until the session ended and
  the new run started, after which all three object censuses were identical (`total 1899, 34 groups`).
- This is the condition batch `20261002-k` recorded, not the generation-stamp family: verdicts about the
  stamped reports are read from the gate's own comparison and the actors' own tables, never from a
  cross-peer census taken while the divergence is live.

## 2026-10-03 — The evaluator cannot compile a nested delegate that closes over the outer lambda (batch `20261003-b`)

- A recipe whose nested handler `delegate(…)` captures the outer lambda's locals fails at eval time with
  only `eval 'recipe:…' failed: (13,24): <InteractiveExpressionClass 65>.<Host65>m__0()`. The fix: park
  the state in `AppDomain.CurrentDomain.SetData` and have the nested delegate fetch it back itself, so the
  delegate captures nothing (a `const` literal is fine).
- Same family as the batch-d note "nested lambdas must not capture outer locals": the evaluator compiles
  the outer lambda and chokes on the inner closure.

## 2026-10-03 — A recipe with two declared arguments is refused before the client (batch `20261003-c`)

- Symptom: the batch's first window attempt produced no answer file at all, the host stayed on layer 0, and
  the poll never saw an empty table — the run nearly blamed the descent.
- Cause: `game-console` declares two arguments (`command=s args=s`); the driver's `Expand-RecipeCode`
  refuses a call that supplies only one and exits before the socket opens, and the caller had piped the
  driver's stderr to `Out-Null`, so the usage error was invisible.
- Change: pass every declared argument (a game command that takes none uses the literal `args=none`), and
  treat a missing probe answer file as the failure signal — re-run the step with stderr visible before
  concluding anything about the product.

## 2026-10-03 — The host's own world regeneration is the real empty-enemy-table window (batch `20261003-c`)

- The host's runtime enemy table is replaced every frame from the live scene, so a `skiplayer` descent
  empties it for the whole generation: measured 78 → 0 at ≈2 s → 7 at ≈11 s → 78 at ≈13 s after the
  command. A send landing in that window is the product's own empty-table state, not a probe invention.
- The staged shape: arm the member's inbound blackout (the host's InMenu pull and the layer's kernel
  removals are dropped, and `member.InWorld` stays true on the host), run the descent, poll the host's
  `enemy-table-read` to 0, then drive the member's own readiness-window repeat (`scene-repeat` →
  `ResendSceneState` → the host's `SceneStateHandler` repeat branch → its entry-repair claim) and read the
  host's log: the same `Sending the in-session repair group …` line with **no** `[Enemy] snapshot sent`
  line, next to the non-empty control where the same call does log it.
- The member's own set across the window is a within-blackout reading: it stays only because the blackout
  drops everything; once lifted, the host's kernel removals for the old layer reach it (the product's own
  transition lifecycle, not an empty snapshot). Without the blackout the member follows the descent
  instead of staying behind.

## 2026-10-03 — The refusal tombstone is cleared by the SESSION end, not by leaving the world (batch `20261003-d`)

- `leave-world` + `continue-run` stays inside the same session: `generation-read` reported `runEpoch:1`
  before and after, the host's `RefusedItemCreations` still held the old id, and a later operation on it
  was still answered with the precise reason. That is the design (ids are session-partitioned), not a bug.
- The real session end is leaving the LOBBY (host `click home.leave` → `lobby:0, role:None`), then a new
  lobby and run (`runEpoch:2`): the tombstone table read `count:0`, and the same id's operation became the
  plain unjudged `Protocol violation` instead of the precise reason. Stale members must leave their old
  lobby (`home.leave`) before `join-lobby` accepts the new one.
- A member joining the ALREADY-RUNNING session enters late through the entry group: the late-join item
  table read `world 259 / carried 1` on the host and `local 1` on both members, both at `runEpoch:2`.

## 2026-10-03 — Two claimers for one item without a timing race: the inbound blackout (batch `20261003-d`)

- The driver's inter-command gap exceeds the relay latency, so a simultaneous two-sender pickup cannot be
  staged by sending two commands quickly. The shape that works: drop one world item, let BOTH members read
  its id, arm the second member's `net-receive-blackout`, let the first member pick it up (the host
  commits and relays; the blacked-out member drops the relay), then let the second member pick its stale
  copy and report.
- The host answered `Conflict (item … is already carried)` for the second claim — no `Protocol violation`,
  no unknown-item window; the second member's reconciliation re-reported once and then accepted the
  refusal. The blackout is the product's own lazy-P2P swallow, not a probe invention.

## 2026-10-03 — The refused creation's drop ids survive behind a blackout (batch `20261003-d`)

- A first-writer-wins break refuses the loser's drops; with the loser's inbound blacked out it never
  receives the `ItemReject`, so its local drops (and their readable `ItemInstanceId`s) survive. Place a
  known block with `block-set-at` (block 11 always yields one wood drop), have the host break it first,
  then the blacked-out member break its stale copy: the host logs the refused report and records the
  tombstones.
- `item-world-read` then gives the refused drop id; `item-tombstone-read` on the host shows
  `remembered:true, reason:"BlockAlreadyBroken"`; the later operation (through the product's own
  `IItemControl.SendItemPickedUp`) is answered with the precise reason on the wire and the guest destroys
  its local copy.

## 2026-10-03 — Crafting and trade need a fixture, and the host's stock overwrite moves the buy index (batch `20261003-d`)

- Crafting: recipe 0 is `2 × foliage → rope` (foliage matched by quality, `minimumCondition 0`). Two
  `item-provide mode=create type=foliage` calls make it craftable; `craft-drive mode=make index=0`
  produces the `rope` locally, and the host's own log (`[Crafting] Craft of …: 2 entries, 1 products
  applied.`) plus a `container-read mode=host` read prove the registration half.
- Trade: the nearest trader starts at `valueGiven 0`, so a purchase needs credit first —
  `trade-drive mode=give type=neuralbooster` (value 50) credited 0→50 and the host executed the GiveItem.
  The give's host broadcast overwrites the guest's local stock, so the index read BEFORE the give can name
  a different item after it (pre-give index 5 was `browncap`, post-give it was `spraybottle`): read the
  bought type from the buy result, not from the earlier list.

## 2026-10-03 — A moved ticket's references need a manual sweep; the reference gate exempts the records

- Symptom: after batch `20261003-d` moved `item-creation-registration-first` to `done/`, a closed
  ticket's `Related:` line still named it under the old `review/` folder, and the gates were
  green. A whole-`docs/` scan of ticket references, run after the sweep, still finds 55 unresolvable
  references — every one of them inside `backlog/done/`, `backlog/resolved/` or `evidence/selfchecks/`.
- Cause: `BacklogReferenceGateTests` deliberately skips the point-in-time record folders
  (the exemption list lives in `BacklogIntegrityGateTests`), because a record states what was true when it was
  written ("Owner cycle: backlog `todo/<slug>.md`") and a later move must not force history to be
  rewritten. The move itself is therefore the only thing that updates a reference, and the exempt
  folders are exactly where nothing will remind you.
- Change: a ticket move sweeps the moved slug across the whole `docs/` tree by hand. Update the
  pointer-shaped references (`Related:`, `Source:`, "closed by", `Superseded by`) to the ticket's
  current folder; leave the point-in-time positions (an "Owner cycle" line naming the folder the
  ticket sat in, a dated "moved" note) as written. This cycle fixed the 12 pointer references it found; the
  remaining 55 are the records the exemption exists to protect. The sweep is one regex over
  `docs/**/*.md` with a target-existence check, cheap to re-run after every move.

## 2026-10-03 — Batch `20261003-e` (recipe unlock, trade, fallback cadence; two clients)

- **A blueprint's unlock is readable only from the recipe table.** `craft-drive mode=list` lists recipes
  whose materials are at hand, so the freshly unlocked index need not appear in it: dump
  `Recipes.recipes[i].INT`/`visible` before and after the use and diff. `item-use type=blueprint` rides
  `Body.UseItem`, and the item's own destruction is a separate `ItemCommand` the guest reconciles in its
  5 s × 12 window (`re-reported ItemDestroy … (1/12)`).
- **The unlock fallback's live steps**: a swallowed guest report healed on the steady step 60.9 s later;
  the host's `unlock set: N reported, M this host had not learned` line is the merge's difference, and the
  host's set send (60–63 s cycle) confirmed the guest. A relay dropped to a member heals on that same set
  (26.7 s after the host's own unlock). The entry group delivers the set to a late joiner with no
  per-recipe alert, and the same set re-delivered applies idempotently.
- **The block-break report leaves ~150–250 ms after the recipe call returns.** A 0.1 s blackout missed it
  and the host adopted the live report; the working shape keeps the host's inbound blacked out ~2 s after
  the break, reads `block-report-pending-count` inside the window (`block=1, drops=1` proves the swallow)
  and then lifts. Measured: entry phase 5.04 s, steady 60.06 s, unblocked control 57 ms.
- **A late joiner's trader stock reads 0 until the first fallback snapshot** (5.0 s base, stretched to
  ~8.6–10.0 s under the run's ordinary load) — the empty read right after entry is the pre-fallback state,
  not a defect; record its time next to the filled one.
- **`trade-drive mode=give` is capped by the trader's lifetime credit** (total 60 in this world): past the
  cap it returns `credited=false`. Use a purchase for a later value change. Log shapes: host-local
  `[Trade] host broadcast action=…`, guest `report action=…`, host `executed action=… accepted=…`, and
  every receiver `[Trade] state received trader=(…)`.
- **The recruit gate is already open in a fresh world** (nearest trader reputation 100, hostility 0, build
  health 1250). What the recruit row still needs is a dead target visible from inside the trader's 8-unit
  range plus the Online UI `member.recruit.<steamId>` control drive — the missing fixture for the next
  cycle.
- **`session-environment.ps1` needs an explicit `-GameDir`** (its example carries it; the local line in
  `AGENTS.local.md` had omitted it — fixed there).
- **The Steam1 box's Steam can be cold after a reboot**: start it inside the box, wait for the box's
  `connection_log.txt` `Logged On` line, then launch the game directly; never start the physical
  `steam.exe` again.
- **Ending clean**: both clients' driver `quit` left zero game processes; `.acceptance/20261003-e/` holds
  every probe dump and log excerpt the four records cite.



