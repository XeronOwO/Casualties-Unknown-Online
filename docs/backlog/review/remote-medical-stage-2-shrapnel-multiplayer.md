# Remote medical parity — Stage 2: multiplayer shrapnel removal

- Status: Review
- Priority: High
- Category: Remote medical / multiplayer shrapnel / concurrent ownership
- Parent: `remote-medical-native-minigame-parity.md`
- Source: User confirmation (2026-09-06) — shrapnel removal must support multiple players simultaneously, following the multi-player model from KrokMP.

## Run sheet (cycle 2026-10-25: what a live session could and could not judge)

Batch `20261025-d`, one live session with three clients (host, guest operator, alt). Artifact `0.1.0+c08c99ad…`.

- [x] **The break-grasp path is drivable** (row 8's substance): `grip-slip-drive` seeds shrapnel, starts the shared special session and breaks the grasp, and the slip's damage appears identically on the patient's own limb and the operator's display (recorded in `cross-player-item-use-random-determinism-20261025-b.md`).
- [ ] **Row 7 — all pieces removed — NOT driven, and the missing capability is exact.** The `pieces` probe shows the started minigame's piece count, but the minigame's lifetime is shorter than one driver round trip in this state: a `start` immediately followed by `pieces` answered `-1` twice tonight, and the next `pieces` call answered `current=none`, so a batch cannot break piece after piece inside the window. A same-call variant was tried and **also fails for a sharper reason**: starting the session inside the `break` call and breaking piece 0 in the same evaluation answered `ok` while the limb kept all five pieces and no damage appeared on either side (`display shrapnel=5 skin=100`, `host local shrapnel=5 skin=100`) — the session the start creates has no pieces yet in that frame, so the break lands on an empty game. What the row needs is a coroutine that starts the session, waits frames until the pieces exist, and breaks them one by one while the game keeps running (shaped like `cpr-rhythm`).
- [ ] **Rows 1–6 and 9** need two operators acting on the same limb inside that same window (leases, force-ungrab, cancel and disconnect), so they inherit row 7's capability.
- [ ] **Row 10** is a code question (the old snapshot hack's removal) and is judged by reading the path, not by a session.

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
