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