# Remote medical native minigame/action parity audit

- Status: Todo
- Priority: High
- Category: Remote medical / native medical minigame parity
- Source: User follow-up (2026-09-06) — "注射只是其中一种医疗 minigame，还有很多别的有实现吗？例如拔出破片等。如果没实现，为什么你没考虑到？你需要将待办加入 backlog。" Follow-up: "对于部分医疗功能，是可以支持多人同时操作的，例如拔破片，具体可以参考 KrokMP 的实现；对于多人操作需要考虑数据同步，该大改就大改。当前仅需回答并更新 backlog，不动代码。"

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

## Current injection dose protocol (as landed 2026-09-06)

- **CUO currently has a completion-time quantitative dose message, not a
  real-time injection stream.**
- `PlayerItemUseRequestMsg.DoseAmount` carries the total ml the acting client
  **actually pumped before the minigame ended**; the host applies that one
  authoritative dose after `MinigameBase.EndMinigame`.
- There is **no per-frame/per-milestone injection stream** and no live target
  body mutation while the syringe is still moving. The target's local body only
  receives the final `PlayerItemUseResultMsg` after the host commits.
- Open question: for "实时注射实时生效", is the completion-time quantitative
  report sufficient, or should CUO adopt a KrokMP-style continuous quantized
  injection protocol (server receives injected ml every ~0.5 s / on fill change,
  applies it to the target immediately, and broadcasts progress)? This decision
  must be made before expanding to other medical minigames.

## KrokMP reference findings (multiplayer medical sync)

Reviewed decompiled KrokMP sources in `reversing/KrokMP/...`:

- **Syringe — real-time injected-amount protocol**: `SyringeMinigame_Update_MultiplayerPatch.cs`
  sends the currently injected amount (item sync id, ml amount, target body,
  limb index, start/end/sound flag) to the server every ~0.5 s / on fill change
  (packet 10067). Server validates with
  `MedicalSync.Server_CheckCanHealerUseItemForLimb`, checks the amount is finite,
  calls `syncInfo.liquidcontainer.Inject(netBody.body.limbs[b], num)` on the
  authoritative body, and announces the syringe sound to other clients.
- **Shrapnel — true multi-player simultaneous removal**: `ShrapnelMinigame_Update_MultiplayerPatch.cs`
  tracks per-shrapnel-piece ownership in
  `MinigameMPManager.ShrapnelMinigameSession.client_shrapnel_owners`; each piece
  sends its held position to the server at ~0.05 s, and non-owners are
  force-un-grabbed so multiple players can pull different pieces concurrently.
  `MinigameBase_StartMinigame_MultiplayerPatch.cs` and
  `MinigameBase_EndMinigame_MultiplayerPatch.cs` manage the shared minigame
  session.
- **WoundSpecialAction — server-authoritative special action**:
  `PlayerCamera_WoundSpecialAction_MultiplayerPatch.cs` sends only `LimbNetId`
  to the server; server performs interaction/distance checks, runs the native
  action (tourniquet/splint removal, shrapnel/dislocation minigames), and
  queues health sync.
- **ApplyWoundItem — item-on-limb relay**:
  `PlayerCamera_ApplyWoundItem_MultiplayerPatch.cs` sends limb + item sync id to
  the server, which validates reachability, relays to other clients, and forces
  the action on the authoritative/target body.
- **Health sync**: `MedicalSync.cs` periodically sends
  `CharacterHealthStateSyncPacket` (unreliable, ~0.91 s) plus painkiller state,
  and overrides the remote WoundView ECG when the panel is pointing at a
  non-local body.

Relevant KrokMP files to mine further:

- `MinigameMPManager.cs`, `MinigameBase_StartMinigame_MultiplayerPatch.cs`,
  `MinigameBase_EndMinigame_MultiplayerPatch.cs`
- `MedicalSync.cs`, `CharacterHealthStateSyncPacket.cs`,
  `CharacterLimbHealthState.cs`, `CharacterHealthPainkillerStateSyncPacket.cs`
- `ShrapnelMinigame_Update_MultiplayerPatch.cs`,
  `BandageMinigame_DoBandageAction_MultiplayerPatch.cs`,
  `DislocationMinigame_CheckForHit_MultiplayerPatch.cs`,
  `AmputationMinigame_Update_MultiplayerPatch.cs`,
  `AmputationMinigame_PhysicsUpdate_MultiplayerPatch.cs`,
  `AEDMinigame_Update_MultiplayerPatch.cs`, `ManualDefibMinigame_Update_MultiplayerPatch.cs`,
  `CPRMinigame.cs`, `CPRHandler.cs`
- `PlayerCamera_ApplyWoundItem_MultiplayerPatch.cs`,
  `PlayerCamera_WoundSpecialAction_MultiplayerPatch.cs`,
  `WoundView_UpdateView_MultiplayerPatch.cs`

## Multi-player concurrency / data-sync factors

For operations that can be performed by multiple players at the same time
(especially shrapnel removal, but potentially bandaging, dislocation, amputation,
CPR, AED, syringe injection):

- Per-piece / per-limb contention: who owns each concurrent sub-operation;
  must not let two players mutate the same shrapnel piece / limb field and
  produce divergent snapshots.
- Authority model: likely host/server-authoritative commit with validated
  client operation messages; per-frame local simulation on actors is only
  presentation until the authority accepts.
- Real-time effect propagation: whether effects are applied continuously
  (syringe/amputation/CPR progress) or only at completion; what intermediate
  frequency is acceptable (KrokMP uses ~0.5 s syringe milestones, ~0.05 s
  shrapnel hand positions, ~0.91 s health snapshots).
- Item ownership / concurrent consumption: one medical item cannot be consumed
  by two operations at once; the authority must arbitrate item condition and
  instance state.
- Mutual exclusion and same-limb conflicts: several actors operating on the
  same body/limb need ordering or per-resource locking; arbitrary interleaving
  must still converge.
- Snapshot vs event semantics: CUO's deep-sync rules prefer dedicated events
  and host-known truth; a real-time minigame stream may require a new session
  protocol, per-operation id, start/update/end messages, or a state-stream
  extension.
- Client-side display body: remote observers need projected intermediate state
  without letting display-only bodies become authority.
- Failure/cancellation: a player who disconnects or cancels mid-minigame must
  release ownership; partial progress must be either rolled back or committed
  atomically according to the operation's semantic.
- Third-party views and roles: all directions (guest→host, host→guest,
  guest→guest via host relay) and non-actor observers.

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
- This item may require a **significant protocol/architecture change** (e.g.
  medical operation sessions, per-operation ids, start/update/end streams,
  ownership/contention handling). Do not constrain the design to the current
  single-shot `PlayerItemUseRequest` unless the per-action analysis proves it is
  sufficient.
