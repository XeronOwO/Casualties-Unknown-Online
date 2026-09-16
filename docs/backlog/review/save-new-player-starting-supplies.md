# S4.3 — A player the world has no character for is supplied as a NEW player

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: High
- Category: Persistence / save system
- Source: `docs/backlog/review/save-multiplayer-restore-and-backups.md` scope 1 (second half),
  acceptance row 3
- Related: decision 162 (transport-scoped identity; an absent player is a NEW character),
  decision 179 (the save system's player-visible surface: one notification plus the account),
  decision 180 (the supplies rule this stage implements), decision 165 (a continue never falls
  back to the native regenerate path), `review/save-guest-restore-claim-and-legacy-store-retirement.md`
  (S4.1, the claim), `review/save-restore-account-surface.md` (S4.2, the surface)

## The gap

The game hands out the run's `startingsupplies` exactly once per RUN, and only on the run's FIRST
layer: `WorldGeneration.WorldPlacePlayer` guards the grant with
`totalTraveled <= 0 && biomeOverride == OverrideSceneType.None && debugStartDepth == 0`
(`WorldGeneration.cs:1891`), and the two branches under it read
`WorldGeneration.GetRunSettingInt("startingsupplies")` (`:1899`) and create the setting's items into
the body's slots (`:1904-1912`). The gate is the run's POSITION, and a CUO continue reaches it exactly
like a native load does: the click lets the original `PreRunScript.LoadRun` run
(`PreRunScriptLoadRunPatch`), which loads the scene; CUO then blocks `SaveSystem.TryLoadGame` — the
only writer of a non-zero `totalTraveled` on a load — so the live field is whatever the generation
baseline carries, and generation proceeds through `GenerateWorld` → `WorldPlacePlayer`. So:

- a run still on its **starting layer** (`totalTraveled == 0`, `debugStartDepth == 0`, no override)
  gets the supplies from the game itself, whether it was started here or restored from an archive;
- a **mid-run join** or a **restored run past its starting layer** (`totalTraveled > 0`) fails the
  same test, so a player who was not in the package joins a world where nobody has supplies for them
  and the restored characters carry only what they had.

S4.1 made the identity half of the acceptance row work (the claim: a present player gets their
character, an absent one is a new character — decision 162). The new character itself was the gap:
nothing gave them the run's starting supplies, and nothing told them what they had (or had not)
received.

The first version of this stage believed the opposite of the first bullet — that a restored run's
starting layer was NOT covered by the game's grant, and added a Runtime fact
(`IWorldSaveControl.RestoredGeneration`) to say so. The adversarial review broke it: on that layer the
game's grant DOES run, so the extra clause turned `AlreadyOwned` into `Granted` and handed a second
set of items to a body whose slots the native grant had just filled — `Body.PickUpItem` refuses an
occupied slot without a word, so all of them were left on the ground as world items the whole team
could see and take. The clause and the fact it needed are gone; the condition is now the game's own,
read off the generation baseline.

## What landed

- **`StartingSupplyPolicy`** (GameAdapter/Character) is the whole decision as a pure function: the
  game's own setting table (none / light / full, with the game's own content ids and slot numbers) and
  the game's own first-layer condition, mirrored clause for clause off the boundary capture
  (`totalTraveled <= 0 && biomeOverride == None && debugStartDepth == 0 && !loadedRun`). It returns a
  `Decision` with a `Reason` (`Granted`, `Disabled`, `AlreadyOwned`, `CharacterRestored`,
  `NoBaseline`), the setting in the game's words, and the plan (content id + slot per item). Pure, so
  every branch the live game reaches is machine-verified without a Unity scene — and deliberately
  WITHOUT any "is this a restored world" input: no Runtime fact may enter a condition the game itself
  evaluates at `WorldPlacePlayer`.
