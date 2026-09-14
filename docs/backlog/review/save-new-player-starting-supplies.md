# S4.3 — A player the world has no character for is supplied as a NEW player

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: High
- Category: Persistence / save system
- Source: `docs/backlog/in-progress/save-multiplayer-restore-and-backups.md` scope 1 (second half),
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
the body's slots (`:1904-1912`). Every other way into the world therefore gets nothing:

- a **restored** world generates the layer being entered (`RegenerateWorld` → `Clear()` +
  `InstantiateWorld(true)`), and the restored run's own travel/depth are the archive's — so a player
  who was not in the package joins a world where nobody has supplies, and the restored characters
  carry only what they had;
- a **mid-run join** (`totalTraveled > 0`) fails the same test, so a late joiner has an empty
  backpack next to players carrying a run's worth of gear.

S4.1 made the identity half of the acceptance row work (the claim: a present player gets their
character, an absent one is a new character — decision 162). The new character itself was the gap:
nothing gave them the run's starting supplies, and nothing told them what they had (or had not)
received.

The one thing the game cannot help with is telling a restored run from a fresh one on the first
layer: a restored run frozen on its starting layer has the same `totalTraveled == 0` and the same
`biomeOverride == None`, so the game's own test answers "the supplies were already handed out" for a
world whose supplies were never handed out by that path. That is the fact S4.3 had to add.

## What landed

- **`StartingSupplyPolicy`** (GameAdapter/Character) is the whole decision as a pure function: the
  game's own setting table (none / light / full, with the game's own content ids and slot numbers),
  the game's own first-layer condition, and one clause the game cannot make — a generation that came
  from an archive is NOT covered by the game's grant. It returns a `Decision` with a `Reason`
  (`Granted`, `Disabled`, `AlreadyOwned`, `CharacterRestored`, `NoBaseline`), the setting in the
  game's words, and the plan (content id + slot per item). Pure, so every branch the live game
  reaches is machine-verified without a Unity scene.
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
- **`IWorldSaveControl.RestoredGeneration`** is the one Runtime fact the decision needs: true from an
  applied `TryContinue` until a run this client owns takes over (`TryBeginRun`) or the attempt is
  abandoned. `WorldRestoreApplier` OWNS it — a restore is what produces it and the applier owns every
  point it changes, including the release at the start of a superseding attempt — and
  `WorldSaveService` relays it.
- **The account reaches the player through the surface S4.2 established** (decision 179): a new
  narrow port (`IStartingSupplyPublisher` for the adapter, `IStartingSupplyControl` for the surfaces,
  `StartingSupplyAudit` as the production broadcast point) and one console line — the same words the
  log carries, so a player's screen and the log can be compared without translating. Detail is not
  needed behind it (the line names its items), and an incomplete grant is the one failure shape: it
  names what stayed on the ground and is announced as an error rather than a success.
- **The decision reads the SAME restore queue the character domain applies from.** S4.2's handoff
  named the hazard: the restore's first pass wipes the body's slots a frame later, so a grant that
  races it would be created, announced and then destroyed. The queue is therefore injected into
  `CharacterDataSync` rather than created inside it (one queue, two readers), and the check is taken
  at the GRANT moment rather than sampled when the body appeared — a restore that lands in between is
  exactly the event that must cancel the grant.

## Structure review notes

- `WorldSaveService` was at 596 of the 600 aggregate lines before this stage and the new interface
  member pushed it over, so the stage split a responsibility rather than buying headroom:
  **`WorldCutDeferral`** now owns the armed cut's deferral deadline (`MaxCutDeferralFrames` moved with
  it) and its reset, which is the trigger-policy half of the frame-end seam — the seam S4.4's interval
  trigger grows next. The archive's restored-generation claim moved to the applier that produces it,
  and the player-facing source name ("the live snapshot" / "backup `<file>`") moved onto
  `WorldLoadResult` as `SourceName`, where the load's own facts live. `WorldSaveService` is back to
  600/600 and `WorldRestoreApplier` is 455.
