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
- **The <sandbox-guest> box's Steam can be cold after a reboot**: start it inside the box, wait for the box's
  `connection_log.txt` `Logged On` line, then launch the game directly; never start the physical
  `steam.exe` again.
- **Ending clean**: both clients' driver `quit` left zero game processes; `.acceptance/20261003-e/` holds
  every probe dump and log excerpt the four records cite.

## 2026-10-05 — Batch `20261005-b`: holding an item report across the host's checkpoint, and what an empty host table costs

- **A report can be held across the host's own checkpoint on purpose.** The host's in-session repair cycle
  sends a kernel checkpoint every 60 s (`Sent kernel checkpoint at revision N`; measured
  19:51:14.757 → 19:52:14.757), and the phase is readable from the last such line. Arm the host's inbound
  blackout ~8 s before the next one and drop the item inside it: the guest's
  `[ItemCommand] kept N unacknowledged item report(s) across the world baseline restored at revision N`
  lands at the restore edge, and holding the blackout through the first re-report (~5 s) makes the second
  repeat the one that converges. The row that used to fail here failed because a checkpoint emptied the
  queue; the keep line is the evidence that it now survives.
- **An empty host world-item table is the layer edge, and that edge also reloads the member's scene.** The
  only thing that empties it is a layer change
  (`[LayerReset] dropped the previous layer's world-rooted items; N item record(s) remain (carried items
  cross the boundary)`). Inside that window the member has no local body — `container-read mode=local`
  answers `no-local-body` at +4.1 s — unless the member's OWN inbound is parked as well, which keeps its
  body and its carried item alive through the transition.
- **Two blackouts for that row, and the freeze stays under the watchdog.** `GuestHostSilenceWatchdog` ends
  the session at 15 s without a host frame, and the report dies with it
  (`dropped 1 unacknowledged item report(s): the session ended.`) — a first landing ran 17.1 s and lost the
  row. The working sequence is one command: arm host, arm guest, skip layer, +2 s, destroy the carried item,
  disarm guest, disarm host (12 s end to end, member freeze 9.7 s). The gap between separate agent commands
  is 5–7 s of wall clock on its own, which is what blew the cap; read the host's tables after the disarms,
  because a `container-read mode=host` is a ~50 KB answer (~4 s) and eats the window.
- **Schedule layer-changing staging last.** Two consecutive layer changes dropped both members out of the
  world and started a repeating `[LayerMod] baseline divergence` warning — the guest's rolling log grew
  0.8 MB → 33.4 MB in about four minutes — and the members do not come back on their own (`SendWorldJoin`
  has no periodic re-send). Recovery is `home.leave` + `join-lobby`, or a cold restart of all three clients.
  **What filled that log is corrected by the 2026-10-06 entry below** (the warning rides the 5-second
  snapshot keyframe and is a few dozen lines over the window; `[ItemPhysics] settle`, a per-frame
  Information line, is the volume); the staging advice itself stands.
- **`item-pickup mode=id` still obeys the game's proximity guard**: naming a world item by id from anywhere
  is refused with `PickUpItem left <type> outside slot 0`. Pick the item the member just dropped at its own
  feet, or use `mode=type` (nearest).
- **Killing a committed-batch receipt needs ~6 s of parked inbound.** A 1.6 s window produced no repeat at
  all (the host emits its committed batch late); 6 s produced the intended
  `re-reported … (1/12)` → `converged`, with the host answering the repeat as
  `[ItemDrop] … present — re-placing at (…)` after exactly one `not present — requesting materialization`.
- **Quote `logs\latest.log`, not `LogOutput.log`, when the evidence needs timestamps** — and a byte-mark
  reader must seek BYTES: slicing the split text by the mark silently matched nothing, which bought a round
  of `NO MATCHING LINES` on a run that had in fact produced the line.

## 2026-10-05 — Batch `20261005-c`: driving the game's own release path in process, and what a passing row can still tell you

The run accepted the remote-inventory native-parity rows by driving `PlayerCamera.HandleReleaseDragging`
— the game's own release entry — with no OS-level input. The mechanism is reusable and is now the
committed recipe `tools/acceptance/recipes/remote-gesture.cs` (`mode=list|open|close|release`).

- **The three inputs are the whole gesture.** The native branch reads `camera.dragItem` (public field),
  `camera.clickPos` (public field) and the `uiCasts` list it is passed. Stage those three, invoke the
  private method by reflection, and the game's own branch runs — the Harmony bracket
  (`PlayerCameraDragUsePatch`) sees it exactly as it sees a mouse release.
- **The `uiCasts` list is a target pointer, not a raycast result.** `InvButton.Overlaps(casts)` ignores
  the list entirely for a non-centre button and answers a pure pointer-distance band for a centre-class
  one (`152 < distance/PlayerCamera.uiScale <= 281`). `new RaycastResult { gameObject = button.gameObject }`
  is enough to select a ring slot; `FindObjectsOfType(typeof(InvButton))` lists them with `slot` and
  `GetItem()`, which is the item a drop lands on.
- **When the run may not move the pointer, move the ring.** The first attempt logged
  `the release of item … produced no intent — unclassified native gesture`: the guard had refused because
  the real pointer sat outside the ring. Setting `camera.radialMenu.position` so the pointer falls in the
  annulus (distance `200 * uiScale`) makes the same guard pass, and the branch that then runs is still the
  game's own. Record that staging in the row's evidence — it is scene setup, not a weaker check.
- **`clickPos` decides drop vs place.** The world branches require
  `Distance(Input.mousePosition, camera.clickPos) > 10`; the container branches require `< 10`. Setting
  `clickPos` to `mouse + 60` or to `mouse` is therefore the whole difference between "drop it" and "put it
  in the slot", with no pointer movement at all.
- **A take-out released over empty space ends in the world, not in a slot.** The native world action runs
  `Container.UnloadItem(dragItem, null)`; the item then exists as a world item. Prove it with
  `item-provide mode=pickup type=<type>` and check that the answer names the SAME instance id — that is
  the difference between "the item is fine" and "the item vanished", which is what the row was about.
- **Not every rendered proxy is drivable.** `RemoteDragProxyQuery.InstanceId` reads the
  `RemoteInventoryItemId` marker; a render without it answers `0` and the release patch fails closed.
  `mode=list` reports the marker's id and owner, so pick a proxy that carries one.
- **The sandbox windows are indistinguishable by title** (`[#] CasualtiesUnknown [#]` for both boxes) and
  all three processes report the same physical `Path`. The Online UI's Home page prints the client's own
  SteamID, so one window capture settles the PID↔endpoint mapping — cheaper and more reliable than
  guessing from launch order.
- **The evaluator's Mono REPL refuses an inner lambda that captures a local** (the 2026-10-03 lesson in
  its general form): the first version of the recipe captured the resolved marker type and died with
  `(1,1): InteractiveHost`. Pass the type as a lambda PARAMETER — a `const` capture is fine, a local is
  not.
- **A passing row can still carry the run's most valuable finding.** Every container move this batch drove
  made the operator's `CloneFactTable` monitor warn (`nested container contents changed without an event
  sync`) while the owner logged nothing. The rows passed; the warning became
  `container-move-snapshot-only-sync`. Read the run's own diagnostics before closing
  a batch, not only the row's expected line.

## 2026-10-05 — Batch `20261005-d`: an event that ARRIVES is not yet a monitor that stays quiet

The carrier fix made the owner's own hook report a remote-driven container move, and this run proved that
half at runtime: the owner logs `[ItemTrace] … origin=OnItemLoadedIntoContainer result=Committed
events=[ContainerContent]`, and both the operator and the third peer log `[CarriedSync] applied trashbag
(id …) to …'s snapshot — re-rendering the clone.` One millisecond later the SAME monitor logs
`[CharSync] divergence … nested container contents changed without an event sync` and `… left the
inventory without an event sync`. The ticket's row is a ZERO-warning row, so it failed.

