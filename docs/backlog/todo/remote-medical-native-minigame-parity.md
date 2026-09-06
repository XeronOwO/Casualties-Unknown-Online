# Remote medical native minigame/action parity audit

- Status: Todo
- Priority: High
- Category: Remote medical / native medical minigame parity
- Source: User follow-up (2026-09-06) — "注射只是其中一种医疗 minigame，还有很多别的有实现吗？例如拔出破片等。如果没实现，为什么你没考虑到？你需要将待办加入 backlog。"

## Problem

The first critical cycle closed fentanyl/injectable-remote-use by routing the
native `SyringeMinigame` and reporting only the ml actually delivered. That
solves one native medical minigame, but the native WoundView medical surface
exposes many other minigames and special actions. The current cross-player
remote medical path either:

- direct-applies the effect to a character snapshot without running the native
  minigame (bandages/dressings, tweezers shrapnel removal, topical/limb tools),
- blocks the native special action entirely (remove shrapnel/splint/tourniquet,
  fix dislocation),
- or has no catalog entry at all (AED/defibrillation, amputation, manual defib
  and any other WoundView-adjacent minigames not yet audited).

This ticket is not a single bug; it is the comprehensive parity audit the
original medical ticket should have opened before implementation. The goal is
to enumerate every native medical/limb minigame and special action, determine
the correct remote model, and either implement it or record a concrete blocker
with user direction.

## Known native medical minigames / actions from decompiled sources

| Native surface | Decompiled evidence | Remote status as of 2026-09-06 |
|---|---|---|
| `BandageMinigame` — bandage, ripped dressing, sterilized bandage, plastic bandage, adhesive bandage, rag, bruise kit, alginate, etc. | `Item.cs:281-620` (`useLimbAction` starts `BandageMinigame`) | Direct `RemoteHealProfile` apply; native minigame is bypassed |
| `SyringeMinigame` — injectable/IV medicines | `Item.cs:729-1086`, `Item.cs:1862-1896` | Implemented: acting client runs native syringe minigame and sends partial ml |
| `ShrapnelMinigame` via tweezers item | `Item.cs:1698` | Direct snapshot shrapnel removal; native minigame bypassed |
| `ShrapnelMinigame` via WoundView special-action "remove shrapnel" | `PlayerCamera.cs:779` | Blocked in remote focus (`RemoteMedicalBlockWoundSpecialActionPatch`) |
| `DislocationMinigame` via WoundView special-action "fix dislocation" | `PlayerCamera.cs:789` | Blocked in remote focus; no remote catalog entry identified |
| `SplintLimb.TakeOff` via WoundView special-action "remove splint" | `PlayerCamera.cs:784` | Blocked in remote focus; applying splint is direct-supported |
| `TourniquetScript.TakeOff` via WoundView special-action "remove tourniquet" | `PlayerCamera.cs:775` | Blocked in remote focus; applying tourniquet is direct-supported |
| `AEDMinigame` | `Item.cs:1253` | Not in remote medicine/tool catalogs; no parity path |
| `ManualDefibMinigame` | `Item.cs:1273` | Not in remote medicine/tool catalogs; no parity path |
| `AmputationMinigame` | `Item.cs:7141` | Not in remote medicine/tool catalogs; no parity path |
| `BandageMinigame` splint item (`splint`, `carcasssplint`, `icepack`, `musharm`, etc. non-liquid limb tools) | `Item.cs:513-625` | Some are direct-supported as `RemoteLimbToolProfile` without minigame; the native bandage/splint minigame paths are not reproduced |

## Investigation scope

- Enumerate every native WoundView entry point: drag item onto limb
  (`PlayerCamera.ApplyWoundItem` → `Item.Stats.useLimbAction` / `ApplyToLimb`)
  and the special-use button (`PlayerCamera.WoundSpecialAction`).
- Trace each native minigame's completion semantics: which result is committed,
  whether failure mutates the limb, and what the acting client must report.
- For each minigame decide the remote model:
  1. run the native minigame on the acting client against the display body and
     report the committed result (syringe model), or
  2. run a host/target-side equivalent, or
  3. keep direct application and explicitly document why the minigame cannot be
     reused.
- Cover guest→host, host→guest, third-party views, partial completion,
  cancellation, failure paths, and item condition costs.

## Acceptance criteria

- Every native medical/limb minigame and WoundView special action is audited
  and classified as implemented / direct-equivalent / blocker.
- Every user-facing remote medical action that is expected to work follows a
  designed remote interaction; no action silently bypasses a native minigame
  without an explicit decision.
- At minimum, the actions specifically called out by the user (shrapnel
  removal, splint/tourniquet removal, dislocation fix, bandages) are either
  implemented with the correct interaction or have a recorded blocker and
  explicit user direction.
- All roles/directions and failure paths are covered.
- Full build, tests, gates pass; latest DLLs deployed and artifact-verified
  before review.

## Non-goals / open questions

- No parallel CUO medical panel; native WoundView remains the only medical UI.
- Whether every native minigame should be reused verbatim on a display-only
  body, or only the committed result should travel, needs per-action analysis;
  do not assume the syringe pattern applies everywhere.
