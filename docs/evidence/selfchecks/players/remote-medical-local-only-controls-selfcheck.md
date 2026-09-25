# Self-check — a remote medical focus presents no local-only control (`remote-medical-panel-hide-local-only-actions`)

Ticket: `docs/backlog/review/remote-medical-panel-hide-local-only-actions.md` (Medium).
Cycle: 2026-09-25. No wire change (a purely local presentation/input gate: nothing about the shared
state, the protocol or the saves moves).

## 1. Mechanism inventory (every claim from source)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The remote focus is a presentation state: open while a display-only body copy exists, closed through one method | `RemoteMedicalView` (`IsOpen` = `DisplayBody != null`, `Close`); its only opener is `RemoteMedicalCoordinator.Open`, which points the native panel (`PlayerCamera.main.WoundViewButton()`) at that copy |
| 2 | Opening the native panel swaps the whole HUD out: the panel is shown and `mainView` is deactivated; the workout list is set inactive on open | `PlayerCamera.ToggleWoundView` (`this.woundView.SetActive(!...)`, `this.mainView.SetActive(!...)`, `WoundView.view.workoutList.SetActive(false)`) |
| 3 | The local-only ACTION surfaces are three CONTROLS across four native fields: the panel's nap button (`WoundView.napbutton`), that control's own state icon (`WoundView.sleepImage`), the workout list (`WoundView.workoutList`) and the HUD main/off-hand switch (`PlayerCamera.handSwapImage`) | `WoundView.napbutton` (a `Button`; the native pass writes `napbutton.interactable = this.body.canTakeNap` every frame), `WoundView.sleepImage` (colored from `this.body.curSleep`, greyed by `!canTakeNap`, tooltip `sleepTip`), `WoundView.workoutList`, `PlayerCamera.handSwapImage` / `handSwapSprites` |
| 4 | The panel's `modeToggleImage` is NOT an action control: it flips the panel between the wound view and the ARMOR view of the displayed body | `WoundView.ToggleMode` (`armorMode = !armorMode`, sprite `modeToggleArmor`/`modeToggleHealth`), `armorMode` read in the per-limb display pass (`GetArmorReduction()` percentages + armor colors) |
| 5 | The HUD hand switch has exactly TWO entry points and no third: the HUD control's own method, and the `switchhands` key branch | `PlayerCamera.SwitchHands` (`handSlot` flip, `handSwapImage.sprite`, the `handswitchwarning` alert); `PlayerCamera.Update` → `HandleInput` → `if (Input.GetKeyDown(KeyBinds.GetBind("switchhands"))) { this.body.SwitchHands(); }`. The whole decompiled tree holds two `SwitchHands` definitions and that one caller (independently re-checked by this cycle's reviewer) |
| 6 | The keyboard path is reachable while the panel is open: `HandleInput` runs unconditionally from `Update`, and the `switchhands` branch sits after only the crafting/trade and minigame guards | `PlayerCamera.Update` calls `this.HandleInput()`; the branch's own guards |
| 7 | The workout list is never re-activated by code, and its actions have no other entry | the whole tree holds one `workoutList` write (`SetActive(false)`, panel open) and no `SetActive(true)`; `PlayerCamera.DoBodyWorkout` has no C# caller outside the prefab wiring (reviewer-verified) |
| 8 | The keyboard path lands in `Body.SwitchHands` (drop both hands, pick them back swapped, play `"switch"`), and CUO already patches that method for REPORTING | `Body.SwitchHands` (`Body.cs:1111-1133`); `BodyItemPatches.SwitchHandsPatch` (internal-reorder scope, then `OnInventoryChanged` + `OnSlotMoved(0/1, "Hands")`) |
| 9 | A Harmony postfix still runs when a prefix skips the original, so a blocking prefix must also silence the report | the repository's own runtime-verified note in `WorldTimeSync` ("the prefix returns false, but a skipped original still RUNS this postfix (verified on this repository's own HarmonyX 2.9.0…)"); `SwapSlotsPatch` relies on the same contract |
| 10 | The reporting seam has no other writer: every `OnInventoryChanged`/`OnSlotMoved` call site sits in a patch class, and `PatchBridge.Impl` is a pure forwarder | `BodyItemPatches`, `BodyPatches`, `CraftingPatches` are the only call sites (reviewer-verified); a swallowed `Body.SwitchHands` never reaches `DropItem`/`PickUpItem`, so the neighbouring patches see nothing either |
| 11 | The nap control's own readout carries no remote fact: neither `curSleep` nor `canTakeNap` is part of a character snapshot, so a display body's sleep icon is native-default | no mapping of either field anywhere in `src/` (the only hits are this change's own comments; `ModTileSleepQuality` is the unrelated tile-content type) |
| 12 | The focus has ONE close path, and it already owns the panel teardown | `RemoteMedicalView.Close` (called by `RemoteMedicalCoordinator.Close`/`Update`, `RemoteMedicalToggleWoundViewCleanupPatch`, the coordinator's open-failure branches, `RemoteBackpackCoordinator`) |

## 2. Whole-family audit

- **The action family, not one control.** The ruling names three controls and the assembly confirms
  exactly three action controls exist in that view (nap control, workout list, hand switch — four
  fields). The read-only family — stat texts, limb images, the ECG, the moodles, the armor view
  toggle — is untouched, which is the ticket's "not hiding read-only information" non-goal made
  concrete.
- **Both entry points of the hand switch are gated.** The HUD control's own method and the keyboard
  path are different methods; blocking one alone would leave the other reachable, which is exactly
  what acceptance row 3 forbids.
- **The reporting trap is closed in the same place as the block.** `Body.SwitchHands` already had a
  reporting postfix, and a Harmony postfix still fires when a prefix skips the original — so a
  blocking prefix that did not also silence the report would send an inventory change and two slot
  moves for a swap that never happened. The ONE prefix owns both verdicts and passes a null scope
  (`__state`), the shape the repository rule "a prefix that swallows a write must not let the
  postfix report it" requires.
- **The hide is a state, not a one-shot write**: it is re-asserted every frame the focus is open (the
  native pass keeps writing `napbutton.interactable`, and a prefab animation can re-activate a
  surface) and every surface is restored to the exact visibility it had when the focus opened —
  including on the close path that no longer sees an open focus (a display body destroyed under the
  focus), because the restore runs before that path's early return.
- **The ticket's own control mapping was wrong, and this cycle corrected it in place.** The previous
  cycle's evidence bullet mapped the ruling's third control to `WoundView.modeToggleImage`; the
  assembly shows that field is the armor/health VIEW toggle (row 4), whose whole purpose is to read
  the displayed body — hiding it would have destroyed information the ticket's own non-goal
  protects. The ruling's words ("main/off-hand switch") match `PlayerCamera.handSwapImage` and both
  switch paths (rows 5-8); the user confirmed the HUD switch when asked which control the ruling
  meant. The ticket now states the resolved control set first and keeps the superseded mapping as
  history.

## 3. Self-check table (mechanism × change × evidence)

| # | Rule | Where | Pinned by |
|---|---|---|---|
| 1 | The hidden surface set is declared and every name still exists on the native type | `RemoteMedicalLocalControls.NativeSurfaceFields` | `RemoteMedicalLocalOnlyControlsTests.DeclaredSurfaces_AreTheRulingControls_AndExistOnTheNativeTypes` |
| 2 | Remember-once / re-assert / restore-original is one tested rule | `RemoteMedicalLocalControls.SurfaceMemory` | `...SurfaceMemory_RemembersTheOriginalOnce_AndRestoresItExactly`, `...SurfaceMemory_KeepsAnAlreadyHiddenSurfaceHidden` |
| 3 | Every declared surface is actually written by the hiding applier | `RemoteMedicalLocalControls.Hide` | `...TheHidingApplier_WritesEveryDeclaredSurface` |
| 4 | Both hand-switch entry points are claimed in the adapter's contract inventory | `RemoteMedicalPatches.RemoteMedicalBlockHandSwitchPatch`, `BodyItemPatches.SwitchHandsPatch` | `...BothHandSwitchPaths_AreClaimedInThePatchInventory` (+ the whole-inventory `PatchContractTests`, which resolves every contract against the game assembly) |
| 5 | A blocked swap reports nothing, and a ran swap still reports all three facts | `BodyItemPatches.SwitchHandsPatch` | `...ThePostfix_ReturnsOnTheBlockedVerdict_BeforeItCanReport` — reads the COMPILED postfix: no blocked-verdict path may leave the method later than its first reporting call, and at least three reporting calls must still exist. Negative control: moving the three report statements above the verdict check (the mutation the independent review demonstrated against the previous text pin) turns this test red |
| 6 | The focus hides the surfaces and the close path cannot skip the restore; the nap button is hidden, not merely disabled | `RemoteMedicalPatches.RemoteMedicalWoundViewBodyPatch`, `RemoteMedicalView.Close` | `...TheRemoteFocusHidesTheSurfaces_AndTheClosePathCannotSkipTheRestore` (presence + order, including restore-before-early-return) |
| 7 | The new patch class is claimed by a capability | `RemoteMedicalPatches` (the Medical capability row already exists; the class is nested, so no catalog row had to be added) | the main suite's capability gate — an Integration-traited class the focused + gate ladder cannot see |
| 8 | The full suite WITH build is green on the frozen tree | the whole change | `%TEMP%/cuo-full-verify.txt` (main suite + normative gates) |

## 4. Verification design, and its honest boundary

- **Reachable red**: the suite was written against the pre-fix tree and observed failing —
  `git stash push -u -- src` (the tree back at `HEAD`, the new tests untouched), the seven cases all
  failing (missing type, missing contract row, missing hide/restore wiring, the text pin the IL pin
  replaced) in `%TEMP%/cuo-red.txt`; green after `git stash pop` (`%TEMP%/cuo-focused.txt`).
- **The reporting decision is read from the compiled method.** The first revision of this suite
  asserted the presence of three source substrings, and the cycle's independent review showed that
  moving the report statements above the verdict check keeps all three green — the very defect the
  ticket exists to prevent. The pin now disassembles the postfix and requires a blocked-verdict path
  that leaves before every reporting call; its own negative control (that mutation, applied by hand)
  was observed failing the test. The disassembly is printed in the assertion message.
- **The pure half is tested directly**: `SurfaceMemory` is Unity-free, so the rule the whole hide
  rests on (remember the original on the first pass, never overwrite it, hand it back exactly once)
  is a unit test, not a code-reading exercise.
- **NOT reachable in this host**: the Unity writes themselves. `SetActive`, `Image.enabled`, the
  panel's rendering and `RemoteMedicalView.IsOpen` all need real scene objects (a display body is
  created by instantiating the game's `Experiment` template), so nothing here proves that a control
  vanished from the screen — it proves the declared surface set against the game assembly, the
  decision memory, both patch seams, the reporting order as compiled, and the wiring between them
  (by source order, which is the strongest available evidence for code the harness cannot execute).
  The visible result is §5. The independent review could not check the same boundary from the other
  side either: whether `handSwapImage`'s GameObject also carries the hand slots — the fact that
  decides between hiding the graphic and deactivating the object — lives in the prefab, and the
  decompiled tree holds no assets.
- **Numbers name their scope**: the full-suite figure that carries the checklist box comes from
  `dotnet test CasualtiesUnknownOnline.slnx` WITHOUT a filter (the checklist gate included) — an
  earlier revision of the checklist cited an evidence run that had been filtered to exclude that
  gate and wrote the count beside the unfiltered command name; the review caught the mismatch, and
  both the command and the count are now stated together.
- **Environment**: `dotnet build` + focused test → normative gates → full suite with build;
  `dotnet format` once after the edits settled.

## 5. What only a real two-client session can confirm

1. With another player's medical panel open, the nap control and the workout list are NOT visible —
   not greyed out, gone (the reported ruling).
2. The HUD main/off-hand switch control is not visible while that panel is open.
3. Pressing the hand-switch key during the remote focus does nothing at all: no slot change, no
   `"switch"` item sound, no off-hand warning alert.
4. Closing the remote focus returns the local panel's own controls in the same frame, and the HUD
   switch works again immediately.
5. Opening your OWN panel (no remote focus) is unchanged: every native control visible and working,
   including the armor/health view toggle this cycle deliberately left alone.
6. The allowed remote operations (tourniquet/shrapnel/splint removal, dislocation fix, dropping a
   medical item on a limb) and every read-only readout still work.
7. Inventory reporting is unaffected: switching hands just before the focus and again right after it
   still updates the peer's view of both slots (the blocked window must not swallow a real report).

## 6. Limits recorded for the next reader

- The hide is re-asserted from the panel's `Update` postfix. If a prefab animation re-activates a
  surface later in the same frame, that frame could still show it; a click cannot reach the
  summoning control, because it lives inside the workout list this hides.
- The HUD image is hidden through `Image.enabled` (its own graphic) instead of deactivating its
  GameObject: the object may also carry the hand slots, and the requirement is satisfied by "its
  graphic is not visible" plus "the action is blocked" — deactivating a shared HUD container would
  have been the larger risk. The prefab answer is not in the repository, so this stays a judgement
  call rather than a verified fact.
- Each surface's remembered visibility is consumed exactly once per focus, whether or not the object
  still exists at that moment: a surface that is gone has nothing to write, and a memory kept past
  the focus would be written onto a later session's surface.
- `PlayerCamera.SwitchHands` is blocked even though the HUD is inactive while the panel is open
  (`mainView` is swapped out): the guard is acceptance row 3's "not reachable by mouse", and
  `RemoteMedicalView.IsOpen` can be true a frame either side of the native panel toggle.
- The WORKOUT action itself is not blocked, only its list: the list's own controls are its only
  entry point, so hiding the container removes the action with it (row 7). The nap action keeps its
  existing `WoundView.TakeANap` prefix.
- The armor/health view toggle stays visible on purpose: it reads the displayed body (row 4), and the
  ruling's third control is the HUD hand switch (confirmed 2026-09-25).
- The IL pin states an assumption about the compiled shape of the verdict check: it requires a path
  that leaves before the reporting calls (an inline `ret`, or a branch past them). A future rewrite
  of the guard into a different but still correct shape would fail it and should update the pin,
  not the rule.
- Deployment and dual-client acceptance remain the user's release-cycle action; this cycle was
  explicitly instructed not to deploy, so nothing here claims a real-machine run.
