# Remote medical parity — Stage 2: multiplayer shrapnel removal

- Status: Review
- Priority: High
- Category: Remote medical / multiplayer shrapnel / concurrent ownership
- Parent: `remote-medical-native-minigame-parity.md`
- Source: User confirmation (2026-09-06) — shrapnel removal must support multiple players simultaneously, following the multi-player model from KrokMP.

## Objective

Implement a shared multiplayer `ShrapnelMinigame` remote path so multiple players can remove different shrapnel pieces from the same remote limb concurrently, with per-piece ownership, conflict arbitration, force-ungrab, progress propagation, third-party visibility and correct cancellation/disconnect behavior.

## Reference

Key KrokMP sources (mechanisms only; not copied):

- `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/ShrapnelMinigame_Update_MultiplayerPatch.cs`
- `ShrapnelMinigame_BreakGrasp_MultiplayerPatch.cs`
- `MinigameMPManager.cs` — server-side `ShrapnelMinigameSession`, piece locations, owner map, involved players
- `MinigameBase_StartMinigame_MultiplayerPatch.cs`
- `MinigameBase_EndMinigame_MultiplayerPatch.cs`

Native mechanics:

- `reversing/Assembly-CSharp/Assembly-CSharp/ShrapnelMinigame.cs`
- `PlayerCamera.WoundSpecialAction` starts shrapnel via WoundView special action
- tweezers item path starts `ShrapnelMinigame` with `hasTweezers=true`

## Design

### Session model

One limb with shrapnel gets one shared operation session. Multiple operators may join the same session.

The host session tracks:

- `TargetSteamId`, `LimbIndex`
- `PieceCount` (native max appears to be 5 slots)
- For each piece:
  - `PieceIndex`
  - `AnchoredPosition`
  - `OwnerSteamId` + lease expiry
  - `Removed` flag
  - `Failed`/break-grasp state (optional)
- `Sequence` for stale updates

### Entry points

Both remote entry points must start the same session:

1. WoundView special action "remove shrapnel" on a remote limb.
2. Dragging tweezers onto a remote limb in WoundView.

The remote focus is no longer read-only for this specific action.

### Ownership and concurrency

- Host is the authority for which piece is held by whom.
- First plausible update claiming a free piece grants ownership that is held until the piece is released, removed, broken, or the operator leaves/cancels/disconnects. A drag-to-drop is one atomic operation: while one operator owns the piece, no other operator can select or move it (confirmed 2026-09-06).
- While a piece has an owner, non-owners cannot move it; their local minigame must force-ungrab or visually mark it unavailable.
- Different pieces can be operated by different players simultaneously.
- Removing a piece commits immediately: the authoritative limb `Shrapnel` count decreases and progress is broadcast.
- Break-grasp/failed removal follows native damage semantics and is relayed to all relevant views.

### Progress propagation

- Piece positions/ownership: unreliable, ~15–20 Hz for presentation.
- Ownership changes, removal, end/cancel: reliable, because they are semantic transitions.
- Target and third-party remote medical views display the same authoritative piece state.

### Item handling

- Tweezers is an operator-owned item. In the multi-player session, each operator who uses tweezers reserves and consumes their own tweezers condition per operation, not one shared item.
- WoundView special-action shrapnel removal without tweezers has no item.

### Cancel / disconnect / partial

- Removed pieces stay removed.
- Owner disconnect releases that piece's lease.
- If all pieces are removed, host commits successful end.
- If an operator cancels, their held piece is released; remaining pieces stay in the wound.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Two guests remove different shrapnel pieces from the same limb simultaneously | Both succeed without overwriting each other |
| 2 | Two players try to grab the same piece | Only the lease owner moves it; the other is force-ungrabbed |
| 3 | Host and guest operate on the same limb | Both directions work through the host session |
| 4 | Third party watches the shrapnel minigame | They see authoritative piece positions in real time; ownership is not required to be displayed, but non-owners cannot select/move a held piece |
| 5 | Operator cancels mid-removal | Held piece is released; already removed pieces stay removed |
| 6 | Operator disconnects while holding a piece | Host releases the lease and terminates/updates the session |
| 7 | All pieces removed | Session ends with final authoritative limb state |
| 8 | Break-grasp failure path | Damage/pain/bleed is applied and visible to relevant players |
| 9 | Tweezers item condition | Each operator's own tweezers is drained only for their own operation |
| 10 | Old direct shrapnel-removal snapshot hack | Removed or replaced by the session path |

## Red-test plan

Add focused tests before implementation, e.g.:

- A host-side session test where two players claim the same piece and the current code has no ownership arbitration (or no session at all).
- A test proving non-owner move is rejected/ignored while an active lease exists.
- A test proving cancellation releases ownership.

The red must be observed on current code before implementation.

## Out of scope for Stage 2

- Other multiplayer-capable minigames (amputation/CPR) are later stages or future.
- CPR remains future.