- `WorldCharacterBinder`'s Collect/Apply and their two pinned tests are untouched.
- `docs/evidence/sync-coverage-evidence.json` and `sync-coverage-matrix.md` were repointed: this
  stage's edits moved 28 evidence lines (787 entries were already correct), and the repoint was
  quote-driven — an entry whose quote is no longer in its file is reported rather than guessed at —
  with the matrix's inline refs moved through the same map and the backtick/hyphen/pipe/colon counts
  compared before and after.

## Acceptance

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | A guest who was not in the package joins a restored world | Fresh character + the run's starting supplies, and one account line saying what they got | `StartingSupplyCoordinatorTests.Update_AnOmittedPlayerInARestoredWorld_IsSupplied` (a REAL continue through the production save service + a real archive on disk, then the coordinator's pump), `CommandConsoleSaveTests.StartingSupplies_OfAGrantedNewPlayer_IsOneNotificationNamingTheItems` |
| 2 | A player joining a running world (its first layer already behind it) | The run's supplies, once | `StartingSupplyCoordinatorTests.Update_AMidRunJoinInAFreshRun_IsSupplied`, `StartingSupplyPolicyTests.Decide_AMidRunJoin_SuppliesThePlayer` |
| 3 | A player the world HAS a character for (a restore is queued) | Nothing is granted, and no second account is printed | `StartingSupplyCoordinatorTests.Update_AQueuedCharacterRestore_CancelsTheGrant`, `.Update_ARestoreThatLandsBeforeTheGrant_CancelsIt`, `StartingSupplyPolicyTests.Decide_AQueuedRestore_WinsOverEverything` |
| 4 | A FRESH run's first layer | CUO grants nothing (the game's own grant is the one that ran) and says so | `StartingSupplyCoordinatorTests.Update_AFreshRun_ReportsAlreadyOwnedInsteadOfGranting`, `StartingSupplyPolicyTests.Decide_AFreshRun_TheGameAlreadySuppliedEveryone`, `.NativeGrantCovers_TheRunsFirstLayer_IsTrue` |
| 5 | A restored world frozen on its starting layer | Not mistaken for a fresh run: the players the archive omitted ARE supplied | `StartingSupplyPolicyTests.NativeGrantCovers_ARestoredGeneration_IsFalse`, `.Decide_ARestoredWorld_SuppliesThePlayerTheArchiveOmitted` |
| 6 | The pump runs many frames over one body / a layer descent | Supplied once per body, never once per frame | `StartingSupplyCoordinatorTests.Update_TheSameBodyIsJudgedOnce_HoweverManyFramesRun`, `StartingSupplyGrantTrackerTests.WasSupplied_TracksEachBodyOnItsOwn`, `.WasSupplied_TwoSeparateInstances_AreTwoBodies` |
| 7 | A new body after the old one is gone (death) / a new run | Judged on its own entry; a new run forgets the old bodies | `StartingSupplyCoordinatorTests.Update_ANewBodyAfterTheOldOneIsGone_IsJudgedOnItsOwnEntry`, `.Clear_ForgetsTheBodiesOfTheRunBeingLeft`, `StartingSupplyGrantTrackerTests.Clear_ForgetsEveryBody` |
| 8 | The run's setting is `none`, or the run carries no run settings at all (the tutorial) | Nothing is granted, and the player reads that the RUN decided it | `StartingSupplyCoordinatorTests.Update_ARunWithNoSupplies_ReportsDisabledAndGrantsNothing`, `StartingSupplyPolicyTests.Decide_ARunWithoutRunSettings_ReportsDisabledUnset`, `.Decide_ADisabledRunOnTheFirstLayer_IsDisabledNotAlreadyOwned`, `CommandConsoleSaveTests.StartingSupplies_OfARunWithNoSupplies_SaysTheRunDecidedIt` |
| 9 | The baseline is not published yet | Not a verdict: nothing is granted, nothing is reported, and a later frame still supplies the body | `StartingSupplyCoordinatorTests.Update_WithNoPublishedBaseline_JudgesNothingAndRetries`, `StartingSupplyPolicyTests.Decide_NoBaseline_IsNotAVerdict` |
| 10 | A slot the body cannot take / an id that produces no item | The item is NAMED as unplaced (and the console announces the partial grant as an error), never claimed | `StartingSupplyCoordinatorTests.Update_AnItemTheGameCannotPlace_IsNamedInsteadOfClaimed`, `.Update_AContentIdThatProducesNoItem_IsNamedInsteadOfClaimed`, `CommandConsoleSaveTests.StartingSupplies_OfAPartialGrant_IsAnnouncedAsAnErrorAndNamesWhatStayedOnTheGround` |
| 11 | The setting's items and slots | The game's own table, item for item and slot for slot | `StartingSupplyPolicyTests.PlanFor_Full_IsTheGamesFourItemsInTheGamesSlots`, `.PlanFor_Light_IsTheEmergencyLightInTheGamesSlot`, `.PlanFor_AnUnknownValue_GrantsNothing` |
| 12 | The account reaches the console in the production composition | The adapter's publish is rendered by the console, from the ONE audit instance the plugin registers | `WorldSaveCompositionTests.ProductionRoot_WiresTheStartingSuppliesAccountToTheConsole` |
| 13 | The archive's claim on a generation | True only between an applied continue and the next run / an abandonment | `WorldSaveContinueTests.RestoredGeneration_IsWhatTellsARestoredWorldFromAFreshOne`, `.RestoredGeneration_OfARepeatedContinueThatIsRefused_DoesNotSurvive`, `.RestoredGeneration_OfAnAbandonedAttempt_IsReleased` |
| 14 | The tutorial | Never supplied (the game skips its own grant for that override, and the run carries no settings) | `StartingSupplyPolicyTests.NativeGrantCovers_TheTutorial_IsFalse`, `.Decide_ARunWithoutRunSettings_ReportsDisabledUnset` |