- **`StartingSupplyGrantTracker`** is the once-per-body rule. Per BODY, not per session: a layer
  descent keeps the same body (`RegenerateWorld` never reloads the scene), so a player who descends
  still HAS their character and is not a new player; a death or a reconnect destroys the body, and
  the next one is judged on its own entry. Bodies are keyed by REFERENCE identity — Unity objects
  overload `Equals` to mean "destroyed", so an equality-keyed set would drop a live body out of the
  record the moment its backing object changed state.
- **`StartingSupplyCoordinator`** (GameAdapter/Character) is the pump and the account: the local
  body, generation finished, the run's baseline published, and no character restore queued for it.
  It publishes exactly one `StartingSupplyGrantReport` per judged body — including the bodies that
  got nothing — and the one exception is a body the world already has a character for (that restore
  is its own reported event from S4.2, and a second line for the same fact is the noise decision
  179's rules exist to remove).
- **`IStartingSupplyBehaviour`** (Abstractions) is the engine seam: the two acts only Unity can
  perform — create a content id's item at the local body, put it in a slot — with items and bodies
  as opaque handles. `GameStartingSupplyTarget` (GameAdapter/WorldGen) implements it over the game's
  OWN calls (`Utils.Create(id, body.transform.position, 0f)` and `Body.PickUpItem(item, slot, true)`,
  the native grant's own shape at `WorldGeneration.cs:1896-1912`), so a player supplied here and a
  player supplied by the game end up with the same objects in the same slots. The seam is not
  decoration: the CLR binds a method body's Unity InternalCall members when it JITs the method, so a
  coordinator that named `Utils.Create` or `Body.transform` could never be constructed in the test
  host at all — with the seam, the whole decision path is driven by tests.
- **There is no new Runtime fact, deliberately.** The first version added
  `IWorldSaveControl.RestoredGeneration` (true from an applied `TryContinue`) to argue that the game's
  grant did not cover a restored generation. It was wrong and it is deleted: the condition that
  decides the game's own grant is evaluated inside `WorldPlacePlayer` against the live fields, so a
  Runtime flag can only make the two answers disagree — and it did, in the one place the divergence
  costs the player items. `RestoredGeneration` no longer exists on `IWorldSaveControl`,
  `WorldSaveService` or `WorldRestoreApplier`.
- **The account reaches the player through the surface S4.2 established** (decision 179): a new
  narrow port (`IStartingSupplyPublisher` for the adapter, `IStartingSupplyControl` for the surfaces,
  `StartingSupplyAudit` as the production broadcast point) and one console line — the same words the
  log carries, so a player's screen and the log can be compared without translating. Detail is not
  needed behind it (the line names its items), and an incomplete grant is the one failure shape: it
  names what stayed on the ground and is announced as an error rather than a success — including the
  worst case, where NOTHING landed, whose line says so instead of opening with "given".
- **The decision reads the SAME restore queue the character domain applies from.** S4.2's handoff
  named the hazard: the restore's first pass wipes the body's slots a frame later, so a grant that
  races it would be created, announced and then destroyed. The queue is therefore injected into
  `CharacterDataSync` rather than created inside it (one queue, two readers), and the check is taken
  at the GRANT moment rather than sampled when the body appeared — a restore that lands in between is
  exactly the event that must cancel the grant. The review found the original suite only APPEARED to
  prove this (both of its rows queued the restore before the first pump, so an entry-sampled
  implementation passed both); `Update_ARestoreThatLandsBetweenTheEntryAndTheGrant_CancelsIt` now
  drives the real interleaving and was recorded RED against a re-introduced entry sample.

## Structure review notes

- `WorldSaveService` was at 596 of the 600 aggregate lines before this stage and the new interface
  member pushed it over, so the stage split a responsibility rather than buying headroom:
  **`WorldCutDeferral`** now owns the armed cut's deferral deadline (`MaxFrames` moved with it) and
  its reset, which is the trigger-policy half of the frame-end seam — the seam S4.4's interval trigger
  grows next. The player-facing source name ("the live snapshot" / "backup `<file>`") moved onto
  `WorldLoadResult` as `SourceName`, where the load's own facts live. `WorldSaveService` ended at 594,
  and all five stale `MaxCutDeferralFrames` references (one of them a summary block the extraction had
  orphaned onto a field) were cleared with the rename.
- `docs/evidence/sync-coverage-evidence.json` and `sync-coverage-matrix.md` were repointed twice: the
  implementation moved 28 evidence lines (787 entries already correct) and the review fix moved 23
  more (792 already correct). The repoint is quote-driven — an entry whose quote is no longer in its
  file is reported and NOTHING is written — with the matrix's inline refs moved through the same map
  and the backtick/hyphen/pipe/colon counts compared before and after.
- `WorldCharacterBinder`'s Collect/Apply and their two pinned tests are untouched.

## Acceptance

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | A player who was not in the package joins a RESTORED world past its starting layer | Fresh character + the run's starting supplies, and one account line saying what they got | `StartingSupplyCoordinatorTests.Update_AnOmittedPlayerInARestoredWorld_IsSupplied` (a REAL continue through the production save service + a real archive on disk, then the coordinator's pump), `StartingSupplyPolicyTests.Decide_AnOmittedPlayerInARestoredWorldPastTheFirstLayer_IsGranted`, `CommandConsoleSaveTests.StartingSupplies_OfAGrantedNewPlayer_IsOneNotificationNamingTheItems` |
| 2 | A player joining a running world (its first layer already behind it) | The run's supplies, once | `StartingSupplyCoordinatorTests.Update_AMidRunJoinInAFreshRun_IsSupplied`, `StartingSupplyPolicyTests.Decide_AMidRunJoin_SuppliesThePlayer` |
| 3 | A player the world HAS a character for (a restore is queued) | Nothing is granted, and no second account is printed — including when the restore lands between the body's entry and the grant | `StartingSupplyCoordinatorTests.Update_AQueuedCharacterRestore_CancelsTheGrant`, `.Update_ARestoreThatLandsBetweenTheEntryAndTheGrant_CancelsIt` (red→green against an entry-sampled decision), `StartingSupplyPolicyTests.Decide_AQueuedRestore_WinsOverEverything` |
| 4 | A FRESH run's first layer | CUO grants nothing (the game's own grant is the one that ran) and says so | `StartingSupplyCoordinatorTests.Update_AFreshRun_ReportsAlreadyOwnedInsteadOfGranting`, `StartingSupplyPolicyTests.Decide_AFreshRun_TheGameAlreadySuppliedEveryone`, `.NativeGrantCovers_TheRunsFirstLayer_IsTrue`, `CommandConsoleSaveTests.StartingSupplies_OfAPlayerTheGameAlreadySupplied_SaysSoInsteadOfGranting` |
| 5 | A RESTORED world frozen on its starting layer | The game's own grant covers it exactly as it covers a fresh run: nothing is handed out a second time | `StartingSupplyCoordinatorTests.Update_ARestoredStartingLayer_DoesNotGrantASecondSet`, `StartingSupplyPolicyTests.NativeGrantCovers_ARestoredStartingLayer_IsStillTrue`, `.Decide_ARestoredStartingLayer_IsAlreadyOwned` |
| 6 | The pump runs many frames over one body / a layer descent | Supplied once per body, never once per frame | `StartingSupplyCoordinatorTests.Update_TheSameBodyIsJudgedOnce_HoweverManyFramesRun`, `StartingSupplyGrantTrackerTests.WasSupplied_TracksEachBodyOnItsOwn`, `.WasSupplied_TwoSeparateInstances_AreTwoBodies` |
| 7 | A new body after the old one is gone (death) / a new run / a session end | Judged on its own entry; a new run and a session end both forget the old bodies | `StartingSupplyCoordinatorTests.Update_ANewBodyAfterTheOldOneIsGone_IsJudgedOnItsOwnEntry`, `.Clear_ForgetsTheBodiesOfTheRunBeingLeft`, `StartingSupplyGrantTrackerTests.Clear_ForgetsEveryBody` |
| 8 | The run's setting is `none`, or the run carries no run settings at all (the tutorial) | Nothing is granted, and the player reads that the RUN decided it | `StartingSupplyCoordinatorTests.Update_ARunWithNoSupplies_ReportsDisabledAndGrantsNothing`, `StartingSupplyPolicyTests.Decide_ARunWithoutRunSettings_ReportsDisabledUnset`, `.Decide_ADisabledRunOnTheFirstLayer_IsDisabledNotAlreadyOwned`, `CommandConsoleSaveTests.StartingSupplies_OfARunWithNoSupplies_SaysTheRunDecidedIt` |
| 9 | The baseline is not published yet | Not a verdict: nothing is granted, nothing is reported, and a later frame still supplies the body | `StartingSupplyCoordinatorTests.Update_WithNoPublishedBaseline_JudgesNothingAndRetries`, `StartingSupplyPolicyTests.Decide_NoBaseline_IsNotAVerdict` |
| 10 | A slot the body cannot take / an id that produces no item | The item is NAMED as unplaced (and the console announces the partial grant as an error), never claimed | `StartingSupplyCoordinatorTests.Update_AnItemTheGameCannotPlace_IsNamedInsteadOfClaimed`, `.Update_AContentIdThatProducesNoItem_IsNamedInsteadOfClaimed`, `CommandConsoleSaveTests.StartingSupplies_OfAPartialGrant_IsAnnouncedAsAnErrorAndNamesWhatStayedOnTheGround` |
| 11 | The setting's items and slots | The game's own table, item for item and slot for slot | `StartingSupplyPolicyTests.PlanFor_Full_IsTheGamesFourItemsInTheGamesSlots`, `.PlanFor_Light_IsTheEmergencyLightInTheGamesSlot`, `.PlanFor_AnUnknownValue_GrantsNothing` |
| 12 | The account reaches the console in the production composition | The adapter's publish is rendered by the console, from the ONE audit instance the plugin registers | `WorldSaveCompositionTests.ProductionRoot_WiresTheStartingSuppliesAccountToTheConsole`, `.ProductionRoot_ResolvesTheStartingSupplyAuditForTheAdapter` |
| 13 | A run started from the debug console (`debugStartDepth != 0`) | Not the run's first layer: the game grants nothing, so CUO supplies | `StartingSupplyPolicyTests.NativeGrantCovers_ARunStartedAtADebugDepth_IsFalse` |
| 14 | The tutorial | Never supplied (the game skips its own grant for that override, and the run carries no settings) | `StartingSupplyPolicyTests.NativeGrantCovers_TheTutorial_IsFalse`, `.Decide_ARunWithoutRunSettings_ReportsDisabledUnset` |

