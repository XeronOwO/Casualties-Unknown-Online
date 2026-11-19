# Saves and recovery: putting a run back

What a save holds, how it is taken, and what a restore may and may not do.

## What the player does

Cuts a save mid-run, continues it later with the same party, and expects the world, the items and every player's body to
come back where they were when the cut was taken.

## What a save holds

- The kernel checkpoint: every domain's authoritative state at the cut.
- The run baseline: what the run was generated from.
- Each present member's character data, keyed by the identity that player presents with.
- The world diff: what changed in the world since the baseline.
- The in-flight classes: each class of transient state with its declared verdict — captured, resolved before the save, or
  dropped.

## Rules

- The save is a repository of worlds, not a list of slots: one archive per world, keyed by an immutable identity; the
  display name may change without touching the archive.
- CUO's save is independent of the game's own save file: it never reads or writes it.
- A cut is armed by the event that asks for it and taken by the host's pump at one seam.
- **A deliberate leave IS a cut, and the two are one operation.** `GameAdapter/Run/SaveCutSeam.cs`: "a deliberate 'leave the
  world' always runs outside the pump — a session teardown event inside a Steam/UI callback, or the game's own
  `PlayerCamera.ToMainMenu` — so the intent is recorded by `RunMenuReturnCoordinator` and both the leave and the scene load
  happen here", and "the deliberate return is itself a mid-run cut now (the world is still fully alive at that moment), so the
  cut and the leave are one operation — if the transient policy defers the cut, the leave waits a frame instead of walking out
  of a world whose in-flight state was never captured".
- **The scene-load interception and the frame-end seam must agree.** `Runtime/Session/RunMenuReturnPolicy.cs` is "pure
  host/guest/solo decisions for the deliberate menu return", kept L0-testable "so neither the save-authority rule nor the
  'would the seam act' precondition can be re-derived differently in the adapter — the scene-load interception and the frame-end
  seam must agree, or a suppressed scene load would never be replayed".
- **A checkpoint travels as chunks, and its schema has its own version.**
  `src/CasualtiesUnknownOnline.Protocol/Versioning/ProtocolConstants.cs` keeps the envelope layout and the checkpoint schema as
  separate numbers (`EnvelopeVersion` and `CheckpointSchemaVersion`) and bounds a checkpoint's transport:
  `CheckpointChunkItemCount = 256` items per chunk and a maximum number of chunks per restore — so a large world is carried in
  a deterministic number of pieces rather than one frame.
- **The native "Save and exit" writes `SaveSystem.SaveGame()`; CUO's does not.** `WorldGeneration.SaveAndExit()`
  (`WorldGeneration.cs:1024-1033`) sets `forceWalk = false`, raises `deepestlayer`, calls `IncreaseDepthByLayer()`, writes the
  game's own save and returns to the menu. CUO keeps the player-facing action and substitutes its own cut, which is the same
  independence decision 165 states — and the reason the acceptance reading for that panel says the save half "leaves `save.sv`
  untouched".
- **Returning to the menu is nothing but a scene load, which is why it has to be intercepted.** `PlayerCamera.ToMainMenu()`
  (`PlayerCamera.cs:613-616`) is `SceneManager.LoadScene("PreGen")` and no more — there is no save, no teardown and no callback
  a caller could hang work on. CUO's leave path exists because of that: the intent is recorded before the scene load and both
  the cut and the load happen at the frame-end seam, "or a suppressed scene load would never be replayed".
- **The end-of-layer panel's "Save and exit" takes a CUO cut, not the native save.** The accepted reading is that "the save
  half takes a CUO cut and leaves `save.sv` untouched", which is the same independence the save rules state from the other
  direction. The panel itself follows the living share of the party: it appears for a dead host and is gone for a member.
