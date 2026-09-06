# Remote medical parity — Stage 3: remaining native medical minigames/actions

- Status: Todo
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

CPR is intentionally excluded; see `future/remote-medical-cpr.md`.

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
| 7 | Concurrent operations on same limb/item | Conflict policy applied (exclusive limb lease or per-resource arbitration) |
| 8 | Removed component item ownership | Splint/tourniquet goes to operator |
| 9 | Item/battery condition | Consumed/updated consistently with the committed action |
| 10 | Legacy direct-apply or blocked paths | Removed/replaced with the designed path |

## Red-test plan

Each action needs a focused failing test before implementation, typically proving the current code bypasses/block the native path or does not commit the intermediate state. This is carried out per action during Stage 3, not all at once.

## Out of scope for Stage 3

- CPR (future).
- Strict anti-cheat validation (separate future item).
