# Members do not arrive together when the group descends a layer

- Status: Review — code complete, waiting for batch `20261021-j` (three clients: one normal descent and
  one descent with the edge dropped on a member)
- Priority: Medium
- Category: World generation / layer transition / spawn placement
- Source: the user's 2026-10-07 backlog request — when the group goes down a layer, the members' spawn
  points are not together.
- Related: `done/layer-change-menu-round-trip.md` (the shared descent edge that made every member run
  the native generation in parallel), `done/layer-change-member-recovery.md` (the member that lost its
  body to the same boundary), `done/reenter-baseline-adoption.md` (a member entering after the host),
  `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs` (`RegenerateWorld`, `WorldPlacePlayer`),
  `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs` (`PlaceBody`),
  `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterDataStore.cs`

## What was observed (2026-10-07)

- A layer's generation ends by placing **the local body only**: `WorldGeneration` calls
  `PlayerCamera.main.body.PlaceBody()` after the world is built.
- `Body.PlaceBody` scans downward from the layer's top (`WorldGeneration.world.halfHeight` minus an offset
  per step) for the first position whose `origColSize` box does not overlap the `Ground` layer, and puts
  the body there — x is the vertical centre line, so the chosen point is an air pocket near the layer's
  ceiling. The tutorial override is the one exception and anchors to the `TUTORIALSPAWN` object.
- When the group descended, the members were pulled to the menu and re-invited; the member's own
  regeneration and placement happened only after the host's, and the handback carried the old layer's
  position. The separation was therefore a **layer change the member did not take part in**, not a
  horizontal offset.

## The reading (batch `20261021-i` and the re-entry log)

**The normal descent already lands the group on one point.** The shared edge (commit `a495c742`) makes
every in-world member run the same `WorldGeneration.RegenerateWorld` from the host's captured baseline;
the native placement above is deterministic on that identical world. Three clients read exactly the same
point before and after the descent: `(0, 504.62)` in layer 0 and `(0, 477.62)` in layer 1 on host, guest
and alt (artifacts `a-before-*-read.json`, `a-r03-*-read.json`, `a-after-*-read.json` of batch
`20261021-i`; the host's and the members' own bodies are each placed by their own client).