## The independent adversarial review, and what happened to each finding

The review ran in a fresh context and produced 1 BLOCKER / 2 MAJOR / 3 MINOR / 3 NIT. Everything
below is fixed in this cycle; nothing is deferred.

- **BLOCKER — the reversed `restoredGeneration` clause** (its evidence was the native continue's own
  call chain: `PreRunScriptLoadRunPatch` lets `PreRunScript.LoadRun` run, which loads the scene;
  `SaveSystemTryLoadGamePatch` blocks the only writer of a non-zero `totalTraveled` on a load, so the
  live field is the archive's; generation therefore reaches `WorldPlacePlayer` with the game's own
  guard satisfied). The clause and the `RestoredGeneration` fact it needed are DELETED, and
  `NativeGrantCovers` is the game's condition verbatim — now including `debugStartDepth`, which the
  first version had dropped. Two tests pin the corrected behaviour, one at each level
  (`NativeGrantCovers_ARestoredStartingLayer_IsStillTrue`,
  `Update_ARestoredStartingLayer_DoesNotGrantASecondSet`); both fail against the old code.
- **MAJOR — the same root cause seen from the guest side**: the flag was only ever true on the host, so
  the two clients evaluated one rule from different inputs. That asymmetry is gone with the flag; the
  decision is now one input set (the shared generation baseline) on both sides.
