# The Remote-Clone Failure Warning Storm — Self-Check (2026-10-07)

Delivery fact sheet for `docs/backlog/review/remote-clone-warning-storm-on-member-dropout.md`, filed by
acceptance batch `20261007-a`: while a member was out of the world, one client's rolling log grew 8.796 MB in
about 90 seconds, **58,148** of its 58,960 lines the same clone-creation warning
(`docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md`, row 2). The cycle bounds that producer
with the mechanism the family already answers with; the reason the clone cannot be built, and the member's
exit from the world, stay `docs/backlog/todo/layer-change-member-recovery.md`'s and are not claimed here (§4).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The batch's reading: after a `skiplayer` layer change the third client (sandbox `Steam2`) was out of the world from +5 s to +60 s (`inWorld: false`, `container-read mode=local` → `no-local-body`), and its own rolling log grew without bound — 58,960 lines / 8.796 MB in ~90 s, **58,148** of them this one line; a further 10-second sample measured **1.212 MB**, i.e. 7.27 MB/min (the batch record writes 7.25, its own rounding), at 639–866 lines/s, still climbing when the session closed (the census taken afterwards reads 82,618 such lines / 12.442 MB for that client since the mark). | the batch record's row 2 and its artifacts `ev-alt-storm-head.txt`, `ev-alt-storm-tail.txt`, `ev-alt-storm-census.txt` |
| 2 | The repeating CALLER is the renderer's lazy clone ensure: for every roster member the local session believes is in the world, `RemotePlayerRenderer.Update` retries clone creation every frame, and a null return `continue`s to the next frame. The retry is deliberate (it absorbs the ordering races of a member joining mid-session), so the CALLER is not the defect. | `RemotePlayerRenderer.Update`'s ensure block, its own note, and the `continue; // template unavailable — retry next frame` |
| 3 | The repeating LINE was written inside the factory, at Warning, once per attempt: `Remote body: no Body component in "Experiment" clone.` Its sibling branch — `Remote body: "Experiment" player object not found in scene.` — is the same shape one branch over, and the gate's census did not name either. | `RemoteBodyFactory.CreateRemoteBody` (pre-fix), the line as quoted by the batch record |
| 4 | Which of the two failures fires, and WHY the local scene has no usable Body in that state, is NOT attributed by this cycle: the batch's own reading is the third client's log and its in-world state, and the local scene's contents at that moment are `docs/backlog/todo/layer-change-member-recovery.md`'s reading (the member's exit has a trigger — an inbound message processed during the change — but its mechanism is unattributed). | the batch record's rows 1 and 4, `docs/backlog/todo/layer-change-member-recovery.md` |
| 5 | The family's answer, already in the tree and already applied to the two producers the 2026-10-06 cycle bounded: a repeatable diagnostic asks `LogRepetitionGuard` (one window per SUBJECT, the key carrying the value), refuses identical repeats, counts them, and reports what it swallowed when the run ends; the level policy picks the level by trigger frequency and puts a repeatable diagnostic behind a window rather than dropping it. | `LogRepetitionGuard`, `ItemDistanceLog`, `LayerModifierSync.ApplyIndex`/`FlushRepeat`, `AGENTS.md` (*Engineering Discipline*) |
| 6 | Why it survived the earlier sweep: `LogVolumeGateTests` pins the sites that cycle could NAME (two per-frame correction lines, two snapshot diagnostics, one 10 Hz receive handler). This producer was outside that census — the ticket's own statement — and nothing else in the tree bounds a line written from a per-frame retry. | the ticket's Symptom, `LogVolumeGateTests` (pre-fix, three census floors) |
| 7 | The failure does not resolve while the state stands, so the window's END is the only place its size can be told — and in the storm it never came: the client closed first. The drain therefore needs a wind-down that does not depend on resolution, which is why the guard gained a bounded view of the subjects it is still holding. | the batch's reading (still climbing at close), `LogRepetitionGuard.Subjects` |
| 8 | The family sweep this cycle ran, by the only method that is bounded and reproducible: the files declaring a per-frame pump (`void Update(` / `LateUpdate(` / `FixedUpdate(` / `Tick(`) that also call `LogWarning(` / `LogError(` — six files, seventeen sites, each attributed to its enclosing method (the two view opens and the probe read in full). Two are click-driven view opens (`RemoteMedicalCoordinator.Open` → `TryCreateDisplayBody`, `RemoteBackpackCoordinator.Open`) — one line per user action, with no auto-retry behind them, so they are a refused action rather than a per-frame producer; the UI facts probe is paced by `OnlineUiNativeFactsCapturePolicy`, whose own cases pin "a finished run must never ask again"; `Plugin.OnUnityLogMessage` is a sink for the game's own volume; and the rest are one-shot startup or message-driven refusals. The per-frame/per-message producers are the five the gate names. | that `Select-String` sweep over `src/**.cs` (reproducible from the tree), `OnlineUiNativeFactsCapturePolicyTests`, `LogVolumeGateTests` |

## 2. The change

- `LogRepetitionGuard` (Runtime): gains `Subjects` — every subject still held, oldest first, as a COPY the
  caller may iterate while it flushes. The guard already kept the subject set and the per-subject count; only
  the walk was missing, and the wind-down is its one caller (`TrackedKeys` stays the leak probe).
- `RemoteCloneFailureKey` (GameAdapter): the window's subject — the failure's own wording AND the member, a
  value type so the guard's `Equals` compares values. The wording is part of the key on purpose: "the scene has
  no template" and "the template cloned without a Body" are different facts, so the second is news rather than
  a repeat of the first.
- `RemoteBodyFactory.CreateRemoteBody`: both failure branches now ask the window and write their line INSIDE
  that ask (`failures.TryLog(...)`), keeping the diagnostic and its Warning level; the two wordings and the
  one line template are constant in the factory, and the message now names the MEMBER it is about — the old
  line named none, so the storm's own log cannot say which member (or how many) it was reporting, and the next
  batch's census can separate them.
- `RemotePlayerRenderer`: owns the window (three lines per subject, capacity 64), passes it to the factory, and
  drains a run where it ends — the clone is built again (`Update`), the member leaves the world
  (`OnRemoteSceneChanged`, which also re-arms the window so a failure that returns reports its first line) or
  the session ends (`DestroyAllClones`, walking `Subjects`). Each drain reports the swallowed count at Warning
  through one template; a subject that cost nothing stays silent.
- `LogVolumeGateTests`: a new fact pins the ask-gating of both failure lines plus their census floor; a second
  pins the drain chain (the drain's own line must SIT inside the drain, both subjects end, the wind-down walks
  `Subjects`, and `Update` / `OnRemoteSceneChanged` / `DestroyAllClones` call them) plus the renderer's floor.
  The new `EveryLogSitsInsideTheAsk` matcher is the family's second shape — bounding by containment — and it is
  what the earlier matcher could not see; `EveryLogSitsInsideTheDrain` pins the summary's own spelling
  (`TryFlush`), and the early-return matcher keeps ITS spelling (`ShouldLog`) so the two cannot widen each
  other. The shared branch reader uses `DescendantNodesAndSelf`, because in the positive-gate shape the
  condition IS the call.
- No wire member, no save shape and no protocol number changes; the detector's information is kept (its first
  lines, its level) and only its repetition is bounded.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The window's contract with the wind-down walk: the subject list in first-seen order, the copy the caller can flush through (the entry list mutates under it), the cap that drops the oldest, and `Clear` emptying the guard while the taken copy survives | `tests/CasualtiesUnknownOnline.Tests/Items/LogRepetitionGuardTests.cs` — 19 cases, 3 of them new, all green |