- **Read a zero-warning row as a PAIR of channels.** `[CarriedSync]` (the event applied) and `[CharSync]
  divergence` (the monitor's verdict) share neither a logger nor a prefix — a `-Match 'CharSync'` filter
  misses every `[CarriedSync]` line and silently turns "the event arrived" into "no event at all".
- **The contrast that locates it:** a `PickUpToSlot` (slot release) stays silent, because its event carries
  the item itself; a `MoveIntoContainer`'s event carries the ROOT container, and
  `CloneFactTable.WarnOnDivergence` compares the PREVIOUS SNAPSHOT with the incoming one — so the moved
  child's departure from the top-level list and the container's new contents both stay uncovered.
- **A remote gesture is judged on the OPERATOR's own client.** The first attempt was refused with
  `<operator> cannot see <owner> on this client` until the three bodies stood in one spot, so stage line
  of sight before driving any remote intent (`body-place` takes WORLD coordinates).
- **`item-world-read` takes BLOCK cells while `body-read`/`body-place` speak WORLD coordinates**: on this
  machine world (0, 445) is cell (510, 941). A world read at the wrong cell reports "nothing there" for an
  item standing right next to the bodies.
- **A classified radial no-op logs nothing**: `RemoteDragIntentDispatcher.Emit` logs only
  `outcome.IsUnclassified`. A row that asks for "an observable log line, never a silent no-op" needs a
  refusal shape that reaches a logged branch (the container guard has one).
- **The radial CENTRE action is drivable in process** and is now committed (`remote-gesture`
  `mode=probe` / `mode=hover` / `cast=-2`): `TryPerformRadialAction` reads the cast's TAG,
  `radialMenu.localScale` (> `inventoryUseLeniency` 0.95) and the pointer's distance to `radialCircle`
  (< its radius 135) — it never consults `InvButton.Overlaps`. `mode=hover` stages the drag and the ring
  under the pointer so the game's own frames open the ring, and `ringScaleForced` in the answer says
  whether the run had to write the scale.
- **The evaluator returns VOID when an inner lambda's local shares an outer local's name**: a probe whose
  answer was JSON without a `value` field had `var found` outside and `System.Type found` inside its own
  helper. Rename one of them; the failure names nothing.

## 2026-10-06 — A peer's monitor reads the fact it REBUILDS, not the message the owner sent

- Symptom: batch `20261005-d` re-judged the container-move ticket after the owner-side carrier fix and
  read the same two warnings one millisecond after the event applied — `nested container contents changed
  without an event sync` for the bag and `left the inventory without an event sync` for the item that
  entered it, on the operator and on the third peer.
- Cause: the receiving client never applies the owner's message. The carried fact a peer sees is rebuilt
  from the committed batch, and a container's contents are listed from the kernel's contained children
  (`KernelBatchItemProjection.BuildContents`). The fix had lit the carrier up, but the host's own container
  report wrote only the parent's DATA, so the batch reached the peers as an empty container with the moved
  child still in its old kernel location. A row that reads a peer's monitor therefore judges the whole
  chain — report, kernel write, projection, clone table — and cannot be satisfied by making the sender
  louder.
- Change: one container report is one `SyncContainerItemsCommand` on both paths
  (`ItemKernelAuthority.TrySyncContainerFacts`, called by `ItemService.SendItemCarriedSync` when the report
  carries contents; the guest's `ItemContainerSync` wire kind already mapped to it), and the dead,
  relocation-blind second implementation (`ItemContainerSyncWriter`/`SyncContainerContents`) is deleted.
  When row A1 is re-read, look for where the child SITS in the kernel and in the batch — the `applied` line
  alone proves only half the chain.

## 2026-10-05 — Batch `20261005-e`: a zero-warning row needs a control gesture, and the re-report that outruns its own event

- Symptom: the container-move fix passed every container kind (insert, take-out, slot release, battery
  unload, both owner directions — zero divergence on the operator and the third peer), yet row A1 failed
  again on the DROP kind: `left the inventory without an event sync` on both viewers, in both
  directions.
- What localised it: the same drop driven through the owner's OWN gesture. The host dropped its own
  lantern with a local release and **neither viewer warned** (`marks-cm-localdrop.txt`), and the owner
  logged no `inventory changed — immediate re-report` either. When a row's expectation is an ABSENCE,
  drive the same scenario through the path you are NOT testing as a control: it is what turns "the drop
  carrier is missing" into "the remote-intent path's re-report is early".
- The actual ordering: `RemoteIntentApplier` ends every applied non-continuous intent with
  `CharacterDataSync.ReportInventoryChanged` (its comment: "the immediate re-report makes every clone …
  converge now"), so the owner's character snapshot omitting the item goes out at once, while the drop's
  own report waits for the pending-drop flush — 30 ms later in this run. Read the owner's own log for
  both lines before blaming a carrier: the event exists, it is just behind the snapshot that announces
  its absence. A world-driven burst that dropped three of the host's items inside 2 ms confirmed it from
  the other side: the one whose report was already flushed was silent, the two still pending warned.
  **Superseded on the drop path (2026-10-05, decision 236):** the re-report is now held back while a drop
  report is pending, at BOTH of the owner's entry points for it — the intent applier and the patch bridge's
  `OnInventoryChanged` — so this paragraph describes what the batch measured before the fix; it is the
  reason the fix is an ORDER rule rather than a new carrier.
- `CloneFactTable.WarnOnDivergence` says outright that "a change whose event is still in flight trips the
  warning too" — so the monitor alone cannot tell a late event from a missing one. The owner-side trace
  is the discriminator, and it belongs in the record either way.
- A whole intention kind can be unreachable in process: `MoveContainerChildren` needs the game's own
  `Input.GetKey(KeyBinds.GetBind("expanddesc"))`, which the live probe reads as `LeftShift`, `false`
  (`probe-expand-key.cs`), and the driver may not synthesise OS-level key state. Probe the guard, name
  the bind, and record the row `blocked` — three batches have now skipped this kind silently.
- Fixture staging has a sharp edge: a world-generated item carries an INSTALLED battery
  (`item.battery.hasBattery == true`) while a `Utils.Create`d copy carries only the component, so the
  native R2 guard refused the first two attempts with a named line. Bringing the nearest world item to
  the body (a scene setup, the same class as `body-place`) and taking it through the game's own pickup
  is what made the row drivable — worth reaching for before writing a row off.
- `item-world-read` takes BLOCK cells, not world coordinates (`BlockToWorldPos`). A read aimed at the
  body's own coordinates silently returned one unrelated item 500 units away. Derive the cell from a
  position you know in the same world (the body's own, read through `probe`/`body-read`).
- The third client can still be mid-handshake long after the host started the run: this batch's `<sandbox-alt>`
  member confirmed its handshake 2.5 minutes late, materialized the world on its own, and needed no
  re-drive. Check both member endpoints before the fixtures; do not repeat `start-run`.

## 2026-10-06 — Batch `20261006-a`: read a zero-warning row together with the polarity that produced it

- **The fix's own log line is the evidence, and the contrast sits in the same window.** A remote-driven
  `DropItem` and `DropWearable` log `replayed native … — a drop report is pending and announces it on the
  next frame, so no immediate re-report.` and then, ~30 ms later,
  `origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]`; inside the SAME mark the container
  and slot kinds still log `[CloneRender] inventory changed — immediate re-report.`. One `-Match` over one
  window therefore carries both the absence (zero divergence) and the reason it is the fix rather than the
  old code — prefer that over a bare warning count.
- **A wearable drop needs the OWNER dressed, in the type the operator drags.**
  `PlayerCamera.TryPerformWorldActions` calls `Body.DropWearable` only when
  `this.body.GetWearable(dragItem.id)` is non-null, and `RemoteDragPredicatePatches` answers that query
  from the body the ring shows — so if the owner is not really wearing a wearable of that TYPE the
  release produces no intent at all, and a run whose ring list looks perfectly healthy proves nothing.
  The operator's own body state is irrelevant to that guard; each direction needs its OWNER dressed
  (`tools/acceptance/recipes/item-wear.cs`).
- **A `mode=hover` radial stage does not survive the game's own frame — say what you ATTEMPTED.** The
  hover does set `camera.radialOpen = true` (`f4-host-hover-helmet.json`: `radialOpen: true`,
  `pointerDistanceOverScale: 0`), and the ring is gone again by the next probe (`menuScale 0.01`,
  `centerButtonCount 0`) because `HandleWhileDragging` re-closes it when
  `|radialMenu.position.x − Input.mousePosition.x| > 600 × uiScale` — the run may not move the OS pointer.
  Do not turn that into "the radial centre is undrivable": the committed `mode=release cast=-2` forces
  `radialMenu.localScale` to one and repositions the ring inside the same call, which is how batch
  `20261005-d` drove its radial row, and a hover-time probe is not that path. Name the attempt, name the
  committed path that exists, and stage an owner-side fixture through a recipe when the fixture is what
  you actually need.
- **A new recipe can fail in the evaluator for a reason the run never establishes.** The same body
  written with multi-line `if` guards failed with `Unexpected character \0022` while a one-line-guard
  form compiled and ran; the cause was NOT established (multi-line guards and non-ASCII bytes both exist
  in recipes that work, e.g. `item-provide.cs`), so record it as an observation about that invocation,
  keep a working form, and prove any new recipe by invoking it rather than by reading it.
- **A stale `camera.dragItem` silently disables the remote ring.** After an aborted `mode=hover` one client
  kept a dragged proxy and every later `mode=open` / `mode=list` answered `buttonCount: 0`,
  `viewOpen: false` — while the other client listed seven buttons for the same fixture. Clearing
  `dragItem` and `radialOpen` brought the view and the ring straight back. When one client's ring works and
  the other's does not, look for a leftover drag before blaming the view.
- **A recipe's guards must survive the evaluator's REPL.** The same recipe written with multi-line `if`
  bodies failed with `Unexpected character \0022` while the one-line-guard form compiled and ran. Keep a
  recipe's guards on one line each, keep its file pure ASCII, and prove any new recipe by invoking it.
- **The Steam half of a batch can be down, and starting it is the run's own job.** Both sandboxes' Steam
  clients had stopped since the previous session (the client's own log:
  `SteamAPI.InitEx failed: Cannot create IPC pipe to Steam client process`; the two box logs' last write
  was hours before the physical Steam's). Starting Steam inside each box is the run's step — it is not the
  physical Steam and not the same act — and no relaunch is needed afterwards: the Online UI's create/join
  call `EnsureSteamReady()` → `steam.Initialize()`, so re-driving `create-lobby` / `join-lobby` retries the
  init on the clients already running.
- **A missing fixture is a recipe, not a weaker row.** `item-provide` stages a carried item through
  `Utils.Create` + `Body.PickUpItem`; the wearable half needed the same treatment for `Body.WearWearable`
  and got it as `tools/acceptance/recipes/item-wear.cs` — the row's own gesture is still the operator's
  remote release, so the fixture strengthens the run instead of substituting for it.

## 2026-10-06 — A held key IS reachable in process, and the OS key state never moves

Measured on one running client, after batch `20261006-a` had recorded row A1g as permanently blocked on
that capability. It is not: the blockers named by three batches were "no in-process route", and this is
that route.

- Posting the game's OWN window a `WM_KEYDOWN` (`wParam` = the virtual key, `lParam` = the key's scan code)
  makes `UnityEngine.Input.GetKey(KeyCode.LeftShift)` read **true** on the next frame.
- A `WM_KEYUP` carrying the scan code AND the transition bits (`0xC0000000 | scan << 16 | 1`) makes it read
  **false** again. Probes: `.acceptance/20261006-a/probe-key-decl.cs`, `probe-key-post.cs`,
  `probe-key-read.cs`, `probe-key-up2.cs`.
- **`GetAsyncKeyState` stayed 0 throughout, and the client's window was never brought to the foreground.**
  That is what keeps this inside the `input` dependency's rule: it is a message to the client's own window
  from inside its own process, not OS-level keyboard or mouse, and
  `AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput` still governs the latter. Nothing
  enters the OS input queue and no other window is addressed.
- **A key-down alone is NOT the capability — prove the key-UP too.** The first release attempt
  (`WM_KEYUP` with a zero `lParam`) left `Input.GetKey` true for minutes afterwards: a half-verified hold
  leaves the modifier stuck down for the rest of the session. Read the state back after every release.
- The evaluator accepts a type declaration carrying `[DllImport]` as one input — declare it in one eval,
  use it in the next; a trailing expression in the same input is a syntax error. So an in-process probe
  reaches user32 without any OS input.
- What is left is ordinary work rather than a research question: build the hold/release into the committed
  driver, prove it with a read-back, and judge the row that needs it.

## 2026-10-06 — Batch `20261006-b`: the held key lands on the NEXT input, so the read step is the verdict

The capability above is committed (`drive-in-process.ps1 -Action declare -Declare window-key` loads
`recipes/key-hold.cs`'s declaration once per client; the recipe's own `bind`/`action` steps hold, release
and read a bind the game itself uses). It was smoked on the primary Sandboxie box's client (evaluator
`<port-guest>`) — the harder of the two hosts, because `Process.GetCurrentProcess().MainWindowHandle` resolving
and the message landing had to hold inside a box; the physical host is the half no smoke has covered.
What the smoke taught, in the order it taught it (artifact directory `20261006-b` in
`acceptance-artifacts-dir`: `g*` the declaration and the first hold steps, `m*` the key-held gesture with
its before/after tree, `c*` the same gesture as a control without the key, `k*` the ring toggle):

- **An input cannot read the state its own message produces.** The first cut posted the message, slept a
  bounded 200 ms inside the eval and read `Input.GetKey`: it read `false` after a key-DOWN and `true`
  after a key-UP — both stale. The very next input read the new state (`g5-read-held.json`), and the tree
  moved, so nothing was wrong with the message: the client's frame that handles it runs only after the
  evaluator returns. A hold is therefore judged by the `read` step that FOLLOWS it, and the recipe reports
  `heldAtStart`/`heldAtEnd` as readings plus `posted` as what it queued — never as a verdict. Do not
  "fix" this with a longer sleep inside the eval.
- **The exception the ban needed is two names wide, not four.** `MapVirtualKey` (scan-code lookup) and
  `GetForegroundWindow` (a read) are queries no ban list ever covered, because neither can drive anything;
  only `PostMessage` and `GetAsyncKeyState` are on it, and those two are excused in one directory. The
  gate refuses an exception that excuses a name the ban does not list — which is what pushed the design to
  the narrow shape, and the recipe now calls the declaration's own vocabulary so no caller names them.
  A corollary the review caught: the scan code is asked for the key the bind resolved to, not for the
  generic one — this layout answers 0x2A for `VK_LSHIFT` and 0x36 for `VK_RSHIFT`, so substituting the
  generic `VK_SHIFT` would have queued the wrong scan code for every right-hand bind.
- **Polarity is what makes the mechanism mean something.** The same staged release (a bag holding a dogfood,
  dragged onto a second bag's slot button, in one client) moves the CHILD with `expanddesc` held and does
  not move it without: before/after `container-read mode=local` shows the dogfood under slot 1's bag with
  the key and back under slot 0's bag without it. Drive a key-gated branch together with its control
  gesture, or "the gesture ran" proves nothing about the key.
- **The game's own ring can be opened in process with the same capability.** `toggleinventory` (Tab) is read
  as a PRESS EDGE (`Input.GetKeyDown` in `PlayerCamera`), so one queued key-down toggles
  `PlayerCamera.radialOpen` and the ring's `InvButton`s become active (`remote-gesture mode=list` then
  answers six buttons instead of zero) — this is what lets a single client drive a LOCAL inventory
  gesture, and it is cheaper than the remote view. Release the key with a following `up` + `read` step
  anyway: a Tab still held is a key that reads down for the rest of the session, the next queued down is
  then no edge at all and the ring will not toggle again.
- **A leftover `camera.dragItem` also disables the LOCAL ring.** Batch `20261006-a` recorded the same
  symptom for the remote view; here a `mode=hover` left one behind and the ring stayed closed
  (`buttonCount: 0`) until it was cleared. Clear the drag and `radialOpen` before staging a ring.
- **A `[DllImport]` declaration is a whole input of its own, and the driver probes before sending it.** The
  evaluator takes a type once per client (a repeat is its own "already defined" error), so `declare`
  probes for the type first, sends the file only when it is absent and probes again for the verdict: the
  second call in the same client answered `sent: false` with `declared: true`. An idempotent setup step is
  what makes a per-client capability safe to re-run after a client restart.
- **The capability did what it was built for, and the row it unlocked FAILS.** In the three-client batch
  that followed the smoke, the key recipe held `expanddesc` on both an operator client and the physical
  host (`heldAtEnd: true`, `osKeyAtEnd: 0`, `isForeground: false`), the operator's release produced
  `[RemoteIntent] MoveContainerChildren captured …` and the owner's own loop expanded the container
  (`1 of 1 direct child item(s) entered the container`) — and both viewers still warned
  `nested container contents changed without an event sync` for the target bag. A driver capability that
  reaches the gesture is not a passing row: drive it, then read the row's own evidence.

## 2026-10-06 — Batch `20261006-b`: the expansion kind's carrier is the load hook, and a local gesture proves it

- Symptom: row A1g's gesture ran end to end (intent captured, owner's native loop moved the child, the
  owner's scene showed it in the target bag) and the operator's and the third peer's clone fact table both
  warned `nested container contents changed without an event sync` for the target container — 20 ms after
  the owner's own expansion line.
- Cause: the owner's two hooks reported the child's calls as `events=[Unload]` and, for its load into the
  TARGET container, `[ContainerLoad] … left the world into a body container — pickup report.` /
  `events=[Pickup]`. `Patches/ContainerItemPatches.cs` captures `ItemWorldSync.IsWorldItem(item)` in the
  `Container.LoadItem` prefix, and the expansion's own native pair (`source.UnloadItem(child, null)` then
  `target.LoadItem(child)`) has just detached the child into the world, so
  `ContainerItemSync.OnLoadedIntoContainer` takes its world→body pickup branch — the target container's
  full fact, the one the peers' fact table compares, is never sent. In direction 2 that misreport cost the
  peers the item itself: they materialized it as a world drop and removed it from the owner's clone.
- **Change: drive a key-gated gesture together with a LOCAL control of the same gesture.** The same release
  driven by the OWNER (no operator, no intent) produced the identical hook pair and the identical warning
  on both peers, which is what moved the finding from "the remote-intent path" to the hook's own
  classification. Without that control the record would have blamed the intent plumbing.
- **The contrast line is in the same log.** `container-fill`'s single load (no preceding unload) reports
  `origin=OnItemLoadedIntoContainer result=Committed events=[ContainerContent]` and warns nowhere — so
  when a carrier looks missing, look for the call shape that reports correctly beside the one that does
  not.
- **Close the Online UI window before driving the game's own keys.** The first `toggleinventory` (Tab)
  toggle did nothing while the Online UI window was visible on Home (`buttonCount: 0`, `radialOpen:
  false`); `click window.close` and the same key sequence opened the game's own ring
  (`buttonCount: 6`). The world-capture rule already says this for frames — it holds for keys too.
- **An owner-side fixture staged while a viewer already has that owner's remote view open warns on its
  own.** The guest's three `item-provide mode=create` actions produced
  `[CarriedSync] … not in …'s snapshot and slot unknown — the 1 Hz snapshot will carry the change.` plus
  `a new carried item the fact table never saw — a pickup without an event sync` on the host and the third
  peer, while the host's own creations (staged before any view was open) produced none. Stage an owner's
  fixtures before opening the viewer's remote ring, or expect those lines in the window and say which
  action produced them.

## 2026-10-06 — Batch `20261006-d`: a "local control" is only local if the ring is on your own body

- **A release's cast index is scene state, never a constant.** The game's ring `InvButton`s do not exist in
  the scene while the ring is shut: a list taken with it closed reads `buttonCount: 0`, and a release cast
  at any index then resolves to nothing and falls through to the WORLD path (which dropped a whole
  fixture bag in this run). With the ring open and the native container panel also open, the panel's two
  `ContextMenu` buttons come first and the ring's slot-1 button moved from index 4 to index 6. Re-list
  immediately before every release, and read the release's own target fields — `remote-gesture`'s release
  result now reports the resolved item's type, instance id, display-proxy flag, owner, parent, position and,
  for the dragged container's children, both `Container.LoadItem` guard inputs and where the child ended.
- **A load into a display proxy and a refused load produce the SAME log shape** — unload registered, no load
  report, a real drop at the unload position ~1.4 s later, the item destroyed right after. The load into a
  proxy LANDS (the probe read `childInCast: true`), both container hooks skip display proxies by design, and
  the clone render rebuild later dumps the foreign child into the world, which the drop report then states
  correctly. Never read "the native guard refused" from that shape: read the target's identity.
- **A run must know which body the ring is showing.** `InvButtonBodyPatch` redirects `InvButton.GetItem()`
  to the focused clone, so a slot-numbered cast names the OTHER player's item while a remote view is open.
  Batch `20261006-c`'s local control cast at an index captured with the guest's view open, which aimed a
  local dragged bag at the guest's proxy — the row was never driven, and its log was read as a refusal.
  Open the remote view for remote gestures, never for a local one.
- **Open the ring with the game's own `toggleinventory` (Tab) toggle, with no drag staged.** While an item is
  dragged the game force-closes the ring (`PlayerCamera.cs:1778`: ring-to-pointer distance over
  `600 * uiScale`, or `consciousness < 20`), and a staged drag left behind by `mode=hover` keeps it shut —
  consume the staged drag first (a `cast=-1, moved=0` release does it harmlessly, because the world path's
  own guards need a pointer move over 10 units to drop anything).
- **Two guards batch `20261006-c` suspected are excluded by measurement, not by argument:** a `trashbag`
  accepts a second `dogfood` (`canHold: true`, `contentsAfter: 2`), and the display proxy stood 0.178 units
  from the dragged bag, nowhere near `LoadItem`'s ten-unit distance test.
- **The shape has a sibling defect** and it is ticketed, not fixed in the batch: a local item released onto
  a ring button that shows a remote player's container is absorbed by that display proxy, then dumped into
  the world and destroyed — an item loss for the actor
  (`backlog/done/local-item-into-remote-display.md`).

## 2026-10-06 — Batch `20261006-e`: a refusal is read from its own line, not from the item's parent

- **A refused release and a landed one can share `childParentAfter`.** A carried `trashbag` is an instance,
  so both outcomes leave the child's parent reading `trashbag(Clone)`. Read the refusal from the release
  probe's own pair (`childInCast`, `castContentsBefore` → `castContentsAfter`) plus the `[RemoteIntent]`
  line, and confirm the item stayed put with the owner's tree probe — this batch compared the tree before
  and after E1/E2 by SHA-256 (identical) rather than by eyeballing a field.
- **The refusal line's target wording is the discriminator between the guard's branches**: `display proxy
  item <type>` (a ring/panel button carrying another player's item), `a slot of the displayed inventory`
  (an EMPTY body slot of the focused clone — the native R8/R9 sequence would otherwise act on the LOCAL
  body with the clone's slot index), and `display proxy container <id>` (the container window's own back
  panel). A batch that reads a refusal line should be able to say which target it was.
- **A refused release has already cleared the drag**, so the next gesture in the same session needs none of
  batch `20261006-d`'s hover/`cast=-1` staging: `mode=close`, one `toggleinventory` press and the next
  release follow directly (the probe reads `dragAfter: "none"` after a refusal).
- **`remote-gesture mode=open owner=auto` is only usable with ONE guest.** A three-client session has two,
  and the driver answers `ambiguous-guest`: pass the focused player's SteamID explicitly, then take the
  button list immediately before the release (`i=2` → slot 0, `i=4` → slot 1 in this batch's layout, the
  same as the previous one).
- **An empty clone body slot is a usable fixture for the second half of this guard**: the focused player's
  occupied slot 0 and empty slot 1 sit beside the owner's own slot 1, so the native sequence really had
  something of the owner's body to swap or drop — the row is only meaningful when the owner's own body has
  an item in the slot index the clone's button names.
- **`[ItemTraffic] … Drop=0; Destroy=0` is a ready-made witness.** This build writes one traffic line every
  ten seconds, so a per-row byte window that spans one cycle carries both the actor's own refusal line and a
  zero-drop statement for the same window, without a second probe.

## 2026-10-06 — Batch `20261006-f`: a redirect that answers a predicate does not cover the method body

- **An answered guard is not an answered read.** The release window answers `Body.HoldingItem(int)`,
  `HoldingItem(Item)`, `GetItem(int)` and `GetWearable` from the displayed clone, but `GetItem`'s own native
  body still does `this.slots[slot].transform.GetChild(0)` on the LOCAL body — so where the clone's slot is
  occupied and the operator's is empty, the call throws `UnityException: Transform child out of bounds`
  instead of returning the clone's item. A redirect seam must be checked against every caller that reads the
  same index, not only against the guard that admits the caller.
- **A throw inside the game's own release body is silent to the wire.** The release artifact's `calls` field
  carried `TargetInvocationException: Exception has been thrown by the target of an invocation.` and nothing
  else, and the ring's own log said `produced no intent — unclassified native gesture` — which reads like an
  unclassified gesture and is actually an aborted one. Re-run the same release from an ad-hoc probe that
  catches the inner exception and prints its stack (`p-f1-diag2.json` did it in one eval) before writing any
  verdict about a gesture that "did nothing". `dragAfter` still naming the item is the tell: the native body
  never reached its own `dragItem = null`.
- **A slot release's classification depends on the DESTINATION slot's state, and the applier's plan does
  not.** With the remote view open the viewer's native branch reads the clone-answered predicates, so an
  OCCUPIED destination takes the R8 swap branch (`SwapSlots`) while an EMPTY one takes the R9 slot release
  (`PickUpToSlot`) — and only the latter reaches `RemoteIntentApplier.ApplyPickUpToSlot`, whose plan is the
  one computed from the OWNER's own body. A row that needs the applier's two-`Body.DropItem` plan cannot be
  driven by releasing a slot item onto an occupied slot; the reachable two-departure shape is the owner's
  container child (its unload registers a departure, the destination slot's occupant another) — that shape
  settled both entries correctly in this run.
- **Read a divergence warning against a control row, in the same session.** Two `[CharSync] divergence` lines
  per viewer followed the container-child release in BOTH destination states; the slot-item release onto an
  empty slot produced none, which is what attributed the warning to the container-child route rather than to
  the gesture or the session. Take the span read (`span-<client>-divergence.txt` from the batch's mark) plus
  one control release, and keep the owner's own window in the comparison — the owner warns in neither case.
- **`container-read`'s mode vocabulary is `local`, `clone` or `host`** (a wrong word answers `bad-mode` and
  the run loses a step): `local` reads the client's own inventory, `clone` its rendered items with their
  owners and an `orphanCount`, `host` the authoritative transfer and world tables — host-role only, and it
  needs the guest's SteamId explicitly in a three-client session.
- **An owner-side fixture staged while the viewer's remote ring is open warns on its own** (batch
  `20261006-c`'s lesson) — this batch staged its fixtures before opening the ring, reopened the ring after a
  failed row, and then saw the follow-up fixture's own `[CarriedSync] merged … starting supplies` line land
  in the next row's window. Take the mark AFTER the last fixture action, not before it.

## 2026-10-06 — Batch `20261006-g`: a gesture whose meaning is read at RELEASE time must be staged and released in one call

- **A sandbox whose service restarted underneath it stops starting programs, and the failure does not look
  like one.** The machine's sandbox service had crashed (`SbieSvc` stopped, exit code 1067) before the batch;
  the already-running sandboxed clients kept working — the driver keeps them alive — but every new launch
  failed. The shapes seen, in order: a modal `Sandboxie 启动` dialog naming `SBIE2331 启动服务失败 … 拒绝访问`
  (read it by enumerating the dialog's child windows and printing their text; the title alone says nothing),
  and then, once the service was back, a silent no-start whose only symptom was a Unity box
  `Fatal error — Failed to load mono` on that sandbox's desktop. Terminating every program in the box
  (`Start.exe /box:<box> /terminate`) and starting the box's Steam again fixed it completely — no reboot, and
  the other box, whose clients were already up, was untouched. **A box's stale process table is what refuses,
  not the game**: a launch that leaves no process, no log line and no dialog is that, not a broken install.
  Check the box's own file access with a `cmd.exe` copy probe before blaming a shadow — the mono DLL copied
  out of BOTH boxes byte-identical while the game still refused to load it.
- **A drag does not survive from one evaluator call to the next.** The game closes the inventory ring by
  itself between calls, so `mode=hover` followed by a separate release call leaves `buttonCount: 0` and the
  release falls through to the world path. Every gesture whose classification is decided at RELEASE time —
  the ring's focused body, `RemoteBackpackView.IsOpen` for `TransferToBody`, the cast button's own item — has
  to be staged AND released inside ONE invocation. That is the whole difference between the two attempts at
  the double-Tab take: two calls answered `radialOpen=False, buttonCount=0`, one call answered
  `TransferToBody captured … (slot 2, body <requester>)`.
- **A further Tab does not reopen what the second one closed.** With a remote proxy staged as the drag and the
  ring already shut, more `toggleinventory` presses left `radialOpen=False, buttonCount=0` while `dragItem`
  stayed set. Clear the drag with the probe, open the ring from a clean state, and drive the gesture in one
  call instead of adding presses.
- **A divergence warning is attributed by the SHAPE of the move, not by who drove it.** In one session: the
  operator's intent-driven swap warned twice per viewer; the OWNER's own purely local swap warned the same
  two lines; a move into free space warned nobody; a custody transfer warned once per viewer with its own
  message (`left the inventory without an event sync`). Take the owner-local control in the same session, or
  the replay gets blamed for the native path's behaviour.
- **A member joining a run in progress is a different code path from a member present at world entry, and it
  can fill the third client's log with warnings that belong to no row under test.** The late joiner logged
  `[Enemy] generation spawn pairing failed (69 host vs 69 guest …)` every 60 s for the whole session while the
  member present at entry read `mapping=True` in the same cycles. Read a third client's own entry-time lines
  before treating its warnings as evidence about the feature being accepted, and file what the entry path got
  wrong instead of folding it into a row's verdict.

## 2026-10-06 — Batch `20261006-h`: the expansion's target must be able to hold the child, and the item domain adopts by position as well

- **`Container.CanHoldItem` is asked BEFORE the refusal route, so a target that cannot hold the child at all
  makes the whole gesture a no-op.** The fixture for "the expansion's child load is refused" needs a target
  whose ceilings ADMIT the child: with a `pouch` target (weight ceilings 2.5 total / 1.0 per item against a
  1.5 `dogfood`) the native loop's `CanHoldItem` gate never opened, `flag` stayed false, the release fell
  through to the world path and the operator's whole source bag was dropped (`[DragFlow] release fell through
  to the WORLD path (no UI target hit).`). Read the capacities off the prefabs in one probe before staging
  (`Resources.Load<GameObject>(id)`, then the `Container` fields and `Item.totalWeight` — a property, not a
  field), and pick a target with room; the refusal this ticket wants is the one AFTER `CanHoldItem` passes.
- **`container-fill` addresses a container by TYPE, so two carried containers of one definition make the
  fill bind the wrong pair.** Nesting a second `trashbag` into the first failed with
  `LoadItem left trashbag outside the container (canHold=true, distance=0)` — the recipe had resolved the
  container to one bag and the item to the other, so the load was refused by `LoadItem`'s non-empty-container
  rule. Give each role its own definition (`trashbag` outer, `plasticbag` target, `duffelbag` source) instead
  of creating a second container of the type already in use.
- **The item domain has a positional adopt path too, and a same-frame pair loses its second child to it.**
  A drop report whose item has no local object calls `RemoteItemSceneOps.FindExistingAt` and BINDS an id-less
  same-prefab world copy within `AdoptTolerance` (1.5 units) instead of materializing; in this batch the
  second of two same-position reports bound such a copy and that copy was destroyed ~10–30 ms later
  (`[ItemBind] bound existing …` then `origin=OnItemDestroyed`), leaving the id `terminal` in the host's
  kernel and the guest's own destroy command refused as `InvalidTransition`. Read that pair as this shape —
  it is the item-domain sibling of `review/runtime-entity-markerless-bind-absorption.md`, not a fixture
  error — and know that a row asking for "both children in the world" cannot pass until it is fixed
  (`done/second-drop-report-loses-its-world-object.md`).
- **A swallowed key reads as a no-op, not as a refusal.** With the Online UI window open the game's own
  binds never reach `PlayerCamera.HandleInput`, and the `toggleinventory` hold reported `posted: true,
  heldAtEnd: true` while the ring stayed shut (`radialOpen: false`, `buttonCount: 0`) — no error anywhere.
  Close the window and read the toggle back (`radialOpen` / `buttonCount`) before staging the drag; the
  gesture then runs on the first attempt.

## 2026-10-06 — Batch `20261005-b`: name the line that grew the log, not the line that explains the state

- Symptom: after a layer change the two members were out of the world and the guest's rolling log grew from
  0.8 MB to 33.4 MB in about four minutes, so the batch restarted all three clients and lost the session.
- Cause of the LOG growth, read later from the client log that survived: `[ItemPhysics] settle` —
  an Information line inside the per-frame ease branch of the item follow pump, one line per item per frame
  while a copy's gap to the host's state does not close, which is what a diverged world looks like. It is
  4,445 of that log's 7,368 lines. The `[LayerMod] baseline divergence` warning the batch's record
  juxtaposes with the growth is real and per-arriving-snapshot, but that snapshot is the 5-SECOND keyframe
  (`WorldItemSnapshotStream`: `BaseIntervalMs: 5000`, measured at 8.3 s gaps) — a few dozen lines over the
  same four minutes, so the detector was right about the state and four orders of magnitude short of the
  volume. An earlier draft of this entry read the cadence off the 10 Hz item MOVEMENT stream, which never
  reaches that decision, and got 2,400; the number was wrong and is withdrawn everywhere it had reached.
- Change: a repeatable diagnostic now asks a repetition window (`LogRepetitionGuard`) keyed on the VALUE it
  reports, so one unchanged fact costs a bounded window of lines and its end still reports what was swallowed
  (`ItemDistanceLog` for the two per-frame correction lines, the snapshot diagnostics in `LayerModifierSync`,
  and the 10 Hz fluid region receive handler); `LogVolumeGateTests` pins the call sites and fails when a
  window call is removed. The batch's own attribution is recorded in
  `docs/backlog/done/layer-change-member-dropout.md` and corrected there rather than edited away.
- Read a log-growth figure by MEASURING the lines per class before naming a culprit: the diagnostic that
  explains the observed state and the diagnostic that produces the volume are often two different lines, and
  a ticket that names the wrong one spends its cycle on the wrong bound. A per-frame line at Information is
  the usual culprit — the level policy puts high-frequency triggers at Verbose/Debug for exactly this reason.

## 2026-10-07 — Batch `20261007-a`: a park window is only as long as the command that holds it

- Symptom: the layer-change fixture was driven as "park both members' inbound, send `skiplayer`, unpark" and
  read as covered; the three driver calls together took **0.4 s**, so the inbound was held for about a tenth
  of a second, the change went through unprotected and BOTH members left the world — the attempt whose whole
  purpose was to keep them in it. The same fixture holds the park for 9.4 s and the parked member keeps its
  body and its carried item.
- Cause: `drive-in-process.ps1 -Action recipe` opens a connection and evaluates one snippet, which is a
  fraction of a second on an idle client; a SEQUENCE of calls is not a hold. What a park covers is the
  wall-clock between arming and clearing it, and the change it has to cover takes seconds — the members were
  still out at +5 s and back only by +20 s.
- Change: a park that must cover a change keeps its own budget INSIDE ONE command — arm, act, an explicit
  `Start-Sleep` bounded under the 15-second `GuestHostSilenceWatchdog` ceiling, clear — and the parked member
  is read as the control (held: stays in the world; not held: it leaves). A park whose window is not measured
  is not a park.
- Also, two things this run cost a step each: `container-read` demands `guest=<owner SteamId>` for EVERY mode
  including `local` (the recipe declares the placeholder, so the driver refuses the call with a usage message
  — `-RecipeArg mode=local,guest=<SteamId>`), and a member left out of the world used to write its log without
  bound (`Remote body: no Body component in "Experiment" clone.`, measured at 7.25 MB/min; bounded later the
  same day by a window per (member, failure) plus a wind-down that reports what it swallowed —
  `done/remote-clone-warning-storm-on-member-dropout.md`), so a session that ends with a member still out is
  closed promptly rather than left running.
- Harvested by this run: `.acceptance/tools/log-census.ps1` takes a `log.ps1` mark and prints, per client, the
  lines and megabytes written since it plus a count per pattern — the size a bounded/unbounded row is judged
  by, split by producer. `log.ps1 -Action read` returns matching lines but never counts them, which is how a
  tail read gets mistaken for a measurement.

## 2026-10-07 — Batch `20261007-b`: a fixture's created item ids repeat, so they identify nothing

- Symptom: the re-run of batch `20261006-h`'s fixture produced the SAME instance ids for its staged items
  (`trashbag` 9492956821, `duffelbag` 18082891413, and the first fixture's two `dogfood`s 22377858709 /
  26672826005 — the ids batch h's own readings carry), which can read as "the old objects are still here".
- Cause: the kernel allocates ids in a deterministic sequence, so the same staging order in a fresh world
  repeats it. An id is not a session fingerprint; only the marks and the log timestamps separate two runs.
- Change: compare two batches by their marks and timestamps, never by an instance id, and read an id in a
  record as "an item this fixture created in that session".

## 2026-10-07 — Batch `20261007-b`: an absence row's window must open after the staging

- Symptom: both gesture windows read zero `[CharSync] divergence`, while a whole-session scan found four per
  viewer — for the `trashbag`, for the `duffelbag` and for the second fixture's two created `dogfood`s.
- Cause: `item-provide mode=create` + `container-fill` is the declared setup substitution and carries its own
  create shape — an item the fact table never saw arrives in the 1 Hz snapshot, one line per created item on
  the first snapshot after it. A window opened before the staging would have blamed the gesture for them.
- Change: take the mark after the staging and name the staging's own lines with their timestamps, which is what
  keeps a zero-monitor row honest (record `…-drop-pending-single-slot-overwrite-20261007-b.md`).

## 2026-10-07 — Batch `20261007-b`: the local probe client cannot deliver a submission that carries a quote

- Symptom: an ad-hoc probe file sent through `.acceptance/tools/hotrepl-eval.mjs` answered
  `{"ok":false,"error":{"kind":"internal","code":"runtimeException","message":"(1,1): InteractiveHost"}}`,
  while `1+1` and `System.Linq.Enumerable.Count(new int[]{1,2,3})` on the same client answered normally; a
  submission as small as `"hi"` failed the same way, and so did every lambda that returns a string.
- Cause: the submissions that fail are exactly the ones carrying a double quote, and they fail at `(1,1)`, so
  the code string is being mangled on the way in rather than mis-compiled. The committed recipes are full of
  quoted literals and run through `drive-in-process.ps1 -Action recipe` unchanged, so the driver's channel is
  not affected. Which layer eats the quote was not attributed.
- Change: an ad-hoc probe goes through the driver's recipe channel — an existing recipe whose reader already
  answers the question (`container-read`, `item-world-read`, `item-watch`), or a new committed recipe — and the
  local probe client is used for quote-free expressions only.

## 2026-10-07 — Batch `20261007-b`: a documentation-only commit above the build commit is named, not hidden

- Symptom: the run's tree was `208bab68` while its artifact reported `0.1.0+2675221c…` — two commits apart,
  with only `docs/` changes in between.
- Cause: the `+<sha>` in `ProductVersion` is the commit the DLL was BUILT from, and the deployment rule commits
  before it builds; a batch that runs later, on a tree whose only new commits are documentation, still runs the
  same code.
- Change: the record names BOTH the run's commit and the artifact's `ProductVersion` and says the delta is
  documentation-only; `tools/verify-deploy.ps1` against this tree's build output before the clients start is
  what makes that statement checkable.

## 2026-10-07 — Batch `20261007-c`: a park that must hold SEVERAL clients is one command, and the census needs `&`

- Symptom: the row needed BOTH members held through the host's `skiplayer` change. The earlier batch's 9.4 s
  park had come from the wall clock BETWEEN separate tool calls — unbounded, unreproducible, and armed on one
  member only — so the row's pass condition had never been reached on both. This batch armed host, guest and
  alt, drove the change and disarmed inside ONE `pwsh` invocation (arm → `skiplayer` → explicit
  `Start-Sleep 9` → disarm) and BOTH members stayed in the world with their carried item, while the same
  session's unparked control still dropped both and returned them inside 30 s.
- Cause: the hold a park provides is the wall clock between arming and clearing it, and the change it must
  cover takes seconds (the members were out at +6 s in the control). A sequence of driver calls is not a hold,
  and the client's own `GuestHostSilenceWatchdog` caps the hold at 15 s — so the shape is "one command, one
  explicit sleep", not "one call per member".
- Change: arm EVERY client the row needs held, read each arm result (`armed: true`) BEFORE driving the change
  so a failed arm cannot silently produce an unprotected attempt, hold with an explicit `Start-Sleep`, then
  disarm inside the same command. `.acceptance/20261007-c/runbook-20261007-c.md` is the recorded shape.
- Also: `.acceptance/tools/log-census.ps1 -Patterns 'a','b'` invoked through `powershell -File` printed one
  comma-joined pattern name with `= 0` for every client while the log held eight hits. `-File` passes every
  argument as a literal string, so the array arrived as `a,b` and matched nothing. Call the helper with `&` in
  the current session, and treat a comma-joined pattern name in the output as the tell that the census
  checked nothing.

## 2026-10-08 — Batch `20261008-a`: the parked spot is the map's corner, and it decides what the rows can show

- Symptom: the standing-object rows that ask for a presentation judgement and for local decay could not be
  read the way the ticket's plan assumed. The parked objects sit at the world origin, which on this world is
  a SOLID block at the map's corner (`world 1024x1024`, cell (0,0) solid) more than 450 world units below
  the inhabited floor and unlit; and a copy forced to condition 0 did not decay and was not destroyed.
- Cause: two independent facts, both properties of the design plus the world. The holder parks everything at
  (0,0) on purpose, so a camera can only reach it by teleporting a body into rock, and the frame there is a
  dark rock face in which a disabled renderer and an unlit sprite look identical. The decay and the
  destroy-at-zero both sit behind `Item.Update`'s live-chunk guard, and the corner's chunk renderer is off,
  so a parked copy neither rots nor dies locally — its condition moves only when the data moves it.
- Change: judge the presentation row on the machine read (every `Renderer` in the subtree disabled, collider
  disabled, every sound-playing component disabled, `Light2D` off — and read a source's `isPlaying`, not only
  its `enabled`, because one prefab's `AudioSource` stays enabled while the component that plays it is off) and
  offer the frame as a residual for the user instead of calling it a visual pass; read the decay row as "not
  reachable while parked" and cite the chunk-renderer guard. The parked spot's own address (origin, inside
  rock, at the map edge) is worth stating in any future standing-object row before the session, because it
  decides both.

## 2026-10-08 — Batch `20261008-a`: the drag gesture needs the pointer ON the target, and the camera sits below the body

- Symptom: the cross-player eat could not be triggered by the product's own drag gesture. A local item
  released over the target needs the target inside 1.5 world units of `Camera.main.ScreenToWorldPoint(
  Input.mousePosition)`, and the run may not move the pointer; placing the target at the pointer's own world
  point satisfied the overlap test (the log line `[DragUse] dropped … on …` appeared) but the product's
  line-of-sight gate then refused it (`[Visibility] blocked … by ground at (11.0,466.0)`), because that point
  is always below the feeder's feet.
- Cause: two measured geometry facts. This game's camera sits ~3.8 world units BELOW the player's body
  (body y 468.615 / camera y 464.761 on the guest; 463.885 / 460.209 on the host), so even a pointer sitting
  essentially on the window's centre maps to a world point below the feeder's feet — the host's cursor also
  sat below its own centre (offset −4.4 world units at zoom 2, while the guest's read +0.02). Zooming the
  camera for the release call (a write that the game reverts within a second, so it only holds inside ONE
  eval) shrinks the offset but leaves it below the feet.
- Change: drive the chain through the production entry the gesture itself calls
  (`IPlayerInteractionControl.SendUseRequest(target, item, instanceId, dose)`) with the target standing beside
  the feeder, where the product's own line-of-sight gate, family verdict, host admission, affected-side eat,
  outcome report and commit still decide; the committed `remote-gesture` recipe remains the way to prove the
  gesture's own half (its `[DragUse] dropped …` lines), and the record names which half came from which. A
  future run that needs the whole gesture in one shot should first find a place where the pointer's world
  point is in open air with a clear line to the feeder — and remember that a release the gesture refuses still
  runs the native path and DROPS the item (`[DragFlow] release fell through to the WORLD path`).

## 2026-10-08 — Batch `20261008-a`: a committed recipe can fail on fractional arguments

- Symptom: `drive-in-process.ps1 -Action recipe -Recipe body-place -RecipeArg x=74.5,y=470.5` answered
  `eval 'recipe:body-place' failed: (1,1): InteractiveHost` on both clients, while the same recipe with
  integer arguments (`x=10,y=490`) ran and moved the body. An ad-hoc probe with the same body of code and
  fractional values worked.
- Cause: the recipe's own placeholder shape. `var x = {{n:x}};` makes an `int` for an integer argument but a
  `double` for a fractional one, and `new Vector3(x, y, body.transform.position.z)` has no implicit
  `double`→`float` conversion, so the submission does not compile — and the evaluator reports that as the
  opaque `(1,1): InteractiveHost` rather than as a C# error.
- Change: the recipe casts its placeholders (`var x = (float)({{n:x}});`), the shape every other reader under
  `tools/acceptance/recipes/` already uses; a recipe whose arguments are numbers should be smoked with a
  fractional value before a batch depends on it, because an integer-only run never sees this.

## 2026-10-08 — Batch `20261008-a`: the host's own starting supplies carry no instance id until they enter the domain

- Symptom: the guest materialized nothing for the host's starting `emergencylight` while the host materialized
  the guest's, and the data-side read showed the host's row with `InstanceId 0` (`[StandingItem] …: 0 carried
  row(s) in the data, 0 standing object(s) alive.` on the guest against `1 carried row(s) … 1 standing
  object(s)` on the host).
- Cause: by design, and documented on `CarriedInventoryReporter`: the guest reports its whole carried
  inventory (self-assigned ids) once its generation finishes, while the host's own supplies are the authority
  and get their ids lazily, on the first domain entry (`EnsureId` in the use/slot/drop chains). A carried row
  with no id is exactly the row `StandingItemPlan` leaves out.
- Change: a standing-object fixture binds the host's own items first — `item-provide … mode=create` (its
  `Body.PickUpItem` allocates the id) or any local slot move/use — and the record states that the id-less row
  was excluded by the plan's own rule rather than missed by the materializer.

## 2026-10-08 — Batch `20261008-a`: a frame taken while the player is unconscious proves nothing (the user caught it)

- Symptom: the presentation row was judged from `w1-host-parked-spot.png`, a dark, empty frame captured at
  the parked spot, and the record offered it to the user as "nothing to see". The user answered that the
  character was unconscious, so its view was dark and blurred — "你能看到才有鬼" — and that crystal enemies
  could be heard attacking it at the time.
- Cause: the run never read the OBSERVER's own state at capture time. The same session had read
  `consciousness 0` minutes later, and the logs carry the host's pain/`death1`/`death4` vocalisations from
  19:38:09 and 56 `[EnemyAttack] … CrystalLunge` announcements between 19:38:01 and 19:41:23 — the capture
  sat inside that window. A blinded or unconscious player's rendering is dimmed by the game, so a dark frame
  is exactly what a conscious-observer check would have predicted.
- Change: judge no visual row without reading the observing player's own state in the same step as the
  capture (`alive`, `conscious`, consciousness value, any blackout/blind effect) and writing that reading next
  to the frame in the record. A frame whose observer state is unknown is not evidence, and a dark frame is
  never "nothing visible" without it.

## 2026-10-08 — Batch `20261008-a`: a fixture item's side effects are the run's own, and they were not watched

- Symptom: the fixture's `exposedcore` (chosen for the "destroys the item and spawns two crystal enemies"
  shape) rotted to condition 0 on its owner and did exactly that: two `crystalenemy` objects appeared at the
  guest's position (19:37:58), CUO mirrored them to the host as runtime spawns, and they attacked the host —
  56 lunges over three and a half minutes — leaving it unconscious. Nothing in the record mentioned it; the
  user heard it from the machine's audio.
- Cause: the run treated its own fixture as inert. An item with a self-destruct or world-spawning behaviour
  runs that behaviour on the side that owns the real item, and the resulting entities are real, synced and
  hostile; the run's census searched for `[ERR]`/`[WRN]` and for standing-object lines, never for the spawn
  and attack lines its own fixture could produce.
- Change: a fixture item with a self-destruct, world-spawning or hostile side effect is either left out or
  declared in the scope page WITH its consequences, and the run's log census searches for the lines that
  behaviour produces (here: `[EntitySpawn]`, `[EnemyAttack]`, `[CharacterSound] Pain`) over the whole run
  window. An absence row ("no entity appears here") needs a census that spans the event that could create
  one: this batch's census was read thirteen minutes before the spawn, so it was a snapshot, not evidence.

## 2026-10-08 — Batch `20261008-a`: a verdict moved to `done/` came back when the evidence behind it fell

- Symptom: the batch accepted the ticket (fifteen rows, one residual) and moved it to `done/`; one user
  message about a frame took that back — the ticket is `todo/` again with `- Status: Todo — Rejected (…)`,
  and the index and five prose references moved with it.
- Cause: the verdict rested on a frame that could not carry it (previous entry), and the review that ran
  before the move checked the record against the artifacts but could not know what the run's own world had
  been doing (the crystal attack) or what the user saw on screen.
- Change: for a row whose evidence is a picture of the world, the record names the observer's state and the
  census window; and a rejection costs the same edit as an acceptance — the folder, the status field, the
  index row and every reference move in one change, which is exactly what the batch did.

## 2026-10-11 — Batch `20261011-b`: a minigame created in the same call as the panel open holds a limb the renderer replaces

- Symptom: the driven grip slip failed twice with a bare `internal runtimeException`, and the recipe's own
  step report then named it: `step=break-grasp`, `System.NullReferenceException`, from inside the native
  `ShrapnelMinigame.BreakGrasp`. The same recipe and the same drive shape had worked in batch `20261011-a`.
- Cause: `grip-slip-drive mode=start` opened the remote medical view AND started the session in one call, and
  the renderer created a fresh display body for that peer 85 ms after the minigame was built — the minigame's
  `limb` field then pointed at the replaced body's limb, so `TryBeginGripSlip` returned null (no window) and
  the native call dereferenced a dead limb.
- Change: open the view in its own call and start the session in the NEXT one (the display body has settled by
  then), and make the break path name the step that threw — `step=break-grasp` plus the host log's
  `Remote body created` line, one frame after the minigame started, is what located this one. The catch also
  reports the scene at the failure (`minigameLimbNull`, `limbBodyNull`, `minigameLimbIsDisplayLimb`, …); those
  fields were written AFTER this run's failures and no failing drive has exercised them yet, so they are a
  diagnosis aid rather than evidence. A drive that builds a native object against a display body needs one
  call between the focus and the action.

## 2026-10-11 — Batch `20261011-b`: a host-side removal is undone by the target's next report, and a flag row would never have seen it

- Symptom: a driven splint removal on a guest logged the whole operation on the host (`started SplintRemoval
  operation 4`, `awarded removed splint … slot 0`, `terminal Completed`) and the target's own client logged
  `local target body updated (terminal=True)` — yet the splint was still on that limb afterwards, the
  authority's copy regained it at the next report, and driving the same removal again awarded a SECOND splint.
- Cause: the removal is host-side only. `OtherMedicalRemovalApplier` clears the component in the host's
  stored character data; the target is reached through `CharacterDataSync.ApplyHealState`, which maps the limb
  FIELDS (so `Splinted=false` really lands) and then calls `LimbComponentStateCodec.Apply` — which is
  additive-only and never removes a component the message omits. The local component survives, and a guest
  body is report-driven, so the target's own report put it back.
- Change: a removal row reads the COMPONENT, not the flag — a flag-only reading passes while nothing was
  removed — and reads it on the target's own client AND in the authority's copy one report interval later.
  The defect is `docs/backlog/done/medical-removal-limb-component-not-applied.md`.

## 2026-10-11 — Batch `20261011-b`: a display clone that instantiates the game's own component runs the game's own `Start`, every frame

- Symptom: the moment a peer's limb carried a splint, the client with the remote medical panel open on that
  peer threw `NullReferenceException` every frame — 5487 exceptions in one window — with the stack
  `OnLimbWound.Update` → `SplintLimb.Start`'s icon predicate → `GetComponent`; closing the panel stopped it
  instantly and reopening it resumed it.
- Cause: the display path applies the peer's limb state to a body CUO only renders
  (`RemoteMedicalCoordinator` → `LimbComponentStateCodec.Apply`), and that codec adds the GAME's own
  component. Its `Start` registers a wound-view icon whose removal predicate dereferences a limb; when the
  display body is rebuilt under the focus the icon's limb is gone and the panel's own `Update` throws.
- Change: when a display path carries component state, ask what the game's own component does on `Start` and
  `Update` before instantiating it — the state, not the behaviour, is what a clone needs, and a per-frame
  exception storm can hide behind an unrelated symptom (this one first looked like a broken shrapnel drive).
  The defect is `docs/backlog/done/display-clone-limb-component-runs-native-start.md` (mechanism corrected
  by batch `20261011-c` — see the entries at the end of this file).

## 2026-10-11 — Batch `20261011-b`: extract a client's log before you restart it

- Symptom: the run had to restart all three clients to get a clean world, and the evidence for the
  exception storm lived only in the host's rolling log, which BepInEx rotates on the next start.
- Cause: the rolling log is per-run; a restart renames it away, and a record that cites "the log" afterwards
  cites a file the next reader cannot open.
- Change: before quitting or restarting a client, copy the window that carries a finding into the batch's
  artifact directory as its own file (`host-splint-icon-nre-storm.log.txt` here) and cite that file in the
  record. The same applies to a mark/read pair whose offsets belong to the old file. A client RESTART also
  rotates the rolling log into a gzipped archive beside it (`2026-10-11-8.log.gz` held this batch's first
  storm), so a window that was missed can still be recovered from the archive with a `GZipStream` — this
  batch's storm digest and its close/reopen counts were rebuilt that way after a review pointed out that the
  first digest promised two timestamps it never printed.

## 2026-10-11 — Batch `20261011-c`: a drive that builds the surface by hand invents the defect it drives

- Symptom: batch `20261011-b` filed a per-frame `NullReferenceException` storm on the client whose medical
  panel was open on a splinted peer, and recorded it as a defect of the display path — "a display clone runs
  the game's own limb component".
- Cause: the two open recipes built the focus by hand — `RemoteMedicalView.Open(<the peer's LIVE render
  clone>)` from `RemotePlayerRenderer.TryGetRemoteBody` — because the run's member probe reported
  `inWorld: false` for every peer. Production opens the same view through `RemoteMedicalCoordinator.Open`,
  which builds a separate PARKED `MedicalDisplay_*` copy, created inactive. With a live body under the focus,
  the per-frame snapshot apply added the game's own `SplintLimb` where the game's own `Start` runs, the icon
  it registered was parented to the panel, and the focus's own teardown then destroyed the clone — the icon's
  limb was gone and the panel's `Update` threw every frame. The probe's reading was itself the bug (next
  entry), so the shortcut was never necessary.
- Change: a recipe that needs a user-reachable surface opens it through PRODUCTION's own entry and reports the
  guard readings beside the result, so a refusal names itself instead of being worked around; and where the
  surface must be a specific kind of object (a display-only copy, a clone, a proxy), the drive reports the
  witness for it — here `displayBodyName`, with a `MedicalDisplay_*` name required — so a drive cannot
  silently exercise a different object than the one the row is about. The same rule caught the second half:
  the earlier recipe's own comment ("the view is torn down between driver calls") described a teardown its own
  shortcut had caused.

## 2026-10-11 — Batch `20261011-c`: a probe that reads a field as a property reports `false` for ever

- Symptom: batch `20261011-b`'s member probe reported `inWorld: false` for both peers while each client's own
  `state` reported `true` and the host accepted medical operations whose guards read that same fact. The run
  recorded the contradiction as a limit it did not resolve, and worked around it.
- Cause: `MemberPresenceTable.MemberPresence.InWorld` is a public FIELD. The probe read
  `member.GetType().GetProperty("InWorld")`, which returns null for a field, and its serializer then printed
  `false` — for every member, in every session, without an error.
- Change: a probe reports a production PREDICATE's own answer (`SessionService.IsRemoteInWorld`) rather than
  re-deriving the rule from a member shape, and where it must read the member it accepts a field or a
  property. The reading a probe invents is worse than no reading: this one sent a whole batch down a
  workaround that manufactured the defect the batch then filed.

## 2026-10-11 — Batch `20261011-c`: a frame-deferred destruction is not a removal

- Symptom: the fix that adds a component-removal half to the receiving path could still lose the removal: the
  receiver re-reports its character snapshot IN THE SAME CALL that applies the authority's message
  (`MedicalOperationApply.ApplyProgress` ends with `ReportInventoryChanged`), and a capture in that frame
  still finds a component that `Object.Destroy` has only scheduled.
- Cause: Unity destroys at the end of the frame; the report that carries the client's own state back to the
  authority is built inside the apply.
- Change: a removal whose result must be visible to a same-frame reader uses `Object.DestroyImmediate`, after
  checking that the component has no `OnDestroy` of its own. The one-client probe
  (`tools/acceptance/recipes/limb-component-apply.cs`) is what proves the whole half without a session: it
  captures the caller's own body, edits ONE limb row to the authority's removal shape, applies it through
  `CharacterDataSync.ApplyHealState` and reads the limb back — the flag landing while the component survives
  IS the defect, in one call, on the unfixed build.

## 2026-10-11 — Batch `20261011-c`: a drive's witness must be production's own fact, not a naming convention

- Symptom: the batch's scope page declared that every open must report a `displayBodyName` starting
  `MedicalDisplay_`, and the first production open answered `displayBodyName: "Body"` — the drive's own
  witness would have refused a focus that was exactly right.
- Cause: the coordinator names the cloned ROOT `MedicalDisplay_<steamid>` and the `Body` component sits on a
  CHILD; the recipe read the body's own name. The convention was a guess about a shape the code states
  differently.
- Change: the witness is the fact production itself acts on — the `RemoteMedicalDisplayBody` marker the
  coordinator puts on the copy's root, which is also what the codec's display guard reads — with the root's
  name reported beside it as information. A drive that has to say "this object is the kind the row is about"
  should read the mechanism's own marker; a name it can compare is at best a second-best and at worst wrong
  about which object in the hierarchy carries it.

## 2026-10-11 — Batch `20261011-c`: an icon row is judged by removing the state, not by recognising a sprite

- Symptom: the display rows asked whether the panel still draws a teammate's splint and tourniquet. A frame
  alone leaves the identification resting on "that looks like the game's icon".
- Cause: the icons are the game's own sprites and a reader has only the picture.
- Change: the run judged the row as a pair — the icon present while the state is, and GONE after the state
  is removed by a driven action — which turns "this looks like a splint icon" into "this icon's lifetime is
  that state's". The same pair doubles as the row's evidence for the removal's display half, and it is why
  the peer's limbs are seeded with one component each: three icons, three states, one removal.

## 2026-10-11 — Batch `20261011-c`: a per-peer exception census must name the known startup exception

- Symptom: each sandbox client logged exactly one `[Unity:Exception]` in the run, which would read as "the
  peers were not clean" beside the operator's zero.
- Cause: `TypeLoadException` about `System.ComponentModel.DataAnnotations.RequiredAttribute` is thrown once
  per client at plugin startup, before any session — the same line every client has logged since batch
  `20261011-b` filed it as unrelated.
- Change: the census counts exceptions per peer AND names the one known startup line with its timestamp, so
  the record separates "the session was quiet" from "the client logged its usual startup line". A census
  that only counts leaves the next reader to re-derive which line is which.

## 2026-10-11 — Batch `20261011-c`: check that the artifact exists before citing it

- Symptom: one reading — the authority's stored copy after the tourniquet removal — was taken in the run and
  returned the expected value, but the driver's `-JsonPath` file for that one call was never written (the
  same call shape wrote fifteen others). It was noticed after the clients had quit, so it could not be
  re-read, and the row had to be recorded with one of its two surfaces unfiled.
- Cause: not established; what IS established is that the record was written from the transcript rather than
  from a file, and nothing in the run noticed until the citation was checked.
- Change: list the batch directory and check each cited artifact exists BEFORE writing the record. A reading
  that only lives in the transcript is not evidence a later reader can open — and a row whose verdict leans
  on it should say so in its Limits rather than cite a file that is not there.

## 2026-10-11 — Batch `20261011-d`: a store the FRAME makes needs the pointer where the frame looks

- Symptom: row 7's favourite half kept reading nothing — no capture line, no owner replay — while the key
  hold itself answered `posted: true, heldAtEnd: true`.
- Cause: the native store is `Input.GetKeyDown(favourite)` on the first `InvButton` the frame's OWN UI
  casts contain (`PlayerCamera.cs:1745`), and the capture is CUO's bracket over that same frame. Two things
  therefore have to be true at once: the physical cursor over a ring button, and a display-proxy drag
  staged so the bracket opens. A cursor anywhere else makes the store simply not happen — and with the
  pointer outside the client window the ring is turned off within a frame (`radialOpen = false` once the
  ring's x is more than `600 * uiScale` from the pointer's x), so `mode=open` reports `viewOpen: true` and
  the very next call reads `radialOpen: False, buttonCount: 0`.
- Change: for this family, read the geometry FIRST (`remote-gesture mode=list` now reports the pointer, the
  ring, and each button's screen position with `pointerInside`), and know that a button can be brought under
  the pointer only by moving the FOCUSED body — `mode=aim-ring` converts the needed screen shift into a
  world delta through the game's own camera. No tooling may take the pointer (decision 237; the gate bans the
  cursor and `SendMessage` families), so a run whose user is elsewhere cannot drive this row: record it
  `unproven` with the geometry reading, and leave the one-attempt proof that the store path is live — batch
  `20261011-d` read the game's own store being put back and refused for the missing drag bracket.

## 2026-10-11 — Batch `20261011-d`: a staged proxy drag is LIVE between calls, and it will drop the fixture

- Symptom: two of the owner's fixture items (`smallmagazine`, `aed`) vanished from its body mid-run, the
  peers' clone fell to `host char data (3 items)`, and every later `mode=open` refused.
- Cause: the drive stages `camera.dragItem` for a display proxy and leaves it across calls — the scene state
  a real drag holds. Any release the game then sees (the user's own mouse, a lost focus, a stray press)
  runs the whole native release path on it, and the ring's buttons stop matching the clone. The same batch
  also lost the two items to a different path: the owner was teleported ~24 units away and fell ~45 units,
  and the GAME dropped them (`[ItemTrace] op=65/66 … origin=OnItemDropped` with no CUO intent).
- Change: stage a drag and press the key in adjacent calls, and CLEAR the drag when the step is over — the
  clear-the-drag rule was already in the local run notes and this batch paid for skipping it. Keep the
  fixture's host ON the surface: a teleport that leaves it falling empties the wardrobe the rows need, and
  the loss is silent until a peer's clone count or a census is read.
## 2026-10-11 — Batch `20261011-d`: `quit` is asynchronous, so a relaunch is a duplicate instance

- Symptom: the operator's client was quit and relaunched in the same command block; the launch did nothing
  (no process, no endpoint) and the user saw the duplicate-instance refusal.
- Cause: the quit returns as soon as the request is sent; the process is still alive for seconds afterwards,
  and Sandboxie refuses to start a second instance of the same program in the same box.
- Change: after `quit`, read the process list until the client is gone, then launch. The guest's log ends
  with `Steam API shut down` — that line, or the missing process, is the gate, not the quit call's answer.

## 2026-10-11 — Batch `20261011-d`: the fixture vocabulary is the game's own item table and its own readouts

- Symptom: "a combineable pair" and "a receiver with an empty battery slot" had been named as unbuilt
  fixtures by four batches, because the deciding facts live in ASSET data (`ItemInfo.combineable`, the
  `battery` tag, which prefab spawns with a battery) rather than in code.
- Cause: the run had no way to ask the game what its items are.
- Change: `recipes/item-census.cs` answers it from the game's own registry (`Item.GlobalItems`, filled by
  `Item.SetupItems`): `mode=combineable` lists the ids that admit a pair, `mode=tag:<t>` the ids carrying a
  native tag, `mode=info:<id>` one id's stats, and `mode=body` every carried item with the flags, tags,
  battery slot, ammunition count and liquid total the native branches read. `9mmround` + `smallmagazine` is
  the combine whose result is a NUMBER (`AmmoScript.rounds`, the round destroyed by the call) — prefer it
  over the condition merge, which is a no-op when both items are at full condition.

## 2026-10-11 — Batch `20261011-e`: the evaluator freezes the game's own frame loop

- Symptom: a probe queued the game's own favourite key on the client's own window and then spun on
  `Input.GetKeyDown` for 400 ms; it polled 12 million times, read `framesAdvanced=0` and
  `timeAdvanced=0`, and never saw the key. A second probe spun 1.5 s with the same result.
- Cause: an eval runs ON the main thread and blocks it — the game's frame loop does not advance while a
  snippet is executing, so no message queued from inside one can be pumped by a frame the same snippet
  sees. (This supersedes the 2026-10-01 note that an eval "runs on a worker thread"; the measurement
  above is the direct one.)
- Change: a row whose native gate is `Input.GetKeyDown` cannot be driven by "queue the key, then invoke
  the mutation" inside one eval, and cannot be driven by two evals either unless the pair is timed onto
  consecutive frames. The recorded shape for that attempt is
  `.acceptance/20261011-e/two-evals.mjs` (one socket, two submissions, an optional spacing in ms) —
  general enough to move into `tools/acceptance/` when a batch needs frame-timed pairs again. The
  measured frame gaps were 1, 2, 3 and 5 frames for 0/16/33/50 ms of spacing.

## 2026-10-11 — Batch `20261011-e`: a pointer outside the client window makes the UI raycast refuse everything

- Symptom: the run staged the ring's own anchor (`RemoteBackpackView._smoothPosition`) AND the ring
  transform so the target ring button sat exactly under the physical pointer — the button's own
  `Overlaps` gate then read true — and the store still never ran. The player's own key press in the same
  session (the user at the mouse, pointer inside the window) did fire it, and the run's accidental
  reading of that press is the only `[FavouriteMark] marked` line the operator's log ever held.
- Cause: `GraphicRaycaster` normalises the event position by the screen size and returns when it leaves
  the unit square ("if it's outside the camera's viewport, do nothing"), so the frame's own cast list is
  empty whenever the cursor is beyond the window — whatever the ring's position is. The committed
  `hover`/`aim-ring` staging moves the ring to the pointer; it cannot move the pointer into the
  viewport, and nothing else can either (`AcceptanceDriverGateTests` bans the cursor families).
- Change: a row whose gesture is a real pointer hit needs the pointer inside that client's window; the
  run's only legal levers are asking the user to place it (the batch scope's option 1) or a rule decision
  about moving the window itself (option the scope names as not the tooling's to take). Staging the ring
  remains necessary but is never sufficient while the pointer is outside.

## 2026-10-11 — Batch `20261011-e`: the key channel answered `posted` and never reached the game

- Symptom: `key-hold` (the committed way to hold one of the game's own keys) answered `posted: true` for
  both the down and the read steps, and its own read-back then reported `heldAtStart/heldAtEnd: false`
  on three consecutive calls — the game never saw the key. Batch `20261011-d` had read `heldAtEnd: true`
  for the same recipe, the same bind and the same kind of client.
- Cause: not established. The client is the same build, in the same sandbox; the window handle the recipe
  posts to differs from the earlier session's. What IS established is that the message is queued on the
  client's own window and the client's own input state never changes.
- Change: a batch that builds a row on the key channel verifies the channel FIRST (one `down`, one `read`,
  and the state in the read) before spending a session on the row; a `posted: true` is not evidence that
  the game holds the key. The finding is recorded on the batch's record as a capability limit.

## 2026-10-11 — Batch `20261011-e`: fixture items do not survive a cut, and a body that fell to death cannot pick anything up

- Symptom: three items created on the owner's body by `item-provide mode=create` were gone from that body
  after the cut + continue (the character came back carrying only its own starting item), and a later
  drop of a fourth one was reported by the owner (`[ItemDropped] … at (1.1,473.1)`) but is visible to
  neither client afterwards. The operator, meanwhile, could not pick anything up at all: the recipe's
  native path answered `no-free-slot`.
- Cause: two separate mechanisms. The fixture items' character-record round trip does not bring them back
  (the archive carried the mark, which is a character-record field, but not those carried items); and the
  operator's body had fallen ~57 world units earlier in the run — `body-force` revived it
  (`brainHealth=100 consciousness=100`) but its LIMBS stayed at zero force, so the game's own
  `InventorySlot.canPickUp` (`conscious && limb.totalForce >= 0.1`) refuses every slot.
- Change: plan the rows that need a carried item AFTER every cut/continue cycle, or re-create the fixture
  after it; and check `canPickUp` (six slots, one probe) before a row that needs this body to take
  something. A fall inside the run's own staging costs the rows that need hands.

## 2026-10-11 — Batch `20261011-f`: a remote ring button reaches the pointer by moving the CAMERA, and the pointer must be inside the window first

- Symptom: five batches could not put a remote inventory button under the physical pointer, so the
  favourite gesture could never be arranged; batch `20261011-e` had already proved the frame's raycast
  refuses a pointer outside the viewport.
- Cause: the remote ring is anchored to the FOCUSED clone's screen position (CUO's own anchor) and its
  buttons sit around that anchor, so where a button lands is decided by the clone's position relative to
  the camera — never by the pointer. `remote-gesture mode=aim-ring cast=<n>` is the tool that converts the
  screen shift the row needs into the world shift that produces it. The LOCAL ring behaves the other way:
  with the bag open it stays fixed on screen (moving the pointer did not move it), so its buttons are
  addressed by putting the pointer on the coordinates `mode=list` reports.
- Change: the order that works — (1) bring the client's window to the foreground and put the pointer inside
  its client area, (2) `mode=aim-ring cast=<n>` for the world delta, (3) move the LOCAL body by the NEGATIVE
  of that delta (`body-place` takes integers; half a world unit is about eight logical units), (4)
  `mode=list` until the target button reads `pointerInside: true`, (5) `mode=hover cast=<n>` to stage the
  drag (it answers `castUnderPointer: 0` when the button is under the pointer), (6) the press. The ring
  stays open while it sits within `600 * uiScale` of the pointer, which the same read reports.

## 2026-10-11 — Batch `20261011-f`: a real key press needs the FOREGROUND window, and the user said so first

- Symptom: the batch planned to ask the user for one press; the user answered that the game and the chat are
  two windows and only one can hold the focus — so "leave the pointer there and press the key" cannot be
  done while reading the instruction on screen.
- Cause: Windows delivers a key to the foreground window only, and clicking a title bar to raise a window
  moves the pointer, which is the other half of what the gesture needs.
- Change: raise the window from inside the run instead of asking for a click — `AttachThreadInput` to the
  foreground thread plus `SetForegroundWindow` (a click on the title bar is refused while another window
  covers that point) — then read the client's own `focused` and `inside` before the press, and press without
  switching windows. A batch that needs the user's hand asks for the press alone, never for a window switch
  between two instructions.

## 2026-10-11 — Batch `20261011-f`: a transferred item's adoption lands one beat after the pickup

- Symptom: the census taken in the same call as the pickup read the item `favourited=False` while the local
  mark entry was already gone; the same census eight seconds later read `True`.
- Cause: the adoption rides the character-data path, so the item's own flag is written after the transfer
  call has returned.
- Change: judge a "the mark became the item's own" row on a re-read and on the adoption's own log line, not
  on the first census after the transfer. The transfer itself was reliable this time as an owner drop plus
  `item-provide mode=pickup`; batch `20261011-e` failed to move the item only because that operator's limbs
  sat at zero force, so check `canPickUp` before a row that needs hands.

## 2026-10-11 — Batch `20261011-f`: `item-world-read` did not see a just-materialized world item

- Symptom: after the owner's drop the operator's own log showed `[ItemSpawn] materializing paincream
  (id …) at (3.0,505.1)` and the pickup then found that item, while `item-world-read x=0,y=507,radius=30` on
  the same client answered `count: 0`.
- Cause: not established. The reading echoes the pair it was given as `cellX`/`cellY`, so the position used
  may not have named the item's own cell.
- Change: judge "the item is in the world" on the spawn line plus the pickup that consumes it, and treat
  `item-world-read` as an unproven witness for a just-dropped item until its coordinate contract is pinned.

## 2026-10-11 — Batch `20261011-f`: a one-batch actuator authorisation is recorded as such, and the rule it bends is not changed by it

- Symptom: the gesture the batch needed exists only as a real pointer hit plus a real key press, and the
  standing rule forbids the tooling from taking either. The user, present at the machine, authorised the run
  to drive both — for this batch only.
- Cause: the rule's purpose is that the tooling never takes the machine's input on its own initiative. A
  recorded, user-granted, single-batch authorisation does not violate that; a silent exception would, and so
  would a permanent change nobody asked for.
- Change: the authorisation is stated in the batch scope and in the record, the actuator lives only in the
  batch artifact directory, and the run asked the user which of the two they meant (this once, or a rule
  change) before touching the pointer. Nothing under `tools/` gained a cursor or a keyboard call, and
  `docs/acceptance/AGENTS.local.md` carries no licence for the next batch.

## 2026-10-11 — A development probe on a CONTINUED world: the first driven act lands inside the restore's own wipe window

- Symptom: `continue-run` answered `inWorld: true`, and the `item-provide mode=create` driven right after it
  answered `ok` with `instanceId: "0"` — the created item was gone from the local tree a moment later. Driven
  again after the restore had settled, the same recipe answered a real id and the item stayed.
- Cause: a continue's landing is multi-seam, and `inWorld` is not its end. The restore's halves arrive after
  the click — the world-fact replay at the world-entry seam, the generation reconcile one frame after the
  generation-finished edge — and the local character restore is queued on the two-frame path, whose FIRST pass
  wipes the body. An act driven inside that window is wiped by the restore, and no id can be allocated while
  the world is still generating (`ItemIdAllocator.EnsureId` returns 0 there by design).
- Change: before driving anything against a continued world, wait for the restore's own completion line
  (`[GenItems] restored cut: N of N restored item(s) landed …`, or `CUO restore of world …: the live world took
  every restored fact`) instead of trusting `inWorld`; the first driven act is then the probe's own, not the
  restore's casualty.

## 2026-10-11 — Batch `20261011-i`: two window captures seconds apart are not an A/B

- Symptom: a visual row was planned as an A/B — capture a frame, change one thing (a real item moved onto the
  spot; then the standing objects' own renderers and lights forced on), capture again, read the difference. Both
  attempts failed, and the numbers say why: against a **control** of two captures taken back to back with NO
  state change in between differing over 284,218 of 637,332 pixels, the item control differs over 279,603 (below
  the floor) and the forced-on control over 297,052 — 4.5% above it, and spread over the whole frame rather than
  at the spot, i.e. the world's own change instead of the objects'.
- Cause: the scene is live. This run's own probes read 26 particle renderers, `Rain`, `Fog` and a moving player
  at the point under test, the host was freezing in an ice biome (a per-frame state the frame carries), and the
  world area's mean brightness moved from 4.2 to 125.8 across the session — so the frame-to-frame difference
  floor is larger than a small object's contribution, and no one-switch change can be isolated by subtraction.
- Change: budget a visual row as ONE frame read against a machine read, never as a two-frame comparison. Measure
  the noise floor first when a comparison still looks like the only way (two captures, no change between them),
  and write the number into the record; `.acceptance/20261011-i/n1-noise-a.png` / `n2-noise-b.png` are that
  measurement for this machine.

## 2026-10-11 — Batch `20261011-i`: the terrain's sorting order decides whether a frame can carry a visual row at all

- Symptom: row 5 of `mod-cross-player-solid-food-semantics` ("nothing visible at the parked spot") had been left
  `unproven` because its only frame was taken with the player unconscious. This run fixed the player's state (the
  vitals read at the capture said `consciousness 96.2`, `brainHealth 100`) and captured the spot with the camera
  on it — and the row still could not be carried by a frame.
- Cause: the spot is inside solid rock (the columns read block 14 solid from y=30 down) and the terrain tilemap
  that covers it draws at sorting order **1000**, while an unheld item's sprite carries order **0** — so an item
  standing there is behind the rock whatever its own renderer says, and a frame cannot distinguish "the switches
  took" from "the rock covers it". The run's own control (a real item moved onto the spot, renderer and light
  enabled) was invisible for the same reason.
- Change: a "nothing visible" row is planned against the draw stack, not against a camera position. The new
  committed recipe `tools/acceptance/recipes/draw-at.cs` answers it in one eval — every enabled renderer whose
  bounds contain a world point, with its sorting layer and order — and a spot whose covering tilemap outranks the
  object's own order is judged on the machine read, with the ticket re-worded to say so (the ticket carries the
  measurement, and the frame half returns if a future design parks the objects where a camera can see them).

## 2026-10-11 — Batch `20261011-i`: a mod-API ticket waits for a mod that exercises what it added

- Symptom: two tickets in `review/` whose end-to-end rows need a live session (`mod-authored-effects`,
  `mod-item-authored-actions`) could not be batched although both clients were up and the API surface was
  deployed: the sample mod in this tree declares no item content beyond the dressing, and the dressing declares
  neither `Usable` nor `UsableOnLimb` — so placing the built DLL would have exercised the registration rails and
  nothing else.
- Cause: the dependency table has no row for "a mod that calls the API under acceptance". Deploying a DLL that
  registers nothing is not a capability; the tickets' own Limits say the end-to-end half needs a mod that
  registers an action, and nobody builds one inside a batch (a batch changes no product code).
- Change: recorded as this batch's capability finding. A batch that judges a mod-API ticket names the mod it
  loads and what that mod registers BEFORE the session; when no existing sample registers it, the row stays
  `unproven` and the gap is a fixture to build in a development cycle, not something to approximate with a
  fixture that registers nothing.

## 2026-10-11 — Batch `20261011-i`: a probe helper invoked through `powershell -File` binds no array arguments

- Symptom: a probe template with two placeholders failed three different ways in a row — `-Arg` "specified more
  than once", then the second value binding to `-TimeoutMs` (`cannot convert "y=4" to Int32`), then a
  comma-joined value substituting only the first placeholder.
- Cause: `powershell -File script.ps1 -Arg a b` cannot bind a `[string[]]` parameter — the nested interpreter
  takes `a` for `-Arg` and `b` positionally — and `-Arg a,b` is parsed as the array only when the comma survives
  the outer parse, which it does not when the outer call already splatted its own array.
- Change: call the local helper script IN-PROCESS (`& .acceptance/<batch>/probe.ps1 -Url … -Arg $array`), where
  array binding is ordinary PowerShell; keep `powershell -File` for scripts whose contract is "ASCII, no
  dependent modules" (`preflight.ps1`, `session-environment.ps1`), which is what those files are written for.

## 2026-10-11 — Batch `20261011-j`: the fixture a row reads must be materializable, and placing it is not the deploy's job

- Symptom: a row that reads a live instance of mod content (its hover text) had nothing to read. The in-repo
  example mod's only item declared no `TemplateId`, so the adapter could not build a runtime object for it at
  all — and `deploy.ps1` does not deploy mods, so the plugins tree carried the framework and the pinyin mod but
  not the example.
- Cause: two halves of one fixture gap. A definition that is static item info only is invisible to every
  instance-reading probe, and a mod DLL is placed by hand, directly in `BepInEx\plugins\` (where the in-repo
  pinyin mod sits), never by the plugin deployment.
- Change: give the declaration a vanilla prefab base in a DEVELOPMENT commit before the run — the batch then
  runs against that commit and the DLL is an artifact under test, not a code change inside the batch — place the
  built DLL in the plugins tree for the session, and remove it at the close. Ask "can this declaration produce
  an instance" BEFORE writing a row that reads one.

## 2026-10-11 — Batch `20261011-j`: a row about a DEFAULT must be read below the old default

- Symptom: the first reading of the mod item's recognition gate proved nothing. The host body's own INT was 7,
  which satisfies the data class's inherited gate (2) as well, so the fixed and the unfixed code answer the same
  there, and only a change to the field itself was visible.
- Cause: a change that alters a default is only observable in the states the OLD default rejects. Reading it in
  a state both defaults accept is a reading about the field, not about the change.
- Change: force the reading body below the inherited gate (`body-force intelligence=0`, read back in the same
  step as the gate), and carry a POSITIVE CONTROL in that same state — a vanilla item whose own gate is 1 — so
  the run shows the game's branch flipping and printing the requirement, instead of asserting that it would.

## 2026-10-11 — Batch `20261011-j`: HotRepl logs one error-level line per client at startup, and it is not the product's

- Symptom: both clients' logs carry exactly one error-level line — a `TypeLoadException` for
  `System.ComponentModel.DataAnnotations.RequiredAttribute` — which turns a naively worded "zero `[ERR]`" row red
  on a run whose product paths are clean.
- Cause: `HotRepl.Control.Schema.SchemaCache` builds a JSON schema for a probed type, and NJsonSchema resolves
  `RequiredAttribute` through Newtonsoft on Mono, where that .NET Standard facade has no type forward. It fires
  before the world loads and names no CUO path.
- Change: attribute an error-level line from its own stack before calling it a product finding, and treat one
  such line per client as this channel's known baseline. Word such a row about the mechanism under test ("no
  item-provider refusal, and the provider's own lines are all Info") rather than about every error in the file.

## 2026-10-11 — The game derives a field once, for its own table; an injected definition owns it after that

- Symptom: batch `20261011-k`'s fixture item — `example.placebo`, the example mod's "declared usable, no
  action registered" shape — threw `ArgumentNullException: Value cannot be null. Parameter name: source`
  from `ItemInfo.HasTag`, once per item on BOTH clients at creation and once per frame while it sat in the
  hand slot (2,174 `HasTag` frames in the session-1 census). The item still existed and was still usable, so
  nothing in the row the run was judging said the fixture was broken.
- Cause: the game's own item-table pass ends by writing three derived members per entry (`fullName`,
  `description`, and `SetTags()` → the private `actualTags` array). CUO injects its definitions AFTER that
  pass, so it never runs for them, and `ModItemInfoFactory` populates `actualTags` only when the definition
  declares a non-blank `Tags`. `Tags` defaults to `""`, so the array keeps its CLR default and the game's own
  `HasTag` (an `Enumerable.Contains` over it) throws wherever it is asked.
- Change: filed as `docs/backlog/done/mod-item-with-no-declared-tags-throws.md` with this run's numbers (a
  batch validates and does not change code). The rule a fix must carry: a derived member the game's own table
  pass writes is the injected definition's to write, and a collection must exist even when it is empty.

## 2026-10-11 — One throwing frame can brick the process: the call-context depth guard latches

- Symptom: the same batch's first session went from one exception to a 136,445-line / 15,706,048-byte host
  log (re-derived 2026-10-11 from the rotated log and landed as the batch's
  `f3-host-session1-storm-census.txt`), 19,973 of those lines
  `InvalidOperationException: CallContext.Enter depth 16 exceeded — an Enter was never disposed (leaked
  scope)`, i.e. every scope-entering patched path in that client throwing on its own account over 92
  seconds. Nothing recovered it; that session could only be ended.
- Cause: the guard's own message names the mechanism — an `Enter` was never disposed. The window it happened
  in is the one where a game-side throw fired per frame, and the frame that entered the scope is NOT in the
  captured stacks: what fits the source is `Body.HandleVisuals`, which CUO wraps in a `CallContext` scope
  (`Patches/BurpSoundPatches.cs`) and which asks `HasTag("gun")` on the per-frame path — NOT the two frames
  this entry first named (`PlayerCamera.HandleGunMenu` has no CUO patch, and `Item.Start`'s patch is a
  postfix with no scope). A batch's own review caught the mis-naming, which is the lesson inside the lesson:
  a mechanism sentence must be built from the artifact, not from the frame the log happens to print first.
- Change: the run's own discipline — a fixture known to throw is driven LAST in its session, and a client in
  this state is restarted rather than fought (this batch's second session did exactly that and judged its
  remaining rows cleanly). The amplifier is the second half of the same ticket, so one fix decides whether
  scope disposal survives a throwing original — decided 2026-10-12 in batch `20261012-a`: the guard owns the
  release deadline (the frame boundary releases what a frame leaked, reported once per episode) and the frame
  that leaked was MEASURED there (`Body.HandleVisuals`, `CharacterBurp ×16` on the stuck stack), which is the
  reading this batch's artifacts could not give.

## 2026-10-11 — A parameterized recipe cannot put a placeholder inside a lambda

- Symptom: the guard rows needed a mod function that throws, which no fixture ships, and the first attempt to
  promote the probe as an ordinary recipe could not be written: the delegate body has to read the timed key,
  the duration and the threshold, and an evaluator lambda refuses a captured local.
- Cause: two limits meet — the evaluator's Mono REPL refuses a lambda that captures a non-const local, and a
  recipe's placeholder may appear exactly once, so `{{s:key}}` cannot be repeated inside the delegate that
  reads it back.
- Change: the promoted recipe (`tools/acceptance/recipes/plant-throwing-effect.cs`) declares `// args: none`
  and writes its keys and thresholds as LITERALS inside the lambdas, and its header says why a parameterized
  version of that shape cannot exist. A run that needs other values writes its own batch-local probe the
  same way.

## 2026-10-11 — Write probes as files on this channel, not as inline snippets

- Symptom: a brace-heavy probe sent through the local eval client's inline `--code` form answered
  `eval_error … Unexpected symbol '{'` while the same snippet as a file ran first time; an earlier snippet in
  the same batch answered `ok` with NO value at all — the silent void the evaluator returns when a
  submission's shape trips it.
- Cause: the inline form has to survive the shell, the argument quoting and the client's own parser at once,
  where the file form has none of that and also leaves the probe in the batch directory where a record can
  cite it.
- Change: every probe a record cites lives as its own file under the batch's artifact directory, and the
  reusable ones are promoted into `tools/acceptance/recipes/` before the batch closes (this round:
  `remote-medical-release.cs` — the cross-player release no committed recipe covered — and
  `plant-throwing-effect.cs`). `ok` without a value is a failed probe, never a passing one.

## 2026-10-11 — The remote half of the wound-view release had no committed drive at all

- Symptom: the cross-player limb-tool row of `mod-item-authored-actions` — "the treated player's own client
  runs the item's own limb action" — had no recipe to reach it. `remote-gesture mode=medical` refuses by
  design unless the selected limb is the acting client's OWN, and the medical view's remote half routes
  through a patch (`RemoteMedicalBlockSpecialActionPatch`) that only fires when the display body's view is
  open.
- Cause: the two halves of one native gesture were built as two entries and only the local one was committed;
  the remote entry needs the display focus open (`remote-wound-special mode=open`), the view pointed at the
  treated limb, and a local item dragged onto that limb's `WoundViewLimb` — which is exactly what
  `remote-gesture`'s own guard forbids.
- Change: `tools/acceptance/recipes/remote-medical-release.cs` (args `item`, `limb`) is the remote half, and
  the batch's record names it. A future cross-player topical/injection/limb-tool row starts from it instead of
  rediscovering that the local recipe refuses.

## 2026-10-12 — A driver answer with non-ASCII text does not survive a plain `Get-Content`

- Symptom: reading the final `state` answers with `Get-Content <json> -Raw | ConvertFrom-Json` threw
  `传入的对象无效，应为“:”或“}”` on the guest's answer while the host's parsed — the guest's carried the
  gate text `Starting…`, whose ellipsis had already been mangled on the way in.
- Cause: Windows PowerShell 5.1 reads a UTF-8 file with no BOM through the system code page. The earlier
  finding (2026-10-02, the delivery-checklist line count) was the same mechanism; this is the second shape it
  takes, and it fails INSIDE the parse, where the message names a byte offset and nothing about encoding.
- Change: every driver answer a run parses is read with `-Encoding UTF8`, and the batch's own helper does the
  same for its log windows. A `ConvertFrom-Json` error on a file the driver just wrote is an encoding
  symptom first, a protocol symptom second — re-read before suspecting the client.

## 2026-10-12 — An exception census must not count the `[Unity:Exception]` prefix as its own dimension

- Symptom: a batch window counted `ArgumentNullException` 267 and `[Unity:Exception]` 267 on the host, 384
  and 384 on the guest — two "independent" columns that were the same lines.
- Cause: Unity's own console listener writes the exception with the `[Unity:Exception]` marker as part of the
  line, so a pattern for the marker matches every line the class-name pattern matches. Counting both reads
  like corroboration and is one reading twice.
- Change: an exception census counts the mechanism (the exception type or the message) and, if it wants a
  second dimension, counts something disjoint — an unrelated family, or the frames the throw came from. When
  two columns come out equal, that is the tell.

## 2026-10-12 — The artifacts a record cites are written before the record, not after

- Symptom: a record's row table named four window files (`d7-…`, `e7-…`, `g10-…`, `g11-…`) that did not exist
  yet; they were generated afterwards, so the record was briefly citing evidence a reader could not open.
- Cause: the writing order followed the narration (run → record → tidy the evidence), and the standing rule
  ("list the batch directory and check every cited artifact exists", 2026-10-11) was applied as a final check
  instead of as the order of work.
- Change: the order is run → dump every window, count and probe answer the record will cite → then write the
  record, whose table is filled FROM the listing. A record is not finished until every file it names exists,
  which is cheap to verify and expensive to notice later.

## 2026-10-12 — Moving a ticket also moves the references in gitignored local archives

- Symptom: after both tickets moved to `done/`, `BacklogReferenceGateTests` failed on exactly ONE reference:
  the line in `docs/acceptance/AGENTS.local-archive.md` for the batch that filed the tagless-item defect. A
  sweep driven by `git grep` found nothing, because that file is gitignored — its name carries `.local.`.
- Cause: the gate scans the markdown files on DISK under `docs/`, not the tracked set, so the area's own
  local archives are inside its surface. A tracked-file search misses precisely those pages, and the failure
  then looks like "a gate that is red only on this machine".
- Change: a ticket move's reference sweep includes the local pages beside the area's `AGENTS.md`
  (`docs/acceptance/AGENTS.local.md`, `AGENTS.local-archive.md`, the `.agent-local/` notes), and the cheap
  way to get the complete set is to run the gate's own scan rather than a `git grep`. The sweep is worth
  doing rather than exempting: the page that failed here is the only place that batch's finding is recorded.

## 2026-10-12 — A moved ticket drags three gate families behind it, and an edited page PAIR drags a fourth

- Symptom: after 20 tickets moved from `review/` to `done/` and the index rows moved with them, the gate
  suite went red twice on the same change: 46 prose references across 19 gated documents did not resolve to
  a ticket, and `EveryPair_IsRecordedAtItsCurrentContents` reported both sides of
  `docs/en|zh/reference/modification-policy.md` as "edited without re-recording the pair".
- Cause: the folder IS the status, so every path that names a ticket is a pointer that has to travel with
  it — and this batch's own reference sweep necessarily edited BOTH sides of one registered page pair
  (re-pointing two ticket paths in the policy page), while the pair registry records each page's content
  blob hash. Re-pointing a path inside a registered pair therefore invalidates the record by construction,
  not by mistake.
- Change: the move pass re-points every reference the gate names in one sweep (`review/<slug>.md` →
  `done/<slug>.md`: 74 references across 19 files, the evidence matrix and `docs/decisions/active.md` the
  heaviest) and re-records the edited pair's two blob hashes in `docs/standard/alignment.txt` in the same
  change. `git grep` cannot see the gitignored local pages, so those are swept by name — the previous
  entry's rule, unchanged.

## 2026-10-12 — An offline batch decides a ticket only from the rows its own acceptance section carries

- Symptom: `review/` held tickets whose cycle verification list was a status checklist (build 0 warnings,
  N tests, deployed hashes) and could be decided with no client at all, next to tickets whose list carried
  one live observation and could not — and a ticket naming a surface a PERSON verifies on the machine (the
  Online UI's parity row, a host/guest frame-rate comparison) had to leave that surface open while its own
  rows passed.
- Cause: the ROW is the unit of acceptance. A ticket's prose limits are not rows, and a historical count
  ("2417 tests passed") is a state of the cycle that wrote the ticket, which no later run can re-measure —
  only its durable outcome can be checked.
- Change: the batch writes the row table itself for a ticket that has none, judges every row against this
  run's own per-case outcomes (the TRX outcome index) or against a file it read, judges a historical-count
  row by its durable outcome (the suite, gate, case or self-check record the row names still exists and
  passes in this run, with the historical number named as un-remeasured in `Limits`), and reports a
  person's surface as a residual for the user rather than as a row the run pretends to judge.

## 2026-10-12 — The anchor check is a tool, and the tool lies in two familiar ways

- Symptom: of 148 evidence anchors a drafting pass proposed, four test names looked absent from the run's
  outcome index and three file quotes looked absent from their files.
- Cause: two checking artefacts, neither a record defect. An xunit theory's display name carries its
  arguments (`...RejectsMalformedInput(text: "")`), so an exact-name lookup misses the family; and
  PowerShell's `-like` treats `[` and `*` as pattern syntax, so a quote beginning with `[AdaptiveSync]`
  can never match a pattern-based containment test.
- Change: the anchor check resolves a test anchor by name prefix against the outcome index and checks a
  quote with literal containment (`Contains`, or `-SimpleMatch` in `Select-String`) — the same trap
  `AGENTS.local.md` records for `-like` on a pattern containing `[`. A "missing" anchor is re-derived with
  the literal tool before a row is called unsupported.

## 2026-10-12 — A row a run can produce is not a residual, and a ticket's "verified by the user" is not a licence to defer it

- Symptom: the offline batch's scope page and two records put three rows on the user's residual list — the
  Online UI's native-binding-parity row, a hand-edited config value, and the host-vs-guest frame-rate
  comparison — while the same run's own preflight printed
  `RESULT: OK - a full two-client run is possible` and the machine sat free. The user's answer was one line:
  "you said you can edit the config and read the logs, so how is it unreadable?" They were right.
- Cause: the tickets' own `Limits` say those surfaces are "verified by the user on the physical machine"
  (the Plugin layer has no test host), and that sentence was read as a property of the ROW. It is not: it
  describes what the WRITING cycle could not do, not what an acceptance run cannot do. A residual is a
  judgement no run can produce — a taste, a feel, a call only the owner can make; every other row belongs to
  the run, and a run that can start clients must start them.
- Change: both tickets returned to `review/` with their index rows and their references (an unjudged row
  keeps a ticket open, and `done/` claimed otherwise), their records name the open row and say why the
  residual label was wrong, and `docs/acceptance/workflow.md` §6 now carries the rule: a row that needs a
  client is a session row, never a residual.
- Also: a reversal must reverse every sweep the batch ran. Re-running the one-direction `review/` → `done/`
  reference sweep after the re-open silently put the two index rows and one decision entry back on `done/`;
  the gate suite caught all four failures at once and the reverse pass was the fix. A sweep script carries a
  DIRECTION, not just a rule — never re-run it after the world has moved the other way.






