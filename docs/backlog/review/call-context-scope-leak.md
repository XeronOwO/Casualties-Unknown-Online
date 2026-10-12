# A game-side throw inside a scope-entering patched frame latches CallContext off

- Status: Review — the amplifier is fixed and verified at runtime against a deployed artifact (batch
  `20261012-a`, the missing stack produced there); the code side is done and what remains for it is an
  acceptance run's rows (row 4 needs both peers). Split out of
  `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` on 2026-10-12, when that ticket's content
  defect (stage 1) landed in `22ae381c` and its own *Required work* left this half as a decision. It is its own
  cycle because it touches call-identity architecture rather than content binding.
- Priority: High — a single game-side exception can end a client session. In batch `20261011-k` the client was
  unusable for 92 seconds and had to be restarted, and what kept breaking was not the game's own path but
  CUO's: every scope-entering patch in that process threw on its own account.
- Category: Runtime robustness / call identity
- Related: `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` (stage 1, the content defect whose
  throw was the trigger), `src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs` (the depth guard, the scope
  stack and now the release deadline), `src/CasualtiesUnknownOnline.GameAdapter/Patches/BurpSoundPatches.cs`
  (the measured leaking site), `docs/decisions/active.md` (the call-identity model this guard exists for)
- Source: batch `20261011-k`'s storm census — `f3-host-session1-storm-census.txt`, a re-derivation of the
  rotated host log, holds 136,445 lines / 15,706,048 bytes with **19,973** `InvalidOperationException:
  CallContext.Enter depth 16 exceeded — an Enter was never disposed (leaked scope)` lines, the first at
  22:27:20.779 and the last at 22:28:52.907 (92.128 s), alongside 2,174 `ItemInfo.HasTag` frames.
- Evidence: batch `20261012-a` (local artifacts under `.acceptance/20261012-a/`), probe
  `tools/acceptance/recipes/call-context-scope.cs`.

## The missing stack, produced (batch `20261012-a`)

`20261011-k` kept no artifact naming the frame that ENTERED a scope and never disposed it: a depth-exceeded
stack names the victim (a scope-entering prefix throwing on its own account), and the sample was one of those.
The probe closes that gap by reading the guard's own state instead of a stack:

| mode | what it does |
|---|---|
| `read` | reflects `CallContext._depth` and `Stack` — the origins the guard is holding right now |
| `break` | puts an item in the local body's hand slot and nulls that definition's derived tag array (`ItemInfo.actualTags`, the private field the game's own table pass fills) — the state `20261011-k`'s tagless fixture reached by declaring no tags |
| `restore` | puts the array back through the game's OWN `ItemInfo.SetTags()` |

Run 1 (unfixed tree, artifact `0.1.0+3b872de2…`, one physical-machine client, no lobby) measured, in order:

| when | what |
|---|---|
| 22:06:27.824 | the game's own `ArgumentNullException` starts (`ItemInfo.HasTag`) |
| 22:06:27.831 … | **16 stacks name `Body.DMD<Body::HandleVisuals>`** (`Body.DMD<Body::Update>` calls it) — one leak per frame |
| 22:06:28.024 | the first `depth 16 exceeded` — the latch, 0.2 s after the trigger |
| 22:06:28.024 → 22:07:02.860 | **11,608** refusals in 34.8 s: `PantSoundUpdatePatch.Prefix` 3,870 + `PlayerCameraHandleUnconsciousScreenPatch.Prefix` 3,869 + `BodyHandleGroundedStatePatch.Prefix` 3,869 — counted on the full log after the run; `a6-log-window-1.txt` is the window as of the mid-run read (1,777 refusals) |
| `read` after the break | `depth = 16`, `current = CharacterBurp`, `origins = CharacterBurp ×16` |
| 22:06:50.052 | the last game-side throw (the `restore` call) |
| ↳ after it | **4,458 more refusals** — the trigger was gone and the latch stayed; only a restart cleared it |

`Origin.CharacterBurp` has exactly one entry site, `BurpSoundPatches.BodyHandleVisualsBurpPatch`: its `Prefix`
enters the scope and its `Postfix` disposes it, and `Body.HandleVisuals` asks `HasTag("gun")` of the hand item
per frame (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`, the method declared at :3123, the call at
:3272). The mechanism the ticket could only read before is now observed — including the second half: once the
stack is full, `HandleVisuals` is not called at all (its own Prefix throws first), which is why the victim
census holds exactly the three prefixes that run before it.

## The fix: the release deadline belongs to the guard

**`CallContext` owns the deadline for every scope: the frame boundary.** `EndFrame()` releases every scope the
frame left open — `_depth = 0`, one epoch bump so a late `Dispose` of a released entry cannot pop somebody
else's — and answers ONE operator line naming what it recovered, or null when the frame ended clean. Reporting
is per leak EPISODE: while frames keep leaking (the measured shape) the release stays silent, and one clean
frame re-arms it, so a per-frame throw cannot flood the log. The adapter calls it as the last step of its
per-frame service pump (`ICuoService.Update`).

Reaching the bound inside ONE frame (real nesting is 2-3 levels) is now **refused, not thrown**: the refused
scope owns no entry, `IsWithin` does not see it, the count is reported, and the session keeps running. A throw
there is what turned one game-side exception into an unusable client.

Why the guard and not a per-site release: the sweep below found **26 sites** whose release rides a skipped
postfix, and a per-site release (a Harmony `Finalizer` per patch class) would fix only the shapes it can see —
not a Prefix that enters and then throws on its own account, not a patch added later. The deadline covers the
whole family, including those, and it is the one place that must not latch. What a per-site release would buy
is the window between the throw and the frame boundary, bounded by one frame and named under *Limits*.

Both halves are gated rather than trusted: `CallContextRecoveryTests` (the bound refuses; the boundary releases
and names; an episode is reported once; real nesting is untouched) and `CallContextFrameBoundaryGateTests`
(the adapter's pump closes the frame exactly once — delete that call and every behavioural case stays green
while the latch returns).

## The family sweep (required work 3)

Every site that opens a scope OUTSIDE a `using` — the release rides a path a throwing original skips, and every
one of them is now released by the frame boundary (which is why the fix is in the guard):

| # | site | wrapped call | origin |
|---|---|---|---|
| 1 | `BurpSoundPatches.BodyHandleVisualsBurpPatch` | `Body.HandleVisuals` | CharacterBurp ← measured |
| 2-4 | `PantSoundPatches` (Update / Bark / TryGrowl) | `PantSound.Update` / `Bark` / `TryGrowl` | CharacterVocalization / CharacterBark / CharacterGrowl |
| 5-8 | `BodyPatches` (Attack / TryExertSound / FootStep / HandleGroundedState) | `Body.Attack` / `TryExertSound` / `FootStep` / `HandleGroundedState` | CharacterAttack / CharacterExert / CharacterFootstep / CharacterLandingImpact |
| 9-13 | `BodyItemPatches` (SwapSlots / SwitchHands / ThrowItem / UseItem / UseItemInHand) | `Body.SwapSlots` / `SwitchHands` / `ThrowItem` / `UseItem` / `UseItemInHand` | InternalReorder ×2 / CharacterThrow / CharacterItemPlacement or CharacterItemUse ×2 |
| 14-15 | `CraftingPatches` (craft / combine) | `Recipe.TryMake` / `Body.CombineItems` | Craft (opened in `CraftingSync.OnCraftBegin` / `OnCombineBegin`, released by the matching end) |
| 16 | `BandageMinigameSoundPatches` | `BandageMinigame.PhysicsUpdate` | CharacterMedicalUse |
| 17 | `AmputationMinigameSoundPatch` | `AmputationMinigame.Update` | CharacterMedicalUse |
| 18 | `ShrapnelMinigameSoundPatch` | `ShrapnelMinigame.Update` | CharacterMedicalUse |
| 19 | `MedicalSoundPatches` | `PlayerCamera.ApplyWoundItem` | CharacterMedicalUse |
| 20 | `LockpingSoundPatches` | `LockpingMinigame.Update` | CharacterLockpickPain |
| 21 | `InventoryGestureSoundPatches` | `Body.CombineLiquids` | CharacterInventoryGesture |
| 22 | `WorldDrinkSoundPatches` | `FluidManager.DrinkLiquid` | CharacterWorldDrink |
| 23 | `PlayerCameraHandleUnconsciousScreenPatch` | `PlayerCamera.HandleUnconsciousScreen` | WorldTimeSleepLocal |
| 24 | `TutorialHandlerUpdatePatch` | `TutorialHandler.Update` | TutorialClawSpawn |
| 25 | `WorldGenerationDamageBlockPatch` | `WorldGeneration.DamageBlock` | DamageBlockOrigin |
| 26 | `BuildingEntityPatches` | `BuildingEntity.Update` | BuildingDeathDrop |

The other sites — the ~40 adapter handlers and appliers, `ScopedCoroutine`, `NativeLimbActionScope`,
`CraftingSync`'s `using`-scoped halves — release inside `using`/try-finally, so a throwing original releases
them. `ScopedCoroutine` enters around one `MoveNext` inside a `using` and never keeps a scope across a `yield`.

**No site holds a scope across a frame**, which is what makes the deadline exact: the two window-shaped sites
(`AmputationMinigameSoundPatch`, `ShrapnelMinigameSoundPatch`) document "never kept open across frames" and
bound their capture window to the step they wrap, `NativeLimbActionScope` documents the same around the ONE
synchronous native call, and `RemoteDragIntentWindow` (which does span frames) holds no scope at all.

## Required work

1. DONE (2026-10-12) — **The missing stack, produced**: `tools/acceptance/recipes/call-context-scope.cs` plus the
   run above; the leaking frame is measured, not read.
2. DONE (2026-10-12) — **Fix direction decided with the artifact in hand, and cased**: the guard's release
   deadline, with the bound refusing instead of throwing. Cases: the bound (64 Enters and a further one, no
   throw, the refused origin absent from the chain), the boundary (releases, names the origins, a released
   entry's late `Dispose` is harmless), the episode rule (one line, silent while leaking, re-armed by a clean
   frame) and legitimate nesting (2-3 levels answer as before, the recovery does not fire) —
   `CallContextRecoveryTests`, four cases, all observed failing first against the pre-fix guard.
3. DONE (2026-10-12) — **Family swept**: the table above, one row per site, with the release story for each and
   the frame-spanning audit that makes the deadline exact.
4. DONE (2026-10-12) — **Reported honestly**: the line names the origins it released and how many times, and
   the refusal count when the bound was reached; Error level, once per episode.

## Acceptance rows

| # | Row | Class | State |
|---|---|---|---|
| 1 | A probe makes a game-side frame throw while its CUO scope is open, and the process keeps working: later `CallContext.Enter` calls still succeed and no depth-exceeded line appears | `machine` | passed by run 2 (fixed tree, deployed and hash-matched): the same probe, 2,847 game-side throws (8.8 s of them inside the 17 s window), `depth = 0` throughout, **zero** `depth 16 exceeded` lines, and the game's own `PantSound.Bark` scope-entering path still ran afterwards |
| 2 | The same probe names the frame that leaked the scope, so the fix is aimed at a measured site rather than a candidate | `machine` | passed by run 1: `CharacterBurp ×16` on the stuck stack, one entry site |
| 3 | A legitimate deep stack (2-3 nested scopes) still classifies correctly — the recovery does not fire, and `Current` / `IsWithin` answer what they answered before | `machine` | `CallContextRecoveryTests.LegitimateNesting_IsUntouched` + `CallContextCompositionTests` and the two origin-contract cases |
| 4 | After a recovered leak the client stays usable on both peers for the rest of the session, with the recovery reported once rather than per frame | `machine` | single-client half measured: ONE recovery line for the whole episode, the stack back to 0, the client usable on both probe modes afterwards; the "on both peers" half awaits an acceptance batch |

## Limits, stated rather than implied

- A leak still mis-classifies for the remainder of the frame it happened in: the released origin sits on top
  until the boundary, so `Current` answers it for the rest of that frame (a sound can be classified under the
  leaked origin, `LocalUseItemEligibility` can miss its `LocalAction` window). Bounded by one frame, invisible
  in the measured shape, and the alternative — a Harmony `Finalizer` on each of the 26 sites — is a larger
  change that still needs this deadline behind it.
- A REFUSED scope runs as the outer origin, which is the price of not ending the session: a refused
  `RemoteApply` applier answers `IsReplayedRemoteFact == false`, so a local-report hook may report a replayed
  fact (the duplicate-report family the origin exists to prevent), and a guard that asks for
  `Current == LocalAction` drops its capture. Both are bounded by the frame, and the state that reaches them is
  one the design does not describe.
- The boundary releases the `CallContext` scope, not every resource a throwing original can strand: the
  display-body window the two minigame patches and `NativeLimbActionScope` pair with it
  (`RemoteMedicalDisplayCapture.Enter`) rides the same skipped postfix and would stay parked. It is a separate
  resource with its own owner; named here rather than implied.
- The guard does not name the patch CLASS, only the origin, and when more than 16 Enters were refused the
  report groups the first 16 (the total is printed). The origin-to-site map is the table above.
- The probe is a single-client reproduction of the mechanism. Row 4's "on both peers" and the parent
  ticket's own rows belong to an acceptance batch against the deployed artifact.
- The recipe restores the definition through the game's own `SetTags()`; it writes nothing else, and the
  broken state it creates is not a CUO input.

## Non-goals

- Not the content defect that triggered this storm (stage 1, landed).
- Not a change to what a scope MEANS: the origins, `Current`, `IsWithin` and `IsReplayedRemoteFact` keep their
  present semantics.
- Not a general try/catch around patches: swallowing a game-side exception would hide the game's own failures,
  which the log exists to show. The bound refuses a SCOPE, and the exception that caused the leak is the
  game's own and still reaches the log.