## Verification limits

Machine-verified: the whole decision (every `Reason`, the setting table, the native-coverage clauses,
the entry-point/verdict precedence), the once-per-body rule including reference-vs-equality identity,
the coordinator's pump against a REAL composition root — a real world repository on disk, a real cut,
a real `TryContinue` (so `RestoredGeneration` is a produced fact, not a hand-set flag) — the
Runtime's flag lifecycle, the console's rendering of all three dispositions, and the production
wiring end to end. Also verified: the whole existing suite still passes, and the gates (including the
architecture shape gate this stage had to satisfy by splitting, and the sync-coverage evidence gate
this stage repointed).

NOT machine-verified, and NOT claimed here: the Unity half. `GameStartingSupplyTarget.Create` /
`TryPlace` compile against the game's own `Utils.Create`, `Body.PickUpItem`, `PlayerCamera` and
`InventorySlot`, but no test can execute them in this host (the CLR binds those members when it JITs
the method — the method body cannot be constructed at all). Their logic is the native grant's own
calls and arguments, verified by reading `WorldGeneration.cs:1896-1912` and `Body.cs:1388-1410`, and
the guards they add are for the two silent-failure modes the game has (`PickUpItem` refuses a
non-empty or non-pickup-able slot without a word). What still needs the USER's dual-client pass:

- a host + guest where the guest was NOT in the package: the guest must enter the restored world
  with the run's supplies in the same slots a fresh run gives them, and the console must show one
  line naming them;
- a late joiner into a running world: same expectation;
- a guest who WAS in the package: nothing granted, no line, and the restored character's inventory
  intact (the regression the S4.2 handoff warned about);
- a run configured with `startingsupplies = none`: the supplies line says the run decided it, and
  nobody gets anything.