- **MAJOR — acceptance row 3 was proven by a duplicate**. The two rows were line-for-line equivalent and
  both queued the restore BEFORE the first pump, so an implementation that sampled the queue at body
  entry passed both — the very behaviour the ticket claimed to have avoided. The duplicate is replaced
  by `Update_ARestoreThatLandsBetweenTheEntryAndTheGrant_CancelsIt`, which pumps once with no published
  baseline (a non-verdict, so the body stays unjudged), queues the restore, publishes the baseline and
  pumps again. Recorded RED by re-introducing the entry sample (`_entrySample ??= …`): the test reports
  `Granted` where nothing may be granted.
- **MINOR — a fully unplaced grant opened with "given"**: `Describe()` now has a case for "nothing
  landed" and the console announces it as an error; `CommandConsoleSaveTests` pins that the line does
  not contain the word "given", and `StartingSupplyGrantReport`'s own case is covered by the
  `emergencylight` row.
- **MINOR — the extraction left documentation garbage**: the orphaned summary block in
  `WorldSaveService` (it had been attached to a field) is gone, and all five stale
  `MaxCutDeferralFrames` references — one source, one architecture doc, two backlog tickets, one
  decision entry — were repointed to `WorldCutDeferral.MaxFrames`.
- **MINOR — `LoadedRun` must NOT be part of the condition**: the first fix kept it as a safety clause,
  argued as "a native load CUO lets through must not re-open the double grant". A second independent
  pass showed the argument is exactly INVERTED: the game's guard does not read `LoadedRun`, and a native
  load writes `totalTraveled` from the save, so a stored run frozen at its starting layer would still
  satisfy the position clauses and the game would still hand out — which an `!LoadedRun` clause reads as
  "not covered" and answers with a second set of items. It is deleted, and
  `NativeGrantCovers_AStoredRun_IsTrueBecauseTheGameDoesNotReadLoadedRun` pins the reasoning rather than
  a preference.
