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