- **In-flight state gets one explicit verdict per class, and a cut may WAIT for it.**
  `Runtime/Session/Persistence/WorldTransientPolicy.cs` splits the classes three ways: `ResolveBeforeSave` for state that
  exists only because an operation spans frames ("a break's drops do not exist yet in the frame the block broke, a drop's throw
  velocity lands a frame later") — "capturing them is impossible … and dropping them would lose the items, so the cut WAITS —
  the armed request stays armed and the frame-end seam retries, bounded by `WorldCutDeferral.MaxFrames`"; `DropWithLog` for
  state whose effect is already in the kernel or that the restored world re-derives ("the run clock, the earthquake timers,
  item velocity — the kernel owns positions only"), where capturing would be "a second source of truth", so a cut NAMES them
  and goes on; and `Capture` for the world facts a cut carries as data ("the decided native values and the radiation line ride
  `world-transients.json`").
- **Character identity is transport-scoped, and a member, not the "in world" flag, decides presence.**
  `WorldCharacterBinder.cs`: "Steam keys are `steam-<steamId64>`, IP-direct keys are `name-<sanitized display name>`, and the
  two spaces never silently collide"; "membership, not the 'in world' flag, decides who is present, and a stored key nobody
  claims is not an error (that player joins as a NEW character)".
- **Two IP-direct players may share a display name; the client identity keeps their characters apart.** A shared
  `name-<name>` key is not an error to police: each client carries a persistent `[IpDirect] ClientIdentity` (generated on its
  first IP-direct start, carried in the handshake), and `PlayerKeyResolution.EffectiveKeys` folds it into the key as
  `-c<digest8>` only while two present peers share the base name. A unique name keeps the bare key, so saves written before
  the identity existed still load; the same effective keys answer a `Claim`, so the member that follows a continue gets its
  own character back (decision 272; the 2026-11-19 reading is on
  [the ticket](../../backlog/done/1.0-ip-direct-duplicate-names-mix-characters.md)).
- **A continue restores the run's OWN world, not a stale one.** The recorded defect was "A Continue after a run opens a stale
  world (the layer-mod baseline then diverges)"; the fix moves the pointer to the run's own world at the first committed cut,
  and the acceptance reading is that all three clients "generated from the same decision entry state … and applied the same
  modifier (index 5) with zero `[LayerMod] baseline divergence`".
- A restore is repair mode with minimal loss, per entry: an entry whose content no longer exists is skipped with a
  recorded reason, and one broken entry never fails the whole cut.
- A member's stored character is applied by the client that can apply it; on a continue, the keys present peers claim are
  bound to those peers, and an unclaimed key waits rather than being handed to someone else.
- **A member pulled back into a restored world receives its character BEFORE the entry instruction.** The continue binds the
  archive's characters to the present peers (`WorldRestoreApplier.cs:342-343`), and the entry instruction is what starts a
  following member's regeneration; that member's fresh body reports the starting supplies within a second, and the report
  replaces the bound row (`SaveReportedCharacterData` -> `SaveCharacterData`). The invitation carries both: the start-gate
  pump hands each still-outside member its stored character once per run entry and sends that tick's `WorldJoin` right after
  it, and a mid-generation handshake's entry group does the same. The receiver keeps a peer hand-over across the follow and
  drops it only when this client starts its own run (`LocalCharacterRestoreQueue`), so a follower's
  `RunSaveCoordinator.BeginRun` cancels its own-run restore only. The 2026-11-17 readings: the following member's fresh body
  carried the four starting supplies and the later reconnect adoption handed them back, and the first hand-over attempt sent
  nothing because the host was not yet in its own world when the invitation went out; [the
  ticket](../../backlog/done/1.0-followed-continue-keeps-no-character.md) owns the fix.
- **A consumed one-shot trap the restored layer does not have is satisfied, not refused.** A mine, turret or unstable
  crystal absent at its recorded cell cannot be armed, so the absence IS the consumed state: the restore applies the row and
  replays the explosion as presentation only. A state row is different — it needs the entity, and a missing one stays a
  refusal. The 2026-11-17 reading: a mine staged after the world was generated never returns in the regenerated layer, and
  refusing its consumption row reported `restore INCOMPLETE` for a world that was correct (1 refused row — that mine's own
  consumption — while the two destroyed-building rows applied). [The
  ticket](../../backlog/done/1.0-save-after-building-damage-loses-entity.md) owns the fix.
- **A cut names only the buildings the live world still has.** The kernel's health fact for a building is the value the
  blast reported when it landed, and the game can fell that building afterwards through its own support loss, which reports
  nothing — so the kernel keeps a positive health for an entity that no longer stands. The cut therefore reads the live world
  at the cut instant (`CutBuildingHealthReconcile`, through `INativeWorldFacts.LiveBuildingHealthAt`, the same point lookup
  the restore applies a health row with): a building that is gone is recorded destroyed, and a health the world has moved on
  from is written as the live value. A cut taken with no live world cannot answer, and keeps the kernel's facts untouched —
  which is why "can the world answer" is a separate question from the answer itself. The 2026-11-17 reading: the cut named
  health 1437.9 at cell (5,492) whose building the blast had already felled; the continue refused that row and reported the
  world incomplete. [The ticket](../../backlog/done/1.0-save-after-building-damage-loses-entity.md) owns the fix.
- **A health row for a building the regenerated layer does not have is recorded destroyed when its neighbourhood does.** The
  fell can land after the cut, so the archive holds a positive health for a building the next continue's layer does not
  contain — the block state the layer derives it from is the same state that removed its support. The restore therefore asks
  two questions: does a building stand at the row (3 m, inactive chunks included)? If not, does the layer have that AREA at
  all (another building within 25 m)? An area that exists without the row's building means the fell is the truth and the row
  is applied as destroyed; an area that does not exist means the layer has not produced it yet, and the row stays refused, so
  a real loss is still a red. The 2026-11-18 measurement: one refusal in five fresh focused runs, the nearest other building
  9.3 m away. [The ticket](../../backlog/done/1.0-cut-outlives-a-felled-building.md) owns the reading.
- **A refused restore child falls to the ground, not into nothing.** The accepted record is that the refused child is dropped into the world and reported, read across all three rows — the identity is kept because the item exists in the world rather than being destroyed.
- **An in-flight restore account releases its arms.** A restore that finishes its account must release the entity and native arms it held rather than leaking them: the family's record ran its four named classes 66/66 with every row re-read as passed.
- **A restored native set waits for every object it names, and the restored type survives the object's own start.** The saved set is the decided set: every geyser whose own `Start` has run, active or not; a geyser that has not started has no decided type to save. The generated geysers are instantiated during generation, after the world-entry seam can already see a non-generating world. The 2026-11-16 save-continue reading: the archive carried 110 rows, the live query saw 64 at the seam, and the 5 s wait expired with the remaining 46 reported refused. The live query includes inactive geysers; whether the 46 were inactive, not yet instantiated, or both is not settled by that reading, and the post-fix focused run is the evidence: it applied 104 of 104 rows with 0 refused, and the type table matched before the save, after Continue and after every not-started geyser's own `Start`. The replay holds the pending cut while the live geyser set is still GROWING (`GeyserFillWait`): it is released once the count has stood still for 3 s, and never held past 30 s — a count that flatlines is a real loss and is reported as one. The 2026-11-17 reading: one world stood at 94 of 114 objects five seconds past the seam and reached its full set seconds later, so a fixed 5 s window refused rows the world was about to have (113 of 114 reported refused). The adapter remembers each restored type and re-applies it when the geyser's own `Start` rolls the public random type (`reversing/release-1.0.2/Assembly-CSharp/GeyserScript.cs:8-20`: the roll `Random.value > 0.75f ? 2 : 1`, and the `isLava` override to 6), so a geyser that starts after the restore keeps the saved type. The type is matched ONE saved row to ONE live geyser, shortest pair first, inside a 6 m radius: the 2026-11-17 reading had a row at (97.8, 167.2) answer a geyser at (97.8, 163.6) — 3.62 m away, the next live geyser 12.34 m farther — and another two geysers 1.35 m apart, so a live-side nearest match in a 3 m radius refused a row the world actually held (125 of 126 applied).
- A refused restore child keeps its identity where the design says it goes (the ground), rather than being dropped
  silently.
- **A cut records a KIND and a REASON, and only the kind is policy.** `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldCutKind.cs`
  names the three kinds (the layer boundary, an arbitrary moment inside a layer, the interval autosave) and
  `WorldCutReason.cs` says what the reason is for: "the reason is provenance, not policy: several reasons can produce the
  same kind of cut, and the restore path treats them identically".
- **The folder is the unit of the writer lease, not the process.** `WorldLease.cs` implements decision 164's single-writer
  rule per FOLDER, because two instances pointed at one world "would otherwise interleave the [transaction]": both would
  stage into the one `.staging/` and "both would rename `live/` aside, so the loser's snapshot is deleted by a commit it never
  saw. Nothing in the archive would say what happened; the player would simply find an older world".
- **A fallback promotes the good archive and keeps the refused one as evidence.** `WorldBackupPromotion.cs`: a restore that
  falls back "used to leave the world folder exactly as it was: the good archive was read in memory, and the refused snapshot
  stayed in `live/` until the next cut's transaction renamed it to `.previous/` and deleted it. The evidence of what went wrong
  was destroyed by the very next autosave." Now the good archive becomes live and the refused one is kept where nothing writes.
- **A load says which of three states it came from.** `WorldLoadState` is `Current` (the live snapshot read and passed the
  manifest gate), `RecoveredLive` ("the live snapshot read, but a crash leftover was repaired on the way in") or
  `BackupFallback` ("the live snapshot was unusable; a backup archive was opened instead"), and `ChecksumPolicy` is what makes
  the gate real: `Required` reports a mismatched digest as damaged, `None` records digests without enforcing them.
- **An absent native field is NAMED, never defaulted.** `GameAdapter/Character/CharacterDataCapture.cs`: a capture with no
  live scene port "captures a snapshot without them, which is exactly the state the restore path NAMES rather than defaults" —
  so a character restored from an archive that never held the native fields reports the absence instead of inventing values.

## Values the archive must carry

Three values have each been their own ticket, which is why they are stated here together: the layer time accounting (the timer is carried and restored, read on the fixed seam), the run clock base (a player who joins mid-run receives the archived base), and a new player's starting supplies (a player the world has no character for is supplied as a NEW player, with the grant announced before its use). Each of them is a value that silently disappeared before its ticket, so a restore that drops one is the same defect returning.

The player facts a cut carries are the DURABLE ones and only those: `src/CasualtiesUnknownOnline.GameState/Domains/Players/PlayerState.cs` holds alive/conscious, the carry relation, the limb and body latches, and the skills, while "high-frequency movement fields remain a stream" — and `PlayerSkillsState.cs` says why skills belong there: "they change through permanent progression or run rules (respawn keep/reset), so they belong to the player domain and ride checkpoint/wire/save like the other durable player facts".

## What travels

The checkpoint's typed state, the run baseline, each present member's character data, the world diff and the in-flight classes' verdicts; a restore delivers the same back to the clients that own each part.

## Refusals and recovery

| Condition | What happens |
|---|---|
| an entry's content no longer exists | the entry is skipped and the skip is reported |
| a stored key has no present owner | the entry waits for its owner |
| the archive's world identity no longer matches | the archive is not applied to the wrong world |
| a restore throws mid-way | the archive is left intact for a second attempt |

## Who judges

The host takes and holds the cut; a restore is applied by the host and delivered to the clients that own each part.

## What the player sees and hears

A cut produces no player-visible interruption; a continue puts each player back on their own body in the same world.

## Records

Decisions 162–170 (the save family), and the acceptance records of the continue/layer work in `../../backlog/`.

## Gaps

- The exact list of in-flight classes and their verdicts is declared by the transient policy; it belongs on this page
  once read from there.
- What a player sees when their own stored character cannot be applied is not recorded here yet.
