# Acceptance lessons

What the runs keep teaching, as a ledger: one line per pitfall — the rule the next batch acts on, and the
page, tool, recipe or record that carries it. Read it when planning a batch ([workflow.md](workflow.md) §3)
and when a scenario will not behave as the plan says.

A lesson whose rule now lives in a page, a tool, a recipe or a gate is DELETED here rather than repeated:
the conclusion belongs where its reader already opens it, and this page holds only what has no other home.
Machine values belong in `AGENTS.local.md`, a rule that binds the repository in `docs/AGENTS.md` or
`AGENTS.md`, and a run's own record in `docs/evidence/acceptance/`.

## How to add an entry

One entry per lesson, newest last:

```text
## <date> — <the pitfall, as a headline>

<the rule the next run follows, in one or two sentences> — <the page, tool or record that carries it>
```

Add an entry only when the next batch would act differently for reading it AND nothing else carries the
rule: if a page, a tool, a recipe or a gate can hold it, put it there and leave no entry here. Name the
carrier with a backticked path so the reader can open it, and cite the run's own artifact when the evidence
is what the reader needs. A narrative entry (a Symptom/Cause story) is refused by
`AcceptanceLessonsGateTests`, which holds every entry to one line.

## 2026-09-27 — A title-screen click is not "start the game"

Session actions go through the Online UI's own registered controls; before an in-process click on a scene object becomes a setup step, the handler's target is identified from evidence, because every title-screen object carries its own action — `tools/acceptance/drive-in-process.ps1`, `workflow.md` §5.

## 2026-09-27 — A row that names a past event is judged by its durable outcomes

A row naming an event no later run can re-create (an adversarial review, a one-off measurement) is judged by the durable outcomes it produced — its findings pinned by regression cases that pass in this run — and the record's Limits says the event itself was not re-run — `workflow.md` §6, §7.

## 2026-09-27 — A header bullet can span lines; an insertion must not split it

A header bullet added to a ticket goes after the LAST continuation line of the bullet above it, and the insertion point is verified on every ticket a script touches; rebuilding the ticket from its previous revision plus the intended lines is the cheap way to make that exact — the header shape is `docs/backlog/README.md`.

## 2026-09-27 — A driver vocabulary is not a scenario library

Plan a session ticket into a batch only when its setup path exists as a committed or recorded recipe; otherwise record the setup gap, leave the ticket in `review/`, and say so in the scope — `tools/acceptance/recipes/`, workflow §5.

## 2026-09-27 — A recipe smoke belongs inside the session it verifies

Smoke the recipe set in the session it will run in, as steps of the batch, rather than planning a separate "recipe cycle" first; the probe JSON is the layer's proof — `tools/acceptance/recipes/`, `docs/evidence/selfchecks/tooling/acceptance-recipe-layer-selfcheck.md`.

## 2026-09-28 — `move-drive` walk is frame-window-dependent; slide is the displacement source

A movement window uses `slide` and confirms travel from the positions before and after; a walk-only window that shows no travel is `unproven`, not a pass — the `move-drive` recipe.

## 2026-09-28 — World frames do not resolve poses at the game's own zoom; the native panels do

Judge a visual row from the largest readable surface the scenario has (the native panel over the world frame), and record a row the frames cannot resolve as `unproven` or a residual — never as a pass — workflow §6.

## 2026-10-01 — A local window family is a one-client batch

A window or panel family is planned as a one-client run; peer-dependent rows get their own batch with the setup they need, and stay `unproven` meanwhile — workflow §1, §3.

## 2026-10-01 — A forced state must be written where the game derives it