**The remaining defect is the re-entering member.** In the same batch's degraded session (the
`LayerDescent` edge dropped on the guest, which then went through the menu and re-entered), the guest
sandbox client's raw `BepInEx\LogOutput.log` carries `Character restore position applied at spawn:
(-1.5,506.6).` while the new layer's entry reads `(0, 507.62)` on the host and alt. The numbers happened
to be close in that world; the mechanism is layer-blind and would put a returning member at any old
coordinate (possibly inside the new layer's rock). **Evidence status**: the raw log is a machine artifact
outside git, and the batch's probe files are gitignored too; the tracked record
(`layer-change-menu-round-trip-20261021-i.md`) does not restate this line, and the batch's
`b2-after-*` probes do not reproduce the applied coordinate (physics had moved the body to
`(1.59, 507.62)` by the time of that read). The observation is therefore recorded as
observed-but-not-reproduced-by-the-tracked-readings; batch `20261021-j` re-stages the shape against this
fix, and its record is the tracked evidence.

**Why a one-instant clear is not enough (the independent review's blocker).** The first draft dropped
every saved position at the host's descent prefix. The member this fix targets is still in the old world
for a moment and keeps reporting:

- `RunCoordinator.cs` — `_characterData.Update(_localBody);` runs every frame while the local body exists,
  and `CharacterDataSync.cs` — `ReportCharacterData(_favouriteMarks.Capture(localBody, out var nativeFailure), throttled: true, nativeFailure);`
  sends the body's **current coordinates** once a second, with no "descending" suppression.
- The host stores a report by replacing the whole snapshot: `CharacterDataStore.cs` —
  `_savedCharacters[steamId] = msg;`.
- In batch `20261021-i` the host's `[LayerDescent]` line is at 22:13:02.650 and the guest's
  `Leaving the world to the main menu` at 02.738 (about 0.08 s), but the descent's own window runs to
  the members' InWorld at ~14.6 s (the degraded member's at ~19.2 s), and the reliable lane's latency
  and jitter (the batch's `NetworkHealth` line) mean a report can land **after** a one-instant clear and
  write the old coordinates back.

The fix therefore cannot be a clear at one instant: the layer a position belongs to must travel with the
snapshot.

## The rule

**The entry point is the game's own, and the host's baseline is the one input.** `WorldPlacePlayer` runs
`PlayerCamera.main.body.PlaceBody()` at the end of every client's own generation; on the shared host
baseline every client computes the same `(0, first air gap)` point, and the start gate releases the group
together. There is no second spawn picker and no per-client choice: "host-authoritative" here means the
host's captured baseline is the single world input, and the game's own placement is the single rule. A
broadcast of the host's body position was considered and rejected — it would override the game's own
placement with a second rule and need a wire field for no added guarantee.

**A saved position belongs to the layer that captured it.** The capturing client stamps its character
report and its local cut with its kernel run baseline (`CharacterDataMsg.Generation`, epoch + layer); a
restore compares the stamp with the receiver's own current layer and does not apply a position from
another layer (`CharacterRestorePositionPolicy.IsFromAnotherLayer`). The comparison happens when the
position is about to be **applied**, not when the restore arrives: a reconnecting client receives its
saved character before the run-baseline checkpoint (the handshake's own order), so at arrival its current
generation is still unknown; by the time the body exists, the baseline names the layer the body was
generated in. The snapshot's other facts (items, vitals, skills) still restore. A same-layer reconnect
keeps its position; a member that left before a descent or during it lands at the new layer's own spawn.

**A member that cannot follow keeps the ordinary entry path.** It must not start the native generation
without a local body: the game's `WorldPlacePlayer` dereferences `PlayerCamera.main.body`, so
`LayerDescentPolicy.DecideFollow` refuses a bodyless member (`NoBody`) and `RunCoordinator` guards the
`WaitingReady` branch with the same fact.

**The preserved body keeps its one-shot refusal, and the revive names itself.** The descent follower's
ordinary InWorld handback is still refused whole (body id, one shot). The host's next-level respawn sets
`CharacterDataMsg.Respawn`; that handback is applied even to the preserved body. The host's save of the
respawn snapshot projects its alive/conscious/limb facts into the kernel
(`PlayerKernelLimbProjection.SyncFromCharacterData`), so the kernel's terminal facts follow the revive and
the send's kernel projection confirms it rather than overwriting it with the death.

## What landed

- `CharacterDataMsg.Generation` (wire field 12, the snapshot's capture layer) and
  `CharacterDataMsg.Respawn` (wire field 11, a respawn handback). `CharacterDataSync` stamps every report
  and the local cut with `IWorldControl.CurrentGeneration`; the restore applies a stored position only
  after the run baseline names the layer the body was generated in, and one Information line names both
  layers when it refuses. `PlayerCharacterAccess.CloneCharacter` (the interaction services' save-shaped
  clone) carries both fields — a dropped layer stamp would let a later restore apply an old position.
- `CharacterRestorePositionPolicy` (Runtime, pure) and `IWorldControl.CurrentGeneration` (the kernel run
  baseline's stamp); `PrepareRevive` carries the stamp through a revive. A source pin
  (`SourceShapeGateTests.CharacterRestoreLayer_IsDecidedWhenThePositionIsApplied`) keeps the decision in
  the apply half: the handshake sends the character before the checkpoint, so a receive-time comparison
  would answer "unknown".
- `LayerDescentPolicy.DecideFollow` gains `hasBody` + the `NoBody` decision; `RunCoordinator` guards both
  descent branches and names the refusal.
- The first draft's `ICharacterDataControl.InvalidateSavedPositions` and its call at the host's descent
  prefix were removed: the review showed the member's own 1 Hz report can write the position back.
- Red observed first: the layer policy's `APositionFromAnotherLayer_IsNotApplied` failed on the skeleton
  (`Expected: True, Actual: False`); the store's respawn test failed against the unconditional kernel
  projection (the projection is correct — the save's kernel sync makes the respawn alive first); the
  clone test fails if the two new fields are omitted. Focused set green at the time of writing.
- Wire records: `CharacterDataMsg` round-trip test sets and reads both fields; the protocol page (en/zh,
  with the alignment record), the sync-coverage matrix P3 (anchor count 24 → 26) and
  `sync-coverage-evidence.json` carry the new facts; the acceptance lessons' scene-probe lesson now names
  the committed `tools/acceptance/recipes/scene-read.cs`.

## Required work, answered

1. **Attribution** — batch `20261021-i`'s three-client readings (normal descent) and the re-entry log line
   above; the code facts are the native placement and the store's wholesale replace quoted above.
2. **The rule** — above: the host's baseline as the single input, the game's own placement as the single
   rule, and the layer stamp for every saved position.
3. **Failure paths** — the blocked entry point is the game's own failure (`Body.PlaceBody` logs
   `No valid player spawnpoint!` and leaves the body where it was) and CUO does not select spawns
   (Non-goals): every client on the same baseline fails the same way, named in Limits. A dead member is
   placed by the native pass and revived at the new entry by the respawn handback (batch `20261021-j`'s
   dead session). A carried member is placed by its own client and re-pinned by the carry driver; not
   staged (Limits). A member out of the world at the edge is refused by the no-world/no-body decisions and
   enters by the ordinary path with the layer stamp. A member that joins mid-layer as a **new** player has
   no saved position and lands by the native placement; a re-entering member is the layer-stamp case.
4. **Three-client verification** — batch `20261021-j`'s rows below.

## Acceptance rows (batch `20261021-j`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | After a normal descent the host, guest and alt read the same new-layer entry: x equal within 0.01 and `abs(dy) <= 1` block, all three `layerIndex` advanced | machine | pending | `a-after-*-read.json` / `a-after-*-gen.json` |
| 2 | The follower's ordinary handback is refused (`dropped the saved-character handback`), its carried items are the same before/after, and no `Character restore position applied` line appears for it | machine | pending | guest log window + item census |
| 3 | With the `LayerDescent` edge dropped on one member, that member re-enters through the ordinary `WorldJoin` and lands at the new layer's entry (`abs(dy) <= 1` block against the host), and its restore applies no previous-layer position (`Character restore position applied` absent; the layer-refusal line present if a stale stamp arrives) | machine | pending | `b-after-*-read.json` + guest log window |
| 4 | A dead member is revived at the new layer's entry after the descent: the host logs `[Respawn] next-level respawn revived`, the guest's `health-read` answers `alive=true`, and its body reads the entry point | machine | pending | `dead-after-*` reads + guest log |

## Limits

- **The blocked-entry shape is not stageable and not fixed.** A layer with no standable gap is the
  game's own failure; CUO does not choose spawns (Non-goals). Every client on the same baseline fails the
  same way, so the group stays wherever each body was, which can be inside the new layer's geometry.
- **A carried member across the descent is not staged.** Both bodies are placed by their own clients and
  the carry driver re-pins the rider; the shape is named in the required work but batch `20261021-j` does
  not carry one.
- **A brand-new player joining mid-layer cannot be staged** (no fourth identity); its path is the native
  placement with no saved position, and a re-entering member's path is row 3.
- **A respawn handback re-sent before the member's next report applies twice.** The flag stays on the
  stored snapshot until the next 1 Hz report; a second `SendSavedCharacter` (a repeat InWorld edge or a
  handshake) carries it again and the local restore queue restarts the apply. The content is the same
  snapshot, so the result is the same character; the duplicate transient is not measured.
- **The raw `Character restore position applied at spawn: (-1.5,506.6)` line is an observation from the
  guest sandbox client's `BepInEx\LogOutput.log`**, not from a tracked artifact: the batch's probe files
  are gitignored and the tracked record does not restate the line, and the batch's `b2-after-*` probes
  read `(1.59, 507.62)` after physics had settled. It is recorded as observed-but-not-tracked, and batch
  `20261021-j` re-stages the shape against this fix.
- **A report received after the host's new baseline commit while its body is still in the old world** can
  carry the new layer's stamp with an old position; the stamp cannot separate those two facts. The window
  is the member's frame hitch, not a fixed duration (batch `20261021-i`'s descent ran ~12 s to the
  follower's InWorld and ~16.5 s to the degraded member's), and it is not measured.
- **A reconnect during the descent is not staged on the machine.** Its decision is made at apply time (the
  handshake sends the character before the baseline), covered by the pure policy test and the source pin
  that keeps the decision in `ApplyPendingPosition`; a real crash-restart row is a future batch, not this
  one.
- **The archive stores a snapshot's transient `Respawn` flag whole.** The ordinary restore paths do not
  read it; only the descent's one-shot refusal does, and the note is cleared with the body/session, so a
  continue cannot hand it to a preserved body. Named because the archive is a second reader of the shape.

## Independent review

Two adversarial passes (fresh contexts, the working tree frozen for each) are recorded here; quotes are
the reviewed code. Every finding below is fixed in this change.

### First review — the one-instant clear

**Blocker — the clear did not survive the member's own report.**

> `RunCoordinator` — `_characterData.Update(_localBody);` … `CharacterDataSync` —
> `ReportCharacterData(_favouriteMarks.Capture(localBody, out var nativeFailure), throttled: true, nativeFailure);`
> … `CharacterDataStore` — `_savedCharacters[steamId] = msg;`.

The old-world report rewrote the position the descent prefix had just dropped; the first store test never
simulated the report in between. **Fixed** by the layer stamp: the report carries the generation it was
captured in, so its position is refused at apply time even if it lands after any clear; the clear API is
deleted.

**Blocker — the code commit carried no ticket, record or protocol entry.** The ticket was still
`- Status: Todo` and its “What is not known yet” still asked for the entry-rule decision; the wire field
had no protocol/coverage/round-trip record. **Fixed** by this ticket's rewrite, the protocol page in both
languages plus the alignment record, the P3 matrix/evidence anchors, and the
`CharacterData_EveryFieldFamily_RoundTrips` additions.

**Major — the respawn flag proved only “not refused”, and the kernel projection could send a dead
character.** The review read `SendSavedCharacter`'s unconditional `_playerKernelRestore.Apply` and
`PlayerKernelRestoreProjection` — `health.Alive = player.Alive;`. **Investigated, not a defect, and now
pinned**: `CharacterDataStore.SaveCharacterData` calls
`_playerLimbKernel.SyncFromCharacterData(steamId, msg);`, which writes `data.Health?.Alive` /
`data.Health?.Conscious` into the kernel before the send, so the respawn snapshot IS the kernel's revive
and the send's projection confirms it. `SendSavedCharacter_RespawnRevivesTheKernelAndSurvivesTheHandback`
stages a dead kernel row, saves the respawn, asserts the kernel row is alive, and asserts the received
snapshot is alive and flagged.

**Major — the new wire field had no protocol page, coverage matrix, evidence or round-trip test.**
**Fixed**: fields 11/12 are set/read in the round-trip test, the en/zh protocol rows describe them, and
the P3 row (26 anchors) plus the two new evidence quotes carry the apply/refuse and respawn facts.

**Major — no post-fix machine evidence.** The deployed artifact was `0.1.0+a495c742…`; the reviewed
change had never been deployed. **Will be verified by** this cycle's commit → deploy → batch
`20261021-j` (the ticket stays in `review/` until its rows are judged).

**Major/minor — the `WaitingReady` branch bypassed the new body guard.** The review quoted
`RunCoordinator`'s `if (_descent == DescentPhase.WaitingReady) { … AcceptDescent(twice); }` and its
`NoBody` log “asks to be taken back in” (no request is made there). **Fixed**: both branches now pass
`HasLocalBodyForDescent()`, and the log says only “it keeps the ordinary entry path”. The duplicate
`Follow_AlsoForAMemberWaitingOnThisDescentForTheNextLayer` policy test was deleted.

**Minors.** The `Respawn` doc now says the flag rides the stored snapshot until the next report; the
archive keeps it and the ordinary restore paths do not read it (only the descent's one-shot refusal does);
the duplicate-application limit is named; the lessons entry names the committed `scene-read` recipe.

### Second review — the redesign

**Blocker — the reconnect restore arrives before the baseline, so an old-layer position was applied.**
The review quoted `HandshakeHandler`'s member branch (`ctx.CharacterData.SendSavedCharacter(sender);`
before `_kernelProtocol.SendCheckpoint(sender);`) and the receive-time check
`CharacterDataSync` — `if (CharacterRestorePositionPolicy.IsFromAnotherLayer(data.Generation, _world.CurrentGeneration))`;
the policy is `captured is { } from && current is { } now && from.LayerIndex != now.LayerIndex;`, so an
unknown current layer (a fresh/reconnected client) keeps the position, and the InWorld edge's
`_characterData.ApplyPendingPositionOnly(_localBody);` runs before the later handback could correct it.
**Fixed** by moving the decision to the apply half: `CharacterDataSync.ApplyPendingPosition` marks the
one-shot gate, then compares `pending.Generation` with `_world.CurrentGeneration` (now the layer the body
was generated in), and skips the write with one line naming both layers. The receive-time block is
deleted; `SourceShapeGateTests.CharacterRestoreLayer_IsDecidedWhenThePositionIsApplied` pins the call's
home so a later edit cannot move it back.

**Major — `PlayerCharacterAccess.CloneCharacter` dropped both new fields.** The save-shaped clone (every
interaction service saves it over the stored snapshot) listed the character fields and stopped at
`FavouriteMarks`; `Generation`/`Respawn` were forgotten. **Fixed** by adding both to the clone and a
field-family test (`CloneCharacter_CarriesTheLayerStampAndTheRespawnIdentity`).

**Major — the attribution line was not reproducible from the tracked batch readings.** The ticket had
claimed the `b2-after-*` probes as “the tracked readings”, but they are gitignored and the tracked record
does not cite them; the reviewed batch's own probes did not reproduce the applied coordinate. **Fixed** by
the evidence-status rewrite in the reading and Limits above: the line is an observation from the sandbox
log, not a tracked reading, and batch `20261021-j` is the tracked re-staging.

**Major — the post-fix evidence was written as done when it was planned.** **Fixed**: the section now says
“will be verified by”, and the first review's counts (754/6184) are separated from the second pass's
(754/6189 after the added tests).

**Minors.** The dead `todo/` reference in `done/layer-change-menu-round-trip.md` moved with the ticket. The
`DescentBodyPreservation` class and test comments no longer say the host drops every position (the clear
API is gone; the handback can carry the old-layer position and is refused whole first). The Limits rewrite
names the reconnect and the slow-report residual with the batch's real ~12 s window, and the archive's
`Respawn` flag wording is narrowed to “ordinary restore paths do not read it”. Source citations in this
record use path + quote, no line numbers.

**Verified by the second review.** 754/754 gates and 6189/6189 behaviour tests (57/57 in the affected
classes); the protocol page hashes match `git hash-object`; P3 = 26 anchors and the evidence file's count
= entries (1121); the debt counts; the lessons budget 65,438 ≤ 65,536; `scene-read.cs` is a real recipe;
batch `20261021-i`'s positions and the descent timings. The review's own quote: “`PlaceBody` … x 恒为 0，
取‘顶部第一个从空气进入地面的高度’。所以‘同一点’是原生规则”.

## Non-goals

- Not a spawn-selection feature (choosing where to enter): the ask is that the group stays together.
- Not a fix for the member-dropout family’s recovery (`done/layer-change-member-recovery.md`); this ticket
  owns where an in-world or re-entering member lands, not whether the invite reaches it.
