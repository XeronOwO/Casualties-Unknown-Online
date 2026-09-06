# Remote medical native minigame/action parity — roadmap

- Status: Review
- Priority: High
- Category: Remote medical / native medical minigame parity
- Source: User follow-up (2026-09-06) — "注射只是其中一种医疗 minigame，还有很多别的有实现吗？例如拔出破片等。如果没实现，为什么你没考虑到？你需要将待办加入 backlog。" Follow-up: "对于部分医疗功能，是可以支持多人同时操作的，例如拔破片，具体可以参考 KrokMP 的实现；对于多人操作需要考虑数据同步，该大改就大改。"

## Confirmed direction (2026-09-06)

The user confirmed the design before implementation:

1. **Breaking changes are allowed.** This project is not publicly released, so old compatibility layers, dead paths and legacy fields that the new architecture supersedes are deleted, not kept.
2. **After public release, CUO itself must enforce a strict mod/protocol version check.** Versions that do not match are rejected; no old/new compatibility shim is planned.
3. **Injection is real-time.** The end-of-minigame single-dose report is not enough. Adopt a session-based incremental stream: host commits each delivered ml increment and progress is visible to the target and third parties while the syringe is still moving.
4. **Shrapnel removal is a true multiplayer shared session.** Per-piece ownership, non-owner force-ungrab, shared progress, remote-hand/third-party visibility and disconnect/cancel handling are all in scope.
5. **Removed splint/tourniquet items go to the operator.** If a player takes a splint or tourniquet off another player, the removed item appears in the operator's inventory.
6. **CPR is NOT a native medical surface in Assembly-CSharp.** KrokMP ships a custom `CPRMinigame`/`CPRHandler`; it is treated as a future enhancement, not part of native parity.
7. **Implementation is staged.** This umbrella ticket is split into stage tickets below; the old monolithic audit document is replaced by these stage files.

## Stage index

| Stage | Ticket | Scope |
|---|---|---|
| 1 | [Medical operation session + real-time injection](remote-medical-stage-1-injection-session.md) | Generic `MedicalOperationSession` protocol and the migration of syringe/IV medicine to real-time incremental injection |
| 2 | [Multiplayer shrapnel removal](remote-medical-stage-2-shrapnel-multiplayer.md) | Shared shrapnel minigame session with per-piece ownership, concurrent operators, force-ungrab and end/abort semantics |
| 3 | [Remaining native medical minigames/actions](remote-medical-stage-3-other-actions.md) | Bandage/dressing minigame, splint/tourniquet removal, dislocation, AED, manual defibrillation, amputation and remaining WoundView actions |
| Future | [CPR enhancement](../future/remote-medical-cpr.md) | KrokMP custom CPR; not native parity, deferred to future |

## Global acceptance criteria

- Every native medical surface is either implemented through a designed remote interaction or explicitly recorded as future/non-goal with user acceptance.
- All roles and directions are covered:
  - guest → host
  - host → guest
  - guest → guest via host relay
  - operator view
  - target view
  - third-party observer view
- Multiplayer concurrency is covered:
  - per-piece / per-limb ownership and leases
  - same-limb and same-item conflict arbitration
  - concurrent item consumption prevention
  - disconnect, cancel, partial-progress and timeout behavior
- No parallel CUO medical panel. The native WoundView and native minigame surfaces remain the only UI.
- Each runtime stage is delivered only after:
  - red regression test seen on the current code
  - implementation
  - full build + tests + gates
  - independent adversarial self-check
  - deploy to the real game directory and artifact hash verification
  - backlog transition / documentation update

## Non-goals / open

- No CPR parity in this roadmap; see `future/remote-medical-cpr.md`.
- No anti-cheat hard validation beyond accept-first arbitration; strict validation remains a separate future item.