- **MINOR — `DebugStartDepth` cannot reach the guest**: the first fix carried it on the baseline and
  wrote it back on the guest, and the ticket claimed "both sides reach the same verdict". False: the
  kernel baseline (`WorldRunStateMapper.ToWorldStartParams`) does not carry the field, so a guest reads
  the default 0 while the host reads the live field. The write-back is gone, the field's documentation
  says it is the HOST's clause, and the reason it is harmless is now written down where a reader will
  find it: game code declares `debugStartDepth` (`WorldGeneration.cs:4258`) and reads it three times
  (`:247`, `:257`, `:1891`) but NEVER writes it, so the clause cannot currently fire on either side. It
  is mirrored (and read, not assumed) so that the verdict follows the game if that ever changes.
- **MINOR — a comment still described the refuted model**: `RunSaveCoordinator`'s "the world hands out no
  starting supplies when a run is being continued" was the old belief. Corrected in place, with the
  reason the restored character still does not keep them (the restore's first pass wipes the body).
- **MINOR / NIT — coverage and lifetime**: the `AlreadyOwned`, `Disabled` and fully-unplaced console
  branches and their line kinds are now asserted, the granted line's POSITIVE wording is asserted too
  (the earlier row only pinned the negative), the adapter-facing publisher registration is pinned in the
  production root, and the tracker's `Clear` has a mechanism test. The session-end call itself is
  belt-and-braces — `WorldService` nulls the world params on `SessionEnded` (subscribed before
  `RunCoordinator`), so the next pump already answers `NoBaseline` — and this ticket does not claim a
  test drives it.
- **NIT — the coordinator suite never ran as a guest**: left as it is, deliberately. No host ever
  supplies a body it is not driving, and the in-process role is what the pump gates on; the guest
  asymmetry the original finding hinted at was the deleted flag. It is recorded here rather than papered
  over with a test that would assert the fake.
- **NIT — test naming vs discriminating power**: `NativeGrantCovers_TheRunsFirstLayer_IsTrue` and
  `NativeGrantCovers_ARestoredStartingLayer_IsStillTrue` take the same input, so the pair is one row of
  discriminating power; the real guard for the BLOCKER is the coordinator-level
  `Update_ARestoredStartingLayer_DoesNotGrantASecondSet`. Kept as documentation of the corrected
  reasoning, not as a second net.

### Pre-existing hygiene the same pass reported (NOT introduced here, not fixed here)

- `docs/evidence/sync-coverage-evidence.json`'s `count` field says 807 while `entries` holds 815 (the
  pre-fix file says the same), and three entry keys are duplicated verbatim — the duplicates are
  legitimate repeated references, the header is simply stale.
