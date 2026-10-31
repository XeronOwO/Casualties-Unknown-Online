# Remote medical parity — Stage 3: remaining native medical minigames/actions

- Status: Review
- Priority: High
- Category: Remote medical / native minigame parity / special actions
- Parent: `remote-medical-native-minigame-parity.md`
- Source: User follow-up — the syringe is only one of many native medical minigames; the whole family must be aligned, not one action.

## Objective

After Stages 1 and 2 establish the `MedicalOperationSession` layer, align the remaining native medical/limb surfaces:

- Bandage/dressing minigame (`BandageMinigame`)
- Splint removal (`SplintLimb.TakeOff`)
- Tourniquet removal (`TourniquetScript.TakeOff`)
- Dislocation fix (`DislocationMinigame`)
- AED (`AEDMinigame`)
- Manual defibrillation (`ManualDefibMinigame`)
- Amputation (`AmputationMinigame`)
- Any other WoundView limb action that was found bypassed or blocked in the original audit

CPR is intentionally excluded; see `remote-medical-cpr.md` in this folder (promoted out of `future/` on 2026-10-07).

## Per-action design summary

### Bandage / dressings

- Current state: `RemoteHealProfile` direct-apply, native `BandageMinigame` bypassed.
- Intended model:
  - Run native `BandageMinigame` on the operator's display body.
  - Each `DoBandageAction` / wrap step is reported through the operation session (or a small batch of completed wraps).
  - Host applies the authoritative treatment to the target snapshot at the end or at milestones, depending on the exact native effect pattern.
- Must audit every bandage-family item to confirm whether each uses `BandageMinigame` or a direct limb action; no silent direct-equivalent without an explicit decision.

### Splint / tourniquet removal

- These are not native minigames; they are one-shot special actions.
- Intended model:
  - Operation session start/end is still used because host must arbitrate who owns the removal and ensure no duplicate removal.
  - Host removes the limb component state and clears `splinted`/`blockedBleeding` facts.
  - **The removed item is given to the operator** (user-confirmed design).
- Target's own local body must be instructed to remove the native component and does not auto-pick the item.

### Dislocation fix

- Native `DislocationMinigame` is an interactive minigame.
- Intended model:
  - Operator runs the native minigame on the display body.
  - Host receives authoritative progress/hit updates if the action is long-running.
  - On successful completion, host commits `Dislocated=false` and `DislocationTimer=0`.
- Decide whether multiple operators may hit the same bone; if yes, share the session state like shrapnel; if no, use an exclusive limb lease.

### AED / Manual defibrillation

- Native minigames with battery/charge/shock stages.
- Intended model:
  - Operation session tracks charge/state and battery consumption.
  - The final shock is host-validated and committed.
  - Remote display/ECG remains native WoundView; no parallel UI.
- Both item types need battery/condition synchronization while an operation is active.

### Amputation

- Native continuous cut-progress minigame.
- Intended model:
  - Session tracks `cutProgress`, incremental pain/bleed/skin/muscle changes.
  - Final `Dismember()` and connected-limb effects are committed by host only when cut reaches 100%.
  - Partial progress remains as committed damage on cancel/disconnect.
- This is the heaviest remaining continuous action and should reuse Stage 1 update mechanics.

## Acceptance matrix (all actions)

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest uses each action on host | Native-equivalent interaction runs and authoritative state converges |
| 2 | Host uses each action on guest | Same |
| 3 | Guest uses each action on another guest | Host relays and all views converge |
| 4 | Third-party observer | Sees the same progress/state where applicable |
| 5 | Cancel mid-action | Per-action partial semantics applied; no stale session |
| 6 | Disconnect mid-action | Host releases resources and commits/aborts according to action semantics |
| 7 | Concurrent operations on same limb/item | Conflict policy applied (exclusive limb lease or per-resource arbitration) — **superseded 2026-09-19**: the limb lease is gone, the item claim stays, and per-unit settlement governs (see `review/concurrent-medical-operations.md`) |
| 8 | Removed component item ownership | Splint/tourniquet goes to operator |
| 9 | Item/battery condition | Consumed/updated consistently with the committed action |
| 10 | Legacy direct-apply or blocked paths | Removed/replaced with the designed path |

## Red-test plan

Each action needs a focused failing test before implementation, typically proving the current code bypasses/block the native path or does not commit the intermediate state. This is carried out per action during Stage 3, not all at once.

## Out of scope for Stage 3

- CPR (future).
- Strict anti-cheat validation (separate future item).

## The "host treats a member" column, four actions read on the deployed build (2026-11-01)

Row 2 of the matrix ("host uses each action on guest") is being read action by action, each through the chart's own release path
(`medical-open` then `remote-medical-release`), with the patient's own client answering for what landed:

| action | reading on the patient | the host's own line |
|---|---|---|
| bandage | the limb healed (`skinHealth` 63.04 -> 100, bleeding 0) | `started remote Bandage minigame … limb 4`, then `operation 1 (Bandage) ended`, terminal `Completed` |
| splint | `SplintLimb` component on the limb | `requested use of <patient> limb 4 with splint`, `<patient> installed splint` |
| tourniquet | `TourniquetScript` component on the limb | the same request/installed pair for the tourniquet |
| icepack | `ChilledLimb` component on limb 3 | the same pair for the icepack |

So four treatment families read end to end in this direction, and the topical/liquid rows keep their own readings on
`topical-limb-action-shape`. What the column still needs is the actions whose fixtures the batch cannot produce (a syringe holding a
liquid, the chest drain and the other liquid containers), and each of those is named in the triage page rather than assumed.

## Two more actions land on the named limb, with a field-reading caveat (2026-11-01, 08:50)

A fresh session with the guest wounded on limb 3 (`skinHealth=73.6`):

- **pain cream** — `remote-medical-release item=paincream,limb=3` answered `limbLookingAt=3` and the patient read `pain=87.919` immediately after. It leaves no component, so this pass proves the release reached the named limb but not the effect (no before-value was taken).
- **disinfectant** — the same release answered `limbLookingAt=3`; `limb-read` carries no disinfection field on this recipe's projection, so the effect is unread here.
- **medical suture** — the release found nothing (`limbLookingAt` empty): the host's slots were full by then and the item had gone into its carried container, the fixture rule this project already carries ("one treatment per fresh launch").

So the column's component-visible families stay the four already tabled (bandage, splint, tourniquet, icepack); the cream, the disinfectant and the other field-only treatments need their own before/after field readings, one per fresh session, and `limb-read` needs the fields those rows ask about (pain and disinfection are the two this pass could not read).

## The slot technique applied to the limb-tool actions (2026-11-01, 09:20)

With the batch-side way to free a slot (consume a carried item: `item-use type=dogfood` / `type=waterbottle` answering `usable=True`), the same release path was re-run on a fresh run, one action per freed slot:

- **splint** -> `limbLookingAt=2`, the patient's own client carries **`SplintLimb`** (an independent second reading of the family);
- **tourniquet** -> `limbLookingAt=3`, **`TourniquetScript`** (second reading);
- **medical suture** -> `limbLookingAt=4` on a freshly wounded limb (`skinHealth=35.32`), and the patient's `skinHealth` moved to 37.816 with no component: the suture's outcome is a wound state this recipe does not project, so the row needs a field reading (the wound's sutured/closed flag) rather than a component check.

So the column reads four component-visible families (bandage, splint, tourniquet, icepack) and the field-only ones (suture, pain cream, disinfectant, the drain) are each one `limb-read` field away — the fields, not the fixtures, are what those rows still need.