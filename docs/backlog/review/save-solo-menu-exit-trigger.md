# S3.6 — Solo menu-exit trigger for the mid-run cut

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: Medium
- Category: Persistence / save system
- Source: `docs/backlog/review/save-mid-run-consistent-cut.md` → scope 9 ("solo menu-exit trigger")
- Related: `docs/architecture/save-archive-format.md` §4 (cut phases), decision 167 (the cut seam),
  decision 176 (this trigger), `docs/backlog/review/save-system-mid-run-and-layer-end.md` (stage table)

## The gap (as reported)

The deliberate menu return is the second mid-run trigger (decision 167). It was requested from
session-teardown events and decided by `RunMenuReturnPolicy.Decide(role, inWorld)`, which returned
`SaveAndMenu` only for `SessionRole.Host`. Solo play has NO session role (`SessionRole.None` — the
enum's own contract), so a solo player leaving to the menu got no menu-return cut at all: `/save` was
the only mid-run trigger there.

## The mechanism finding that changed the fix

The ticket's suggested fix — "one in-world → menu transition edge in the run coordinator that requests
the same frame-end seam cut" — cannot work, and the evidence is the game's own leave path:

- `RunCoordinator.UpdateSceneState` computes `inWorld` and consumes the transition only AFTER it
  happened (the seam runs later in the same pump with that value).
- The leave itself is ONE line: `PlayerCamera.ToMainMenu()` → `SceneManager.LoadScene("PreGen")`
  (`PlayerCamera.cs:613-616`). Unity destroys the old scene during the load, so by the next pump frame
  `WorldGeneration.world` is gone — the world the cut has to read no longer exists. A cut requested on
  the transition edge is refused (`WorldCutWriter`'s native reads, or a stale request), and the leave
  has already happened, so nothing can wait for it.
- The deliberate-leave family is exactly the `ToMainMenu` call sites: the tutorial's pause exit
  (`PauseHandler.cs:66`), the console's `saveandquit` (`ConsoleScript.cs:792`), the layer-end panel's
  save-and-exit (`WorldGeneration.cs:1029`) and the death screen's MAIN MENU button — the last two are
  scene-wired UnityEvents in `level1` (verified in the shipped scene asset: the `ToMainMenu` target
  sits beside the `endscreenmainmenu` locale key), invisible to a source grep. The host path already
  worked only because a session teardown event arrives BEFORE the leave — solo has no such event.

So the trigger is the LEAVE ACTION, not a transition edge: intercept `PlayerCamera.ToMainMenu`, record
the intent, skip the original scene load once, and let the frame-end seam take the cut and perform the
leave on the pump — exactly the sequence a host's teardown-driven return already had.

## What landed

- `PlayerCameraMenuExitPatches` (new) — `[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.ToMainMenu))]`.
  The prefix skips the original load ONLY when the leave was recorded AND the seam will act on it.
- `PlayerCamera.ToMainMenu` is the single funnel; the request carries an ORIGIN
  (`RunMenuReturnOrigin.PlayerLeave` / `SessionTeardown` / `HostPull`) because the seam's staleness
  rule depends on who asked: a teardown request belongs to the session that is ending, while the
  player's own leave — and a guest's world ending under it — must not be dropped while a world is
  leavable.
- `RunMenuReturnCoordinator` owns the leave: `Request` (record mode + origin) and `Leave()` (perform;
  returns false when the camera vanished, so the request stays armed instead of being consumed).
- `RunMenuReturnPolicy.Decide` answers `SaveAndMenu` for a host AND for the role-less solo player;
  `MenuOnly` is reserved for a guest (decision 164's rule is "only a guest lacks the world-archive
  authority"), and the seam's `IWorldSaveControl.TryRequestCut` still refuses a guest.
- `RunMenuReturnPolicy.WouldLeaveWorld` / `DecideFlush` + `MenuExitInterception` are the L0-testable
  decision; the seam's earlier inline guard (`!inWorld || SessionActive || camera == null` → silent
  `Clear`) is now that rule and LOGS every drop.
- `HarmonyTraverse.HasLiveWorld` is the shared "in world" expression (the patch's gate and
  `RunCoordinator.UpdateSceneState` both use it, so they cannot drift).
- A TUTORIAL entry gets no world archive at all — the SAVE LAYER owns that rule
  (`IWorldSaveControl.TryBeginRun(isTutorial)`, `WorldSaveService`): a tutorial run is generated with
  `biomeOverride == Tutorial`, the game's own save surface is disabled there (`WorldGeneration.cs:979`),
  and an archive is what the Continue entry opens; the previous run's identity is released at the same
  moment, so a `/save` fired during the tutorial can never rewrite the previous run's snapshot.
- Architecture splits made in the same cycle (the 600-line gate): `IModContentPatchBridge` +
  `ModContentPatchBridge`, `ICarriagePatchBridge` + `CarriagePatchBridge` and
  `ISessionSurfacePatchBridge` (implemented by `OnlineMenuInputGuard`) moved three cohesive families off
  `IPatchBridge`/`GameAdapterBridge` rather than growing them past the ceiling.

## Defect found by the adversarial passes and fixed in the same cycle

The first cut of the interception returned Harmony's prefix verdict INVERTED (`true` runs the
original), so the game's own load ran whenever it should have been deferred — and, worse, the seam's
own leave was skipped, breaking the pre-existing host/guest return. The same round suppressed the load
whenever a world was live while the seam still dropped the request for a live `SessionActive` (a
session exists from the moment a lobby is created): a player alone in a lobby leaving via the death
screen's MAIN MENU, the layer-end panel or `saveandquit` would have had the load suppressed AND the
request dropped — the action does nothing and the player stays in the world. Both independent reviews
found these, and a third (verification) pass found that the origin rule was documented but not
implemented. All three are fixed by the structure above: the verdict is a negated, source-shape-tested
one-liner; the interception asks `WouldLeaveWorld` BEFORE recording; `DecideFlush` reads the origin, so
only teardown requests are dropped for a session that took over; the request is cleared only after a
successful leave; and a tutorial entry owns no archive.

## Acceptance

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | Solo play, in world, leave to the menu | The same `MenuReturn` cut is armed and taken at the frame-end seam, and `WorldCutReport` names it | `RunMenuReturnPolicyTests.NoRoleInWorld_ReturnsSaveAndMenu_SoloIsItsOwnSaveAuthority` (red→green), `DecideFlush_SoloLeave_LeavesTheWorld`, `DecideFlush_AnInterceptionPrecondition_AlwaysLeaves`, `ShouldSuppressSceneLoad_OnlyForARecordedDeferral`; the seam's `SaveAndMenu` path; `PatchContractTests` resolves the new patch against the game assembly |
| 2 | Solo play, not in world (menu/generation) | No cut is requested; nothing is written | patch gate `HarmonyTraverse.HasLiveWorld` (false in the menu / during generation) + `Decide` → `None` for `!inWorld` |
| 3 | Host (with or without guests) deliberately leaves | The same cut, taken before the leave — unchanged for the teardown path, and now also taken when the host leaves through the game's own action | `HostInWorld_ReturnsSaveAndMenu`; `DecideFlush_PlayerLeave_IsNeverDroppedForALiveSession` (a live lobby must not turn the host's own leave into a silent no-op) |
| 4 | Guest leaves | No cut is requested at all (the save layer refuses a guest — decision 164) | `GuestInWorld_ReturnsMenuOnly`, `WorldSaveService.TryRequestCut`'s guest refusal; the guest is pulled out when the host goes to the menu even though that lobby is still up (`DecideFlush_HostPull_LeavesEvenWhileTheSessionIsStillUp`) |
| 5 | Cut refused (no repository, no run baseline, a stuck transient past the deferral bound) | The leave still happens; the report names the refusal and the previous snapshot stays | unchanged `FlushMenuReturn` refusal/deferral branches (landed in S3, tests in `WorldSaveCutSeamTests`/`WorldSaveCaptureTests`) |
| 6 | A session is live when a TEARDOWN request is pending (a new lobby took over) | That request is dropped as stale and logged — it belonged to the session that ended | `DecideFlush_TeardownRequestedAgainstANewSession_IsDropped` |
| 7 | Tutorial entry, then leave | No archive is created, no cut is written, and the previous run's identity is released | `WorldSaveCaptureTests.TryBeginRun_Tutorial_GetsNoArchive_AndReleasesThePreviousRun` (new); `RunSaveCoordinator.BeginRun(isTutorial)` passes the entry kind through |

## Verification limits

Runtime suites pin the decision rule (role → mode), the cut/leave precondition, the interception
verdict and the request record. `PlayerCameraMenuExitPatches`' hook and its exact signature are verified
against the game assembly by `PatchContractTests` (the new class is picked up by
`PatchInventory.BuildContracts` automatically) — that is existence and signature, NOT the verdict; the
verdict is the L0-tested `MenuExitInterception`. The patch body itself cannot be machine-driven here:
it reads Unity singletons, so the actual in-world → menu leave, the one extra frame it takes, and the
"cut then leave" order on a real solo run need the user's in-game pass.