| The producers ask the window and their runs are drained | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/LogVolumeGateTests.cs` — 25 cases: the new ask fact (log inside the ask, census floor 2), the new drain fact (the drain's line inside the drain, Warning level, two subjects, the `Subjects` walk, three call sites, renderer floor 1), five matcher theories with positive and negative samples (the ask matcher's negatives include "the ask is there and the log is outside it", the drain matcher's "asking a window is not draining one", and the early-return matcher's "an ask of the family's OTHER spelling cannot make an ungated line pass"), five census floors and three producers' facts |
| The pre-fix RED | the new ask fact was run on the UNMODIFIED tree first: `LogVolumeGateTests` reported exactly one failure, `TheRemoteCloneFailureLines_AskTheWindowBeforeTheyWarn`, naming `RemoteBodyFactory` and the 7.25 MB/min reading — the other 19 cases green. Its positive-sample failure also exposed a real matcher defect on the way (`DescendantNodes` cannot see a condition that IS the call), fixed before the implementation and pinned by the theory |
| The mutations | on the fixed tree: (a) the no-Body line moved OUTSIDE its ask (ask kept as a bare statement) → the ask fact RED, the other 20 green; (b) the ask deleted → the same fact RED; (c) `FlushCloneFailures()` dropped from `DestroyAllClones` → the drain fact RED, the other 20 green; (d) `Subjects` returning the live list instead of a copy → 2 guard cases RED (the walk mutates what it iterates). Each file was restored from a byte-identical backup and its SHA-256 compared (factory `529C663B…`, renderer `79B7ED9D…`, guard `FF73E8E4…`) |
| The build/format/gate run | `dotnet build` clean (0 warnings, 0 errors), `dotnet format` exit 0, the gate project and the behavioural suite green (counts in the commit's own record) |
| The runtime row | the next three-client batch re-drives `layer-change-member-dropout`'s fixture with BOTH members' inbound parked inside one command for ~9 s: the client's log growth is read as a SIZE (MB since the mark, `log-census.ps1`), and the new expectation is at most three lines per (member, failure) subject — 6 per member — plus at most one summary per run that ended, against 58,148 in the batch's reading |

## 4. Limits

- **No session ran in this cycle.** The reading is the batch's record and artifacts and the source; the runtime
  proof is the next batch's re-drive, and this cycle claims no green runtime row.
- **The failure's cause is untouched.** Why the "Experiment" clone carries no `Body` in that state, and why the
  member left the world, are `docs/backlog/todo/layer-change-member-recovery.md`'s; this cycle bounds the volume that
  state produces and nothing else.
- **The bound is per (member, failure), and that is a behaviour choice.** A roster of N members can cost 6·N
  lines (three per subject) plus one summary per ended run, and the window holds 64 subjects (two per member),
  so a pathological roster cannot trade a log storm for a memory one. The cap's mechanism, stated rather than
  implied: a NEW subject is tracked and the OLDEST is evicted — which is why the capacity is far above any
  roster this mod runs: with more live subjects than the window holds, an ask order that cycles through all of
  them would evict exactly the subject its own frame is about to ask, and every subject would report a first
  line per frame again. That needs more than 32 failing subjects, i.e. a roster this mod does not serve.
- **The attempt's own cost is silenced, not removed.** The no-Body branch still `Object.Instantiate`s a full
  character clone and `Object.Destroy`s it on every attempt (the template is only queried for a `Body` AFTER
  the clone exists), so at the batch's 639–866 lines/s the storm's client was paying hundreds of
  instantiate+destroy pairs per second, and it still would while the failure stands. This ticket owns the
  VOLUME that state produces, so the retry cadence is deliberately untouched: a cadence bound would delay the
  clone of a member whose failure is transient, and the state that makes it stand is
  `docs/backlog/todo/layer-change-member-recovery.md`'s to explain.
- **A client killed with no teardown never prints the wind-down summary.** The lines are still bounded (three
  per subject); what is lost in that case is the count, which is the same information loss any bound has when
  its process dies. The storm's own client is that case in the batch's reading.
- **The window is per process and is not cleared by a scene reload.** Its drain points are the three named
  ends; a reload inside an open run leaves the window spent, so the run's remaining lines are refused until one
  of those ends fires — the count is not lost (the wind-down still reports it), but the member's failure is
  then present as its first lines only.
- **The gate reads SOURCE, so it cannot see a per-frame line nobody named.** The census floors make a renamed
  or deleted site loud; a NEW high-frequency producer elsewhere stays invisible until it is named here, which
  is the ticket's own item 2 and remains a census, not a rate model.