- ~35 of the matrix's ~300 inline anchors are no longer referenced by any evidence entry, and the W1
  row's bare anchor points at the wrong line inside `WorldParamsService` (the gate verifies anchored
  references, not bare ones). Both predate this stage.
These belong to the sync-coverage matrix's own upkeep; this stage's repoints are clean (see below) and
touching the matrix's structure now would mix an unrelated repair into a save-system delivery.

## The second independent pass, on the fixes themselves

A fresh verifier was given the fixes and the review's findings (not the implementation reasoning). Its
verdict: five findings genuinely fixed, two partially, no "over-fixed", no new BLOCKER or MAJOR. It
independently re-derived the BLOCKER's game-side chain, reproduced the acceptance-row-3 red in a scratch
copy outside the repository (the new test is the ONLY one of fourteen that fails against an entry-sampled
decision), and re-verified every evidence reference with its own checker (815/815 in the working tree,
815/815 for the pre-fix file against `78ba9bd4^`'s sources, no entry lost or added, 23 moved — matching
the commit's own claim). Its own findings are the three MINORs above (`LoadedRun`'s inverted rationale,
`DebugStartDepth`'s one-sided reach, the stale `RunSaveCoordinator` comment), all fixed in this cycle,
plus coverage notes and the pre-existing matrix hygiene recorded below.

## Verification limits

Machine-verified: the whole decision (every `Reason`, the setting table, the native-coverage clauses
including `debugStartDepth`, the entry-point/verdict precedence), the once-per-body rule including
reference-vs-equality identity and the session-end reset, the coordinator's pump against a REAL
composition root — a real world repository on disk, a real cut, a real `TryContinue` — the console's
rendering of all three dispositions and all four line shapes, and the production wiring end to end.
Also verified: the whole existing suite still passes, and the gates (including the architecture shape
gate this stage had to satisfy by splitting, and the sync-coverage evidence gate it repointed twice).

NOT machine-verified, and NOT claimed here: the Unity half. `GameStartingSupplyTarget.Create` /
`TryPlace` compile against the game's own `Utils.Create`, `Body.PickUpItem`, `PlayerCamera` and
`InventorySlot`, but no test can execute them in this host (the CLR binds those members when it JITs
the method — the method body cannot be constructed at all). Their logic is the native grant's own
calls and arguments, verified by reading `WorldGeneration.cs:1896-1912` and `Body.cs:1388-1410`, and
the guards they add are for the two silent-failure modes the game has (`PickUpItem` refuses a
non-empty or non-pickup-able slot without a word).

The BLOCKER's own last mile is in the same category, and it is the FIRST thing the user's pass should
look at: that the game's own grant really does run on a CONTINUED world still frozen on its starting
layer. The call chain is read off the decompiled sources and four independent statements inside this
repository agree with it, but the only proof is a run: continue a layer-0 archive whose character the
local player cannot claim and check that no second set of items appears at the body's feet. What else
needs the USER's dual-client pass:

- a host + guest where the guest was NOT in the package: the guest must enter the restored world
  with the run's supplies in the same slots a fresh run gives them, and the console must show one
  line naming them;
- a late joiner into a running world: same expectation;
- a guest who WAS in the package: nothing granted, no line, and the restored character's inventory
  intact (the regression the S4.2 handoff warned about);
- a run configured with `startingsupplies = none`: the supplies line says the run decided it, and
  nobody gets anything.