A forced-state probe writes the INPUT the game derives from (the limbs' pain, not `Body.averagePain`, which is recomputed every frame), and reads the state back in the same step with the body still — the `body-force` and `pain-force` recipes.

## 2026-10-01 — A relative cell read names a different cell on every client

A cross-client cell comparison re-derives `dx/dy` from each client's own body and asserts the recipe's returned `cellX`/`cellY` against the intended absolute cell before comparing values, retrying while the body is still falling; a whole-row comparison uses one absolute-coordinate probe — `block-read`, beside the batch's own `read-abs-cell.ps1`.

## 2026-10-01 — The sound family's evidence is a Debug line, so the run sets the level live

Receiver-side evidence (the sound replay lines, the relayed-break presentation lines, the repaired answer line) is written at Debug: raise the level through the config system's own editor, read the CUO rolling log, and restore `Information` before closing — `LoggingConfigEditor`, the `loglevel-*.cs` snippets.

## 2026-10-01 — An entity census must match the type, not the name

A census counts by component type (`FindObjectsOfType<BearTrap>(true)`) and drives its target through that component; the game's own `locate` searches names, and the live objects are named `beartrap(Clone)`.

## 2026-10-01 — "written" in the log is not proof the live field kept the value

A native-field row reads the live field after the write and pairs it with the write's own line (`cut value → the post-generation write line → a live read that starts at the cut value`); a handover line proves the write ran, not that the game kept it — workflow §6.

## 2026-10-01 — A row that says "no file on disk" has to look at the disk

A "file must be absent" row starts with a disk listing; a stale file it finds is setup the run owns — compare its mtime against the retirement date, move it aside as a declared setup step, and confirm nothing recreates it — `workflow.md` §3, §7.

## 2026-10-01 — A deny-delete ACL does not stop the game's prune; the read-only attribute does

A destructive-path injection must be the one the PRODUCT's delete respects: a deny-delete ACL blocks PowerShell but not the client's retention pass, while the read-only attribute makes it report `could not be pruned` — set `IsReadOnly`, read the product's own account line, then clear it to prove recovery.

## 2026-10-01 — A peer that stops reading does not back-pressure a healthy Steam link

A sustained refusal episode needs a peer blocked at the TRANSPORT level while its session stays connected: the application's own drain stopping is not a substitute (the host pushed >20 MB with zero refusals), suspension collapses the session to `Connecting` within seconds, and the usable switch is the service's own `IsInitialized` pump pair — `docs/evidence/acceptance/steam-transport-send-limit-runaway-20261001-s.md`.

## 2026-10-01 — The adaptive stream budgets cap the load a run can generate

Before designing a load scenario, read the profile budgets of the streams it will drive and state which stream is expected to carry the rate: an adapted stream is capped by its `MaxBytesPerSecond`, so spawning more of one kind raises per-frame bytes and the policy answers by lowering the cadence.

## 2026-10-01 — An offline row that names a measurement is re-derived, not waived

"Offline" means the repository can produce the evidence, not that a named test already produces it: a measurement or a pre-change state is reconstructed by the run (`git show <revision>:<path>`, a run-local probe), and a ticket whose row needs something the run does not produce stays in `review/`.

## 2026-10-01 — A measured table is re-derived at its own revision; a figure that does not match is named

A re-derivation names its revision and its measurement form (bytes vs characters, LF vs CRLF), and a recorded figure that does not reproduce is recorded as a discrepancy, never smoothed over — `docs/evidence/acceptance/evidence-matrix-fat-rows-split-20261001.md`.

## 2026-10-01 — A fact check names the owner it found, not just pass/fail

A mechanical fact check says where each unmatched token actually lives (the umbrella, the sibling the row cites) instead of reporting a loss at one hop — `docs/evidence/acceptance/backlog-index-summary-duplication-20261001.md`.

## 2026-10-01 — A HUD widget can be live, readable and still absent from the window capture

A row whose subject the window capture cannot composite (a HUD indicator the widget's own rects place but `PrintWindow` never draws) is judged from the widget's live objects and classified `machine`, with the record saying why the frame cannot carry it — and, the other way round, a visual change is judged on a frame BEFORE the geometry is trusted, which can read correctly while the frame shows a white box.

## 2026-10-01 — A WorldJoin starts every handshaken member, so a parked lobby client cannot hold the gate

A member parked in the lobby does not hold the start gate — the host's click sends `WorldJoin` to every handshaken member and each guest starts its own run: hold the gate with `leave-world` on a member plus the production arm (`tools/acceptance/recipes/gate-arm.cs` → `IWorldControl.StartStartGate`), and cite the arm line plus the forced-release line as the hold's bounds.

## 2026-10-01 — A recipe whose expression ends at the cast returns the delegate

Every recipe ends with the trailing invocation (`}))()`); a missing `()` still parses offline and the driver then reports a protocol-level failure that names nothing, so a new recipe is smoked through the live evaluator once before the run, and a `driver-error` on an empty value sends the reader to the raw frame first.

## 2026-10-01 — A direct-write row needs its site reachability, not just its name

Before designing a scenario for a write site, run the caller census and the shipped-data string scan; an unreachable site is exercised through a declared forced recipe of the game's own code (`tools/acceptance/recipes/scene-reload.cs`), and the record carries the reachability half beside the live one.

## 2026-10-01 — Two "parallel" tool calls are not a same-instant race

A race scenario opens BOTH evaluator sockets first and sends both evals in ONE event-loop tick; two shell calls differ by hundreds of milliseconds, which is longer than the relay's travel time, so the second sender acts on an already-changed world — the batch's own race helper beside `tools/acceptance/drive-in-process.ps1`.

## 2026-10-01 — A substituted numeric literal is a double, and the REPL only says `InteractiveHost`

A numeric template casts explicitly (`(float)(__TX__)`) and a numeric recipe argument is an integer or is formatted by the recipe itself: `7.5` substituted into a `float` parameter fails to compile in the client while the offline gate's own `0` substitution passes it, and the failure is reported as `(1,1): InteractiveHost` — suspect the substituted literals' types before the logic, keep one eval in flight per client, and smoke a numeric recipe with a FRACTIONAL argument, because an integer-only run never sees this shape.

## 2026-10-01 — A same-eval read after a break is too early for the drops

A write and its read do not share one eval: poll with bounded retries (the drop appears tens of milliseconds later, inside `OnBlockDamaged` → `ItemInstantiated` → `FlushPendingBlockBreak`) and return the first snapshot that carries it.

## 2026-10-01 — A harvest written while the review runs breaks the freeze the review depends on

While an independent review runs, the only writes are gitignored artifacts; the harvest, the run log and every tracked edit wait for the report, because the reviewer's contract is the WHOLE tree at the moment it started — `docs/en/contributing/review-and-delivery.md`.

## 2026-10-01 — `Settings.Get<T>` works inside the client evaluator, and a client that just changed layer answers `no-world`

Arm a game setting through the game's own accessor rather than writing its static, and re-read before recording a verdict: a client that just changed layer answers `no-world`, and its own census answers `tableCount: 0` three seconds later.

## 2026-10-01 — A HotRepl eval runs on the main thread, so it freezes the game's own frame loop

An eval blocks the frame loop while it executes, so nothing queued from inside one is pumped by a frame that snippet sees (a 400 ms poll read `framesAdvanced=0`, `timeAdvanced=0`) and a `Thread.Sleep` inside an eval stops nothing: an injection that must stop the game's frame goes through the code that frame runs (a Harmony prefix, or a value the frame reads such as `SteamService.IsInitialized`), and a native `Input.GetKeyDown` gate needs a frame-timed pair of submissions (`.acceptance/20261011-e/two-evals.mjs`). This supersedes the earlier reading that an eval runs on a worker thread.

## 2026-10-01 — The one-shot `[WorldFingerprint]` log is re-armed on session end only

A post-mutation fingerprint pair needs a session restart (quit, relaunch, re-join): neither a world re-entry nor a Continue re-arms it, so a row that needs the pair on those paths is `unproven`.

## 2026-10-01 — The fresh-drop flag is observable only inside the game's own gate and window

A `fresh` drop row arms the game's `itemfloating` setting, places the destroying body beside the entity BEFORE the support is broken and inside the 8-unit radius `BuildingEntity` applies, then reads within the component's own 10 s life (full alpha for the first ~7.5 s, the fall starting under 2 s); its machine half is the breaker's `ItemBuildingDeathDrop` + `FlushPendingBlockBreak` pair beside each peer's `[ItemSpawn] materializing` line with the same id, position and velocity, read in both directions.

## 2026-10-02 — An end-of-line simulation is proven by bytes, not by `git status`

A checkout simulation records the conversion as bytes — a byte-exact backup, a pure-CRLF check and `git hash-object --path=<path> <file>` equal to the committed blob, restored and hash-compared afterwards: with `core.autocrlf=true`, `git status` reports a text file modified while the working copy's clean form equals the blob.

## 2026-10-02 — A late-joiner plan on the leave→continue path must move the pointer world first

`continue-run` restores the world `lastOpenedWorldId` names, not the session's, and a layer-end cut replaces the layer by design: a late-joiner plan either moves the pointer to the session's world first (`worlds.select.<worldId>`) or uses the member re-entry route below, and a restore that changes the world is not a neutral environment for a row — `docs/evidence/acceptance/20261002-c-scope.md`. A world divergence is attributed by reading the local repository's own `index.json`, `world.json` and `run.json` and matching them against the baselines the logs applied — `docs/evidence/acceptance/enemy-snapshot-binding-recovery-20261002-l.md` carries that verification with the pointer fixed.

## 2026-10-02 — A body placed onto a jump pad consumes the pad

Choose a destruction site from SCANNED standing spots and re-read the destroying body's distance to the pad right before the break: `body-place` teleports to a world position, and a position inside the pad's own cell lands the body on the pad and consumes it.

## 2026-10-02 — A pre-staged probe had never been compiled and the eval error hid why

When an eval error names nothing (`(1,1): Interactive Host`), bisect the snippet and send its expressions one at a time instead of guessing; a probe that creates an entity casts to `GameObject` before `GetComponent`.

## 2026-10-02 — A log read can be taken before the writer flushed, and a stale build can fake a mutation red

An empty log read is re-taken after a bounded wait and never treated as "the line is absent" on its own; after reverting a deliberate mutation, rebuild the owning project and confirm the artifact changed (hash or IL) before believing a still-red test — `workflow.md` §6, `docs/en/contributing/build-and-test.md`.

## 2026-10-02 — Never leave the host out of the world while a member is still generating

Wait for `[GenStream] done` on EVERY client (the host's and each member's) before the next host-side world transition (leave, Continue, layer switch): a member waiting for the host yields without consuming randomness, and the last yield becomes the segment start the modifier replay decides from.

## 2026-10-02 — Cross-client world identity: compare the reset hex and the segment fingerprints

Compare the host's and each member's `Generation stream reset to captured baseline` hex as the adoptable identity, and use the Debug `[GenStream] segment N` lines to separate a baseline problem from a tail-accounting one; take the post-repair read before judging an agreement row.

## 2026-10-02 — `quake-force` is a write, so its reading call starts a second quake

Read the quake state once, after the window, and treat a second start as the probe's own side effect; a quake's own breaks are the air-write source such a row needs, the host's `[Earthquake] host quake started` line beside each side's `SetBlock(0)` is what such a row reads, and a block placed onto air near the surface is not durable evidence.

## 2026-10-02 — The partial-damage counter must be read while the crack still exists (corrects the earlier entry)

Read the pending counters BEFORE the follow-up break (a block write clears the cell's damage row), then break in a second window and read again: a pending counter measures an OUTSTANDING entry, so the read point is named relative to the mutation that destroys it, and the counter is `block-report-pending-count`, whose first "instrument gap" reading was a read-order error.

## 2026-10-02 — An in-place member re-entry needs `leave-world` → `home.leave` → `join-lobby`, and the verdict read comes after the snapshot

A member that left only the world stays a lobby member and `join-lobby` is a no-op; the re-entry is `leave-world` → `home.leave` (window open, Home page selected, state `active=false`, lobby 0) → `join-lobby` the same id, and the verdict is read after the host's per-member `Sending world-entry snapshot group` and the member's own applied line — never on the join call's own answer.

## 2026-10-02 — A mutation pass that nets to zero proves nothing

After a mutation pass, require the re-captured fingerprint to differ from the entry pair in the affected band and the total, and leave at least one cell in a new state; convergence reads prove the peers agree, not that the world moved — the `[WorldFingerprint]` pair.

## 2026-10-02 — Log lines that do not mean what they read like

Read a repeated creation line as a rebind, not a duplicate (`EntitySpawn` logs unconditionally after its find-or-create), read the snapshot's own `N runtime spawns` count rather than a first-bind label, and give `container-read` its `guest=` argument even in `mode=local`.

## 2026-10-02 — The inbound blackout is the executable swallow, and it has an order

Stage a lost inbound report with `net-receive-blackout`: read `mode=status` and the member's pending counts before arming, do only the marker writes while armed, then `mode=off` and re-read immediately — a blackout left armed costs every domain's traffic and the member ends its own session after 15 s without a host frame, so keep the window single-stepped and well under that.

## 2026-10-02 — An exception burst is read from the rolling log first, and a capped extract is named as capped

Read the DEDUPED diagnostic before the stack (one line per object names the object, its id, the reason and the scene), then the rolling log's own frame (a re-entry burst writes one), and the full `LogOutput.log` tail by BYTES when the burst is message-only — never scan a hundreds-of-MB log whole, and name the complete extract or its cap whenever an artifact is cited as a window's evidence.

## 2026-10-02 — The solo→lobby handover is counted on the MEMBER's entry, not the host's

The exactly-once denominator for an entry-snapshot row is the per-member `Sending world-entry snapshot group to <steamid>` count, not the host's own re-entry: a solo host enters the world with `role=None`, `create-lobby` works from inside it, and a member waits at `Starting…` until its own entry edge fires.

## 2026-10-02 — The shipped game cannot produce a health-1 block, so the crush family stages one

`crush-find` answering zero is a world-state answer, not a probe failure: only `thinice` and `powdersnow` carry `BlockInfo.health` 1 and they sit behind depths the shipped layer count never reaches, so a crush run declares the substitution — the game's own `SetBlock` write of `thinice` under the feet, the path CUO relays — and pairs its census probe with a `nonAir` plus health-row validation half so a blind probe is told apart from an empty world.

## 2026-10-03 — A moved ticket's references need a manual sweep; the reference gate exempts the records

A ticket move sweeps the moved slug across `docs/` BY HAND: `BacklogReferenceGateTests` deliberately skips the point-in-time record folders (`backlog/done/`, `backlog/resolved/`, `evidence/selfchecks/`), which is exactly where nothing will remind anyone. Update the pointer-shaped references (`Related:`, `Source:`, "closed by", `Superseded by`) and leave the point-in-time positions (an "Owner cycle" line, a dated "moved" note) as written — one regex over `docs/**/*.md` with a target-existence check. The sweep includes the area's gitignored local pages beside `AGENTS.md` (`AGENTS.local.md`, `AGENTS.local-archive.md`), which `git grep` cannot see, so it is driven by the gate's own scan; and it re-records the blob hashes of any registered page PAIR it edited in `docs/standard/alignment.txt`, because re-pointing a path inside a pair invalidates the record by construction.

## 2026-10-03 — Two claimers for one item without a timing race: the inbound blackout

The driver's inter-command gap exceeds the relay latency, so a simultaneous two-sender pickup is staged with the blackout instead: both members read the item's id, the second member's inbound is parked, the first picks it up, then the parked member picks its stale copy and reports — the host answers `Conflict (item … is already carried)`, and behind the blackout the loser's own drops and their ids survive (`item-tombstone-read` shows `remembered:true, reason:"BlockAlreadyBroken"`).

## 2026-10-03 — The refusal tombstone is cleared by the SESSION end, not by leaving the world

Item tombstones are session-partitioned, so `leave-world` + `continue-run` keeps the refused id and a stale member must leave its old lobby (`home.leave`, `lobby:0, role:None`) before `join-lobby` accepts a new one; a member joining an already-running session enters through the entry group.

## 2026-10-03 — Batch `20261003-e`: read a blueprint unlock from the recipe table, and give a blackout the report's own latency

A blueprint's unlock is read by diffing `Recipes.recipes[i].INT`/`visible` before and after the use (`craft-drive mode=list` lists only recipes whose materials are at hand); a late joiner's trader stock reads 0 until the first fallback snapshot (5.0 s base, ~8.6–10.0 s under load), which is the pre-fallback state and not a defect; `trade-drive mode=give` is capped by the trader's lifetime credit (60 in that world) and returns `credited=false` past it; the block-break report leaves ~150–250 ms after the recipe call returns, so a blackout aimed at it spans about 2 s (0.1 s misses the swallow).

## 2026-10-03 — Crafting and trade need a fixture, and the host's stock overwrite moves the buy index

Crafting: recipe 0 is `2 × foliage → rope` (foliage matched by quality), two `item-provide mode=create type=foliage` calls make it craftable and the host's own `[Crafting]` line plus a `container-read mode=host` prove the registration half. Trade: the nearest trader starts at `valueGiven 0`, so a purchase needs `trade-drive mode=give` credit first, the give's host broadcast overwrites the guest's local stock, and the bought type is read from the buy result — never from a list taken before the give.

## 2026-10-03 — The rejection forks are reachable through the product's public seams

Both rejection shapes are staged without a forged wire frame: `WorldService.FireRuntimeEntityRejectedReceived(hostSteamId, msg)` for "a non-reporter receives the rejection", and `SendEntitySpawned` with a hand-built `EntitySpawnedMsg` whose creation token names another member for a foreign-token report; `ISessionControl` (role, host id) resolves from `CuoBootstrap.Services`.

## 2026-10-03 — The entity fallback's window is shared, and one window can serve three rows

The guest's steady fallback re-sends every pending entity report in ONE window (5 s inside the entry phase, 60 s outside), so several staged rejections are read in a single pass; the window is armed by the first outstanding entry and a new scenario interleaved into an open window rides the same re-send — each key's outcome is read from the guest's own `re-reported N unacknowledged creation(s) to the host` line.

## 2026-10-03 — A re-entering member's entry group arrives after `inWorld` flips

Read a re-entry's tables after the burst, not on the `inWorld` edge: the member's `state.inWorld` can read true while the big snapshot frames are still arriving (a probe 2 s after the edge saw 0 trap frames, ~12 s later the stamped `TrapLayoutSnapshot`), and the member's own `[TrapLayout] materialized` lines are the cross-check.

## 2026-10-03 — A recipe with two declared arguments is refused before the client

Pass every argument a recipe declares (a command that takes none uses `args=none`): the driver refuses the call and exits before its socket opens, so a missing probe answer file is the failure signal — re-run the step with the driver's stderr visible before concluding anything about the product.

## 2026-10-03 — A live descent chain leaves a layer-modifier divergence on the members

A verdict about a stamped or reported table is read from the gate's own comparison and the actors' own tables, never from a cross-peer census taken while a `[LayerMod] baseline divergence` is live.

## 2026-10-05 — Batch `20261005-d`: read a zero-warning row as a pair of channels

`[CarriedSync]` (the event applied) and `[CharSync] divergence` (the monitor's verdict) share neither a logger nor a prefix, so a filter on one turns "the event arrived" into "no event at all", and the contrast that locates a warning is a control gesture of the same kind. A remote gesture is judged on the OPERATOR's own client, so stage line of sight before driving one (`body-place` takes WORLD coordinates), and read `item-world-read` in BLOCK cells (`body-read`/`body-place` speak world coordinates: world (0, 445) is cell (510, 941)).

## 2026-10-05 — Batch `20261005-c`: the release gesture's three inputs, and what a passing row can still tell you

`PlayerCamera.HandleReleaseDragging` is driven in process through `tools/acceptance/recipes/remote-gesture.cs` with its three inputs staged (`camera.dragItem`, `camera.clickPos`, the `uiCasts` list, which is a target pointer rather than a raycast result); `clickPos` decides drop vs place (`Distance(mousePosition, clickPos) > 10` is the world path, `< 10` the container path); when the run may not move the pointer, move the ring instead (`radialMenu.position` at `200 * uiScale`), and record that staging as scene setup. Not every rendered proxy is drivable (`RemoteDragProxyQuery.InstanceId` reads the `RemoteInventoryItemId` marker, else the release patch fails closed), the three sandbox windows are indistinguishable by title so a window capture of the Online UI's Home page settles the PID↔endpoint mapping, and a run reads its own diagnostics before closing a batch — a passing row can still carry the batch's most valuable finding.

## 2026-10-05 — Batch `20261005-b`: one command for a two-blackout window, and seek a log mark by BYTES

A staged window that parks both directions is written as ONE command (the 5–7 s gap between separate agent commands is what blows the 15 s `GuestHostSilenceWatchdog`): arm host, arm guest, skip layer, +2 s, destroy the carried item, disarm guest, disarm host — 12 s end to end. The host's in-session repair cycle sends a kernel checkpoint every 60 s, an empty host world-item table is the layer edge (which also unloads the member's body), layer-changing staging is scheduled LAST, `item-pickup mode=id` still obeys the game's proximity guard, a committed-batch receipt needs ~6 s of parked inbound, and a byte-mark log reader seeks BYTES over `latest.log` when the evidence needs timestamps — slicing the split text by the mark silently matches nothing.

## 2026-10-06 — A held key IS reachable in process, and the OS key state never moves

Posting the game's OWN window a `WM_KEYDOWN` carrying the key's scan code makes `Input.GetKey` read true on the next frame, and a `WM_KEYUP` carrying the scan code plus the transition bits clears it, with `GetAsyncKeyState` at 0 and no foreground change throughout — a message to the client's own window from inside its own process, which is what keeps it inside the `input` dependency's rule. A key-down alone is NOT the capability: prove the key-up too, because a half-verified hold leaves the modifier stuck down for the rest of the session — `tools/acceptance/recipes/key-hold.cs`, `docs/evidence/selfchecks/tooling/acceptance-key-hold-selfcheck.md`, decision 237.

## 2026-10-06 — Batch `20261006-g`: a gesture whose meaning is read at RELEASE time must be staged and released in one call

The game closes its own ring between evaluator calls, so every gesture whose classification is decided at release — the ring's focused body, `RemoteBackpackView.IsOpen` for `TransferToBody`, the cast button's own item — is staged AND released inside ONE invocation (two calls answer `buttonCount: 0` and the release falls through to the world path), and further Tab presses do not reopen what the second one closed: clear the drag with the probe and open the ring from a clean state. A divergence warning is attributed by the SHAPE of the move rather than by who drove it (take the owner-local control in the same session), and a member joining a run in progress is a different code path whose entry-time warnings belong to no row under test.

## 2026-10-06 — Batch `20261005-b`: name the line that grew the log, not the line that explains the state

Measure the lines per class before naming a culprit: the diagnostic that explains the observed state and the one that produces the volume are often two different lines (a diverged world's `[ItemPhysics] settle` at Information was 4,445 of 7,368 lines, while the `[LayerMod] baseline divergence` warning that explained it rode the 5-second keyframe and cost a few dozen), and a per-frame line at Information is the usual culprit.

## 2026-10-06 — Batch `20261006-f`: a gesture that "did nothing" is diagnosed by its inner exception

A release that aborts inside the game's own body is silent to the wire (`produced no intent — unclassified native gesture` reads like an unclassified gesture and is an aborted one): `dragAfter` still naming the item is the tell that the native body never reached `dragItem = null`, and an ad-hoc probe that catches the inner exception and prints its stack is what names it — never a verdict written from that line.

## 2026-10-06 — Batch `20261006-h`: give each fixture role its own definition, and read a swallowed key as a no-op

`container-fill` addresses a container by TYPE, so two carried containers of one definition make it bind the wrong pair (give each role its own: `trashbag` outer, `plasticbag` target, `duffelbag` source), and a fixture target must be able to hold the child at all — read the prefabs' capacities in one probe before staging, because the refusal the row wants is the one AFTER `CanHoldItem` passes. A key swallowed by an open Online UI window reads as `posted: true, heldAtEnd: true` with the ring still shut and no error anywhere: close the window and read the toggle back (`radialOpen`/`buttonCount`) before staging the drag.

## 2026-10-06 — A refusal is read from its own line, not from the item's parent

Read the refusal from the release probe's own pair (`childInCast`, `castContentsBefore` → `castContentsAfter`) plus the `[RemoteIntent]` line, and confirm the item stayed put with the owner's tree probe (compare it by hash, not by eye): a refused release and a landed one can share `childParentAfter`. The refusal line's own target wording is the discriminator (`display proxy item <id>`, `a slot of the displayed inventory`, `display proxy container <id>`), a refused release has already cleared the drag, and `remote-gesture mode=open owner=auto` is usable only with ONE guest — pass the focused player's SteamID in a three-client session.

## 2026-10-06 — The held key lands on the NEXT input, so the read step is the verdict

An input cannot read the state its own message produces (the client's frame handles it only after the evaluator returns), so a hold is judged by the `read` step that FOLLOWS it and the recipe reports `heldAtStart`/`heldAtEnd` as readings plus `posted` as what it queued — never as a verdict, and never "fixed" with a longer sleep inside the eval. The ban's exception is two names wide (`PostMessage` and `GetAsyncKeyState`, excused in `tools/acceptance/driver/eval-declarations/` only), the scan code is asked for the key the bind RESOLVED to rather than the generic one, a key-gated branch is driven together with its control gesture, the game's own ring opens with one `toggleinventory` press edge (release it with a following `up`+`read`), the Online UI window is closed before driving the game's own keys, and a `[DllImport]` declaration is a whole input of its own that the driver probes before sending.

## 2026-10-06 — Batch `20261006-a`: a zero-warning row is read together with the polarity that produced it

Prefer one `-Match` over one window that carries both the absence and the reason it is the fix (the drop's own `replayed native … no immediate re-report` line beside the container kinds' `immediate re-report`), because a bare warning count proves nothing. A wearable drop needs the OWNER dressed in the type the operator drags (each direction its own owner), a `mode=hover` radial stage does not survive the game's own frame — name what was ATTEMPTED and the committed path that exists (`mode=release cast=-2` forces the ring in the same call) — a stale `camera.dragItem` silently disables the ring (clear it and `radialOpen` before staging one), a recipe's guards stay on one line each in a pure-ASCII file and are proven by invoking it, and a missing fixture is a recipe (`tools/acceptance/recipes/item-wear.cs`) rather than a weaker row.

## 2026-10-06 — A "local control" is only local if the ring is on your own body

`InvButtonBodyPatch` redirects `InvButton.GetItem()` to the focused clone, so a local cast at an index captured with a remote view open aims the dragged item at the OTHER player's proxy: open the remote view for remote gestures and never for a local one. A release's cast index is scene state — re-list the buttons immediately before every release (a closed ring lists zero and falls through to the world path), open the ring with the game's own `toggleinventory` toggle with no drag staged (a staged drag force-closes it), and read a load into a display proxy by the target's identity rather than as a refusal, because the two produce the same log shape.

## 2026-10-06 — Drive a key-gated gesture together with a LOCAL control of the same gesture

The same release driven by the OWNER (no operator, no intent) reproducing the identical hook pair and the identical peer warning is what moves a finding from "the remote-intent path" to the hook's own classification — and the call shape that reports correctly sits beside the one that does not, in the same log. An owner-side fixture staged while a viewer already has that owner's view open warns on its own: stage an owner's fixtures before opening the viewer's remote ring, or name the action that produced the lines — the hook's own `events=[Pickup]` beside `container-fill`'s `events=[ContainerContent]`.

## 2026-10-07 — Batch `20261007-b`: a fixture's created item ids repeat, so they identify nothing

The kernel allocates instance ids in a deterministic sequence, so the same staging order in a fresh world repeats them: compare two batches by their marks and timestamps, never by an instance id, and read an id in a record as "an item this fixture created in that session", comparing the batches' marks with `.acceptance/tools/log-census.ps1`.

## 2026-10-07 — Batch `20261007-b`: an absence row's window must open after the staging

Take the mark AFTER the last fixture action: `item-provide mode=create` + `container-fill` is a declared substitution that carries its own create shape (one warning per created item on the first snapshot after it), so a window opened before the staging blames the gesture for lines the setup produced — record the staging's own lines with their timestamps instead.

## 2026-10-07 — Batch `20261007-b`: the local probe client cannot deliver a submission that carries a quote

A submission carrying a double quote fails at `(1,1)` on the local probe client (`.acceptance/tools/hotrepl-eval.mjs`) while the same code runs through the driver, so an ad-hoc probe goes through the recipe channel — an existing recipe whose reader already answers the question, or a new committed recipe — and the local client is used for quote-free expressions only.

## 2026-10-07 — Batch `20261007-b`: a documentation-only commit above the build commit is named, not hidden

A record names BOTH the run's commit and the artifact's `ProductVersion` and says the delta between them is documentation-only: the `+<sha>` is the commit the DLL was BUILT from, and `tools/verify-deploy.ps1` against this tree's build output is what makes the statement checkable.

## 2026-10-07 — Batch `20261007-c`: a census invoked through `powershell -File` binds no array argument

`-File` passes every argument as a literal string, so `-Patterns 'a','b'` arrives as `a,b` and matches nothing while reporting `= 0` per client: call a local helper in-process with `&`, where array binding is ordinary PowerShell, and read a comma-joined pattern name in its output as the tell that the census checked nothing — keeping `powershell -File` for the scripts whose contract is "ASCII, no dependent modules" (`preflight.ps1`, `session-environment.ps1`).

## 2026-10-07 — A park window is only as long as the command that holds it

A park is the wall clock between arming and clearing it, and a SEQUENCE of driver calls is not a hold (three calls took 0.4 s; the change it must cover takes seconds): arm every client the row needs held inside ONE command, read each arm result (`armed: true`) BEFORE driving the change so a failed arm cannot silently produce an unprotected attempt, hold with an explicit `Start-Sleep` bounded under the 15-second `GuestHostSilenceWatchdog`, then disarm — and read the parked member as the control both ways.

## 2026-10-08 — Batch `20261008-a`: the parked spot is the map's corner, and it decides what the rows can show

The holder parks everything at the world origin, which on this world is a solid, unlit cell at the map's corner hundreds of units below the inhabited floor: a presentation row driven there is judged on the machine read (every renderer, collider and sound component in the subtree off — read a source's `isPlaying`, not only its `enabled`) with the frame offered to the user as a residual, and a decay row is not reachable while parked at all.

## 2026-10-08 — Batch `20261008-a`: the drag gesture needs the pointer ON the target, and the camera sits below the body

The camera sits ~3.8 world units BELOW the player's body, so a world point derived from the pointer is below the feeder's feet and the product's line-of-sight gate refuses it: drive that chain through the production entry the gesture itself calls (`IPlayerInteractionControl.SendUseRequest`), keep the committed `remote-gesture` recipe for the gesture's own half, and remember that a release the gesture refuses still runs the native path and DROPS the item.

## 2026-10-08 — Batch `20261008-a`: the host's own starting supplies carry no instance id until they enter the domain

A standing-object fixture binds the host's own items first (`item-provide … mode=create`, whose pickup allocates the id, or any local slot move): the host's supplies are the authority and get their ids lazily on first domain entry, so an id-less carried row is exactly the row `StandingItemPlan` leaves out — and the record says the plan excluded it rather than the materializer missing it.

## 2026-10-08 — Batch `20261008-a`: a frame taken while the player is unconscious proves nothing (the user caught it)

Judge no visual row without reading the OBSERVER's own state in the same step as the capture (`alive`, `conscious`, the consciousness value, any blackout or blind effect) and writing that reading beside the frame: the game dims a blinded or unconscious player's rendering, so a dark frame is never "nothing visible" without it, and a frame whose observer state is unknown is not evidence.

## 2026-10-08 — Batch `20261008-a`: a fixture item's side effects are the run's own, and they were not watched

A fixture with a self-destruct, world-spawning or hostile side effect is left out or declared in the scope WITH its consequences, and the run's census searches for the lines that behaviour produces (`[EntitySpawn]`, `[EnemyAttack]`, the pain vocals) over the whole run window: an absence row needs a census that spans the event which could create one, and a census read before it is a snapshot, not evidence.

## 2026-10-11 — Batch `20261011-b`: a host-side removal is undone by the target's next report, and a flag row would never have seen it

A removal row reads the COMPONENT, not the flag — a flag-only reading passes while nothing was removed — and reads it on the target's own client AND in the authority's stored copy one report interval later, because a guest body is report-driven and its own report puts back what a host-side-only removal took away — `docs/backlog/done/medical-removal-limb-component-not-applied.md`.

## 2026-10-11 — Batch `20261011-b`: extract a client's log before you restart it

Before quitting or restarting a client, copy the window that carries a finding into the batch's artifact directory as its own file and cite that file: the rolling log is per-run and a restart rotates it away (into a `.gz` beside it, which a `GZipStream` can still recover), and a mark/read pair's offsets belong to the old file.

## 2026-10-11 — Batch `20261011-c`: a drive that builds the surface by hand invents the defect it drives

A recipe that needs a user-reachable surface opens it through PRODUCTION's own entry (the hand-built focus was itself the defect a whole batch then filed) and reports the guard readings beside the result, so a refusal names itself instead of being worked around; where the surface must be a specific kind of object, the drive reports the witness for it rather than assuming one — `tools/acceptance/recipes/`.

## 2026-10-11 — Batch `20261011-c`: a probe that reads a field as a property reports `false` for ever

A probe reports a production PREDICATE's own answer (`SessionService.IsRemoteInWorld`) instead of re-deriving the rule from a member shape, and where it must read the member it accepts a field or a property: `GetProperty` on a public field returned null and the serializer printed `false` for every member, without an error — a reading a probe invents is worse than no reading, and this one manufactured the defect a batch filed.

## 2026-10-11 — Batch `20261011-c`: a drive's witness must be production's own fact, not a naming convention

The witness that says "this object is the kind the row is about" is the marker production itself acts on (the `RemoteMedicalDisplayBody` marker the coordinator puts on the copy's root), with the name reported beside it as information: a comparable name is at best second-best and at worst about the wrong object in the hierarchy.

## 2026-10-11 — Batch `20261011-c`: an icon row is judged by removing the state, not by recognising a sprite

Judge a display row as a pair — the icon present while the state is, and GONE after the state is removed by a driven action — which turns "this looks like the game's icon" into "this icon's lifetime is that state's", and seed the peer's limbs with one component each so one removal answers one question — the record's own row table (`docs/evidence/acceptance/`).

## 2026-10-11 — Batch `20261011-d`: a store the FRAME makes needs the pointer where the frame looks

A favourite-store row needs the physical cursor over a ring button AND a staged display-proxy drag at the same moment (the native store is `Input.GetKeyDown(favourite)` over the first `InvButton` the frame's own cast list contains), and with the pointer outside the client window the ring is turned off within a frame: read the geometry first, know that a button can only be brought under the pointer by moving the FOCUSED body (`remote-gesture mode=aim-ring`), and record the row `unproven` when no legal lever can put the pointer there.

## 2026-10-11 — Batch `20261011-d`: a staged proxy drag is LIVE between calls, and it will drop the fixture

Stage the drag and press the key in ADJACENT calls and CLEAR the drag when the step is over: a left-behind `camera.dragItem` is the scene state a real drag holds, so any release the game then sees runs the whole native release path and the ring's buttons stop matching the clone. Keep the fixture's host ON the surface — a teleport that leaves the body falling empties the wardrobe the rows need, silently, until a peer's clone count or a census is read.

## 2026-10-11 — Batch `20261011-d`: `quit` is asynchronous, so a relaunch is a duplicate instance

After `quit`, read the process list until the client is gone before launching: the quit returns when the request is sent, the process lives for seconds afterwards, and Sandboxie refuses a second instance of the same program in the same box — the gate is the missing process (or the guest log's `Steam API shut down`), not the quit call's answer.

## 2026-10-11 — Batch `20261011-d`: the fixture vocabulary is the game's own item table and its own readouts

Ask the game what its items are instead of naming fixtures from memory: `recipes/item-census.cs` answers from `Item.GlobalItems` (`mode=combineable` the ids that admit a pair, `mode=tag:<t>` the ids carrying a native tag, `mode=info:<id>` one id's stats, `mode=body` every carried item with the flags, tags, battery slot, ammunition count and liquid total the native branches read). Prefer the `9mmround` + `smallmagazine` combine, whose result is a NUMBER, over the condition merge, which is a no-op at full condition.

## 2026-10-11 — Batch `20261011-e`: the key channel answered `posted` and never reached the game

A batch that builds a row on the committed key channel verifies the channel FIRST — one `down`, one `read`, and the state in the read — before spending a session on the row: `posted: true` with `heldAtStart`/`heldAtEnd: false` on three consecutive calls means the game never saw the key, whatever the recipe queued.

## 2026-10-11 — Batch `20261011-e`: fixture items do not survive a cut, and a body that fell to death cannot pick anything up

Plan the rows that need a carried item AFTER every cut/continue cycle, or re-create the fixture after it (the character record round trip does not bring created items back), and check `canPickUp` (the limbs' own `totalForce`, six slots in one probe) before a row that needs hands: a fall inside the run's own staging left a revived body whose limbs stayed at zero force, refusing every slot.

## 2026-10-11 — Batch `20261011-f`: a remote ring button reaches the pointer by moving the CAMERA, and the pointer must be inside the window first

The remote ring is anchored to the FOCUSED clone's screen position, so where a button lands is decided by the clone's position relative to the camera and never by the pointer; the committed order is (1) foreground the client's window with the pointer inside its client area, (2) `remote-gesture mode=aim-ring cast=<n>` for the world delta, (3) move the LOCAL body by the negative of that delta, (4) `mode=list` until the target button reads `pointerInside: true`, (5) `mode=hover cast=<n>`, (6) the press — while the LOCAL ring stays fixed on screen and is addressed by putting the pointer on the coordinates `mode=list` reports.

## 2026-10-11 — Batch `20261011-f`: a real key press needs the FOREGROUND window, and the user said so first

Windows delivers a key to the foreground window only, and clicking a title bar to raise a window moves the pointer the gesture also needs: raise the window from inside the run (`AttachThreadInput` to the foreground thread plus `SetForegroundWindow`), read the client's own `focused` and `inside` before the press, and ask the user for the PRESS alone — never for a window switch between two instructions.

## 2026-10-11 — Batch `20261011-f`: a transferred item's adoption lands one beat after the pickup

Judge "the mark became the item's own" on a re-read and on the adoption's own log line, not on the first census after the transfer: the adoption rides the character-data path, so the item's own flag is written after the transfer call has returned — judge it on the adoption's own `[FavouriteMark]` line beside a re-read.

## 2026-10-11 — Batch `20261011-f`: `item-world-read` did not see a just-materialized world item

Judge "the item is in the world" on the spawn line plus the pickup that consumes it, and treat `item-world-read` as an unproven witness for a just-dropped item until its coordinate contract is pinned — it echoes the pair it was given as `cellX`/`cellY`.

## 2026-10-11 — A development probe on a CONTINUED world: the first driven act lands inside the restore's own wipe window

A continue's landing is multi-seam and `inWorld` is not its end: wait for the restore's own completion line (`[GenItems] restored cut: …`, or the live-world restore line) before driving anything, because an act driven inside that window is wiped by the restore and no id can be allocated while the world is still generating.

## 2026-10-11 — Batch `20261011-i`: two window captures seconds apart are not an A/B

Budget a visual row as ONE frame read against a machine read, never as a two-frame comparison: against a control of two captures taken back to back with no state change between them differing over 284,218 of 637,332 pixels, no one-switch change can be isolated by subtraction — measure the noise floor first when a comparison still looks like the only way, and write the number into the record — `.acceptance/20261011-i/n1-noise-a.png` and `n2-noise-b.png` are that measurement for this machine.

## 2026-10-11 — Batch `20261011-i`: the terrain's sorting order decides whether a frame can carry a visual row at all

Plan a "nothing visible" row against the draw stack, not against a camera position: the covering tilemap drew at sorting order 1000 while an unheld item's sprite carried order 0, so a frame could not tell "the switches took" from "the rock covers it" — `tools/acceptance/recipes/draw-at.cs` answers it in one eval (every enabled renderer whose bounds contain a world point, with its layer and order).

## 2026-10-11 — Batch `20261011-i`: a mod-API ticket waits for a mod that exercises what it added

A batch that judges a mod-API ticket names the mod it loads and what that mod REGISTERS before the session; deploying a DLL that registers nothing is not a capability, so when no existing sample registers the construct the row stays `unproven` and the gap is a fixture for a development cycle rather than something to approximate.

## 2026-10-11 — Batch `20261011-j`: the fixture a row reads must be materializable, and placing it is not the deploy's job

Ask "can this declaration produce an instance" before writing a row that reads one (a definition that is static item info only is invisible to every instance-reading probe), give it a vanilla prefab base in a DEVELOPMENT commit so the batch runs against that commit, and place the built DLL in the plugins tree by hand for the session — `tools/deploy.ps1` deploys no mods — then remove it at the close.

## 2026-10-11 — Batch `20261011-j`: a row about a DEFAULT must be read below the old default

A change that alters a default is only observable in the states the OLD default rejects: force the reading body below the inherited gate (`body-force intelligence=0`, read back in the same step) and carry a POSITIVE CONTROL in that same state, so the run shows the branch flipping instead of asserting that it would.

## 2026-10-11 — One throwing frame can brick the process: the call-context depth guard latches

Drive a fixture known to throw LAST in its session, and restart a client whose scope guard has latched (`CallContext.Enter depth 16 exceeded`) rather than fighting it. The lesson inside the lesson: a mechanism sentence is built from the ARTIFACT, not from the frame the log happens to print first — this batch's first attribution named two frames that carry no CUO scope, and its own review caught it.

## 2026-10-11 — Write probes as files on this channel, not as inline snippets

An inline `--code` submission has to survive the shell, the argument quoting and the client's parser at once, while the file form has none of that and leaves the probe in the batch directory where a record can cite it: every cited probe is its own file, reusable ones are promoted into `tools/acceptance/recipes/` before the batch closes, and `ok` WITHOUT a value is a failed probe rather than a passing one.

## 2026-10-11 — The remote half of the wound-view release had no committed drive at all

`tools/acceptance/recipes/remote-medical-release.cs` (args `item`, `limb`) is the cross-player half — the display focus open (`remote-wound-special mode=open`), the view pointed at the treated limb, a local item dragged onto that limb's `WoundViewLimb` — and a future topical, injection or limb-tool row starts from it instead of rediscovering that `remote-gesture mode=medical` refuses by design unless the limb is the acting client's own.

## 2026-10-11 — Batch `20261011-c`: an exception census names the known startup line, and an artifact is checked before it is cited

Count exceptions per peer AND name the one known startup line with its timestamp (every client throws the `DataAnnotations` `TypeLoadException` once at plugin startup), so the record separates "the session was quiet" from "the client logged its usual line"; count the MECHANISM (the exception type or its message) rather than the `[Unity:Exception]` marker as a second dimension, since the marker is part of every such line and two equal columns are one reading twice; and list the batch directory to check each cited artifact exists BEFORE writing the record, because a reading that lives only in the transcript is not evidence a later reader can open.

## 2026-10-12 — A driver answer with non-ASCII text does not survive a plain `Get-Content`

Read every driver answer a run parses with `-Encoding UTF8`: Windows PowerShell 5.1 reads a UTF-8 file with no BOM through the system code page, so a gate text carrying an ellipsis is mangled on the way in and the failure lands INSIDE the parse (`ConvertFrom-Json` naming a byte offset and nothing about encoding) — an encoding symptom first, a protocol symptom second.

## 2026-10-12 — The anchor check is a tool, and the tool lies in two familiar ways

Resolve a test anchor by name PREFIX against the outcome index (an xunit theory's display name carries its arguments, so an exact-name lookup misses the family) and check a quote with literal containment (`Contains`, or `-SimpleMatch`: PowerShell's `-like` treats `[` and `*` as pattern syntax) — re-derive a "missing" anchor with the literal tool before calling a row unsupported.

## 2026-10-12 — A reversal must reverse every sweep the batch ran

A sweep script carries a DIRECTION, not just a rule: re-running the one-direction `review/` → `done/` reference sweep after the tickets came back silently put the index rows and a decision entry on `done/` again, and only the gate suite saw all four failures at once.

## 2026-10-14 — A missing sandbox dependency can come back without reinstalling anything

`sandboxie` reported `SbieSvc and SbieDrv: found 1 of the 2 services` with every program file in place: the kernel driver's own service registration was gone (the System log showed the service stopping unexpectedly, then a reboot), and starting the service registered it back — the next preflight reported `RESULT: OK` with no reinstall and no reboot. When the preflight blames a program-plus-service pair, read WHICH half is missing and offer the start-it option before proposing a reinstall; the repair is the user's action (`docs/acceptance/dependencies.md`, "Asking the user").

## 2026-10-14 — A batch-local probe harness takes the FULL placeholder name

A local `probe.ps1` substitutes placeholders verbatim, so a template's `{{s:id}}` needs `-Arg s:id=7`; the committed driver's own `-RecipeArg id=7` spelling matches nothing, and the harness's leftover check cannot see the colon — the un-substituted file is SENT and the evaluator answers `ok:true` with no `value` field instead of raising a compile error. Pass the typed name, and read an answer with no `value` as "the code never ran" (`tools/acceptance/drive-in-process.ps1` is the driver whose spelling differs).

## 2026-10-14 — A page-agnostic UI row is only covered on the pages a batch photographs

The UI tickets' rows read "Any page" and "an English page", while the batches that judged them photographed Home, Worlds and Preferences only: the Admin page collapses every rule row's label to one character per line and paints it over the rows below, in English and Chinese alike, which no earlier frame could have shown. A batch that touches a UI ticket photographs EVERY page it can open and captures the same page in both languages — the A/B is what separates a translation defect from a layout one (`docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261014-a.md`).

## 2026-10-14 — A measurement taken from the container of the thing measured reproduces itself

A layout rule that asks a CONTAINER for the size of the control inside it is asking the box how wide the box is — the container's own preferred width is its children summed, so it contains the value the rule is about to write and grows by its own padding plus its neighbour every frame (a 984-unit page carried rows of 30537, labels squeezed to one character per line) — so when a rule needs a size, name the object that size describes and read it there, once, before anything writes it (`docs/backlog/todo/online-ui-layout-and-input-detail-pass.md`, *Fourth pass (2026-10-14)*).

## 2026-10-15 — A budget read from the container it is shared among agrees with itself

A strip that SHARES a fixed band among its tabs was budgeted from the band's own rect, and the band is as wide as its children's minimums — each of which the sharing rule had just written — so the budget equalled what was asked for, nothing was ever given up and the strip stayed exactly as wide as its own content for ever (`Tabs rect=1038.7x36.0` against a shell of `984.0` in one frame, batch `20261015-a`); a self-measurement does not have to diverge to be wrong, and a fixed point that agrees with itself looks settled in every reading a probe can take, which is why this run reads the container's own width BESIDE the band's and exercises the share by narrowing the window (`docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261015-b.md`).

## 2026-10-16 — "None here" from a probe grid is a claim about the GRID

Before a row is recorded as "this world has none of X", read the population over the whole loaded scene, because a probe that covers the cells around a body measures those cells and nothing else: batch `20261016-c` counted 1365 `requireGround` buildings where the previous batch's 5×4 grid had concluded there were none, and both its record and its ticket repeated that conclusion until a census replaced it — `tools/acceptance/recipes/building-support-census.cs`, `docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`.

## 2026-10-17 — A recipe's type names and field names are read from the tree, never guessed

Four runs of batch `20261017-a` produced nothing but `eval 'recipe:member-identity-forge' failed: (1,1): InteractiveHost` — the evaluator's Mono REPL names no line and no symbol, so each wrong guess cost a whole three-client session: the recipe had put `IKernelProtocolControl` under `Runtime.Session.Items` (it lives in `Application.Kernel`) and `EnemyEffectKind` under `Runtime.Protocol.Messages` (it lives in `Runtime.Protocol`), and then read the pickup id from a field the host's record does not spell that way (`container-read mode=host` writes `"id"`, not `"instanceId"`). A recipe is compiled against the loaded assemblies with no editor and no diagnostics, so every type, member and JSON field a NEW recipe names is opened in the tree first (`src/**` for the type, an existing recipe's own answer for the field), and a recipe that must read one number is written as the smallest thing that returns it (`tools/acceptance/recipes/member-id.cs`) and read on the machine before it grows branches.
## 2026-10-17 — A recipe's `// args:` line is a requirement list, and a cell's state belongs to the host

`drive-in-process.ps1` REFUSES a recipe call that omits any parameter its `// args:` line declares — `block-drop-forge`'s optional-looking `absolute`, `building`, `airwrite`, `item2` and `condition2` are all required — and it says so as exit 64 in the DRIVER log while the batch log shows only `ok=missing`; and a probe that needs a cell HOLDING a block cannot find one by probing offsets around the member (eight `block-read` calls all answered `block=0`, and the reading was reached only because the absolute cell the forged break landed on happened to hold block 14). A cell's state is the host's own fact, so ask the host; a setup step that failed is a reason to stop rather than to continue, because a batch that reached its setup by accident cannot say what it proved.

## 2026-10-17 — A green suite is not a start

Every test fakes the give source, so 693/693 and 5697/5697 were green while the deployed build died in `Awake`; only the prelude composes the real adapter — `docs/acceptance/workflow.md` §2/§5.

## 2026-10-18 — A member's console verb answers later; read the console history

A member's `/give` returns no line and the host's answer lands at least a frame later, so the driver's `lastLine` right after the call is the PREVIOUS line: read `tools/acceptance/recipes/console-read.cs` after a bounded wait and judge the newest line — `docs/evidence/acceptance/give-item-to-a-player-command-20261018-q.md`.

## 2026-10-18 — A 15 s blackout window is one process's budget, not a step-by-step driver's

The member ends its own session 15 s after the last host frame, and a host layer change (`skiplayer`) spends 8-13 s of that budget re-entering the world. The committed driver spawns one Windows PowerShell process and one websocket per step (~2.2 s), so a correct step-by-step window still lands past the deadline — batch `20261018-s3`'s first attempt answered `No active session` at 15.2 s, with the host ready 1.7 s earlier. Two rules from that run: arm the blackout BEFORE `skiplayer` (without it the member's own generation follows the host to the next layer within ~3 s, before the host re-enters), and drive the window itself over sockets opened before it, reusing the committed recipes through the same in-process evaluator protocol — the window then closes in ~8 s. The helper is a batch-local artifact named in the record (`docs/evidence/acceptance/item-and-entity-data-commands-20261018-s3.md`); a committed fast-window action would let the next batch drop it.