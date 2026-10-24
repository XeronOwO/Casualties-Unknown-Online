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
- [x] **Row 10 — judged by reading the path, and the old hack is gone.** `RemoteMedicalPatches` routes the four allowed remote specials
  (tourniquet removal, shrapnel removal, splint removal, dislocation fix) through `PatchBridge.TryStartRemoteWoundSpecial` — the
  host-authoritative medical operation session — and says so at the dispatch ("They are routed through the host-authoritative medical
  operation session and started on the display body; every other special stays read-only"). The receiving half branches on a NAMED
  operation kind (`MedicalOperationApply`: a message with no kind logs a warning and replays the authoritative snapshot instead of
  injecting), and the 2026-10-11 shrapnel limb write that "vanished" is recorded there as the omission that caused it. No direct
  shrapnel-removal snapshot write remains outside the session path, so row 10 is satisfied by the code as it stands.

## Run sheet for the row-7 coroutine (next cycle executes this, in order)

1. **Declaration.** Add `tools/acceptance/driver/eval-declarations/shrapnel-break.cs` declaring `AcceptanceShrapnelBreak` with a
   `Start(limb, pieceCount)` that starts a coroutine (the `cpr-rhythm` shape: the starter returns at once, frames do the waiting).
   It reuses `grip-slip-drive`'s two proven pieces — the `objects`/`currentlyHeld` fields on `ShrapnelMinigame` and the
   ownership report a grab must post first — by invoking the same report path before each `BreakGrasp()`.
2. **Recipe.** Add `tools/acceptance/recipes/shrapnel-break.cs` (`args: limb=n count=n`) that resolves the declaration and calls
   `Start`, exactly like `cpr-rhythm` does; the recipe returns immediately.
3. **Drive.** `seed-shrapnel limb=<n> count=<k>` on the patient, `remote-wound-special mode=open` then `mode=special` on the
   operator (proven tonight: `started=true, shrapnel=3`), then `shrapnel-break limb=<n> count=<k>`.
4. **Read.** `pieces` and the limb read on BOTH clients: the pieces must go `k → 0` and the session must END with the limb's final
   state agreed on the patient and the operator (row 7). Rows 1-6 and 9 then run inside the same window: two operators acting on the
   same limb (leases, force-ungrab, cancel, disconnect).
5. **Failure branch.** If the coroutine's first frame still reports `current=none`, the session died before the coroutine's first
   tick — record that and stop rather than re-running step 3 a third time.

**One more measurement for the sheet (batch `20261025-aa`)**: with the session started through the production special
(`remote-wound-special mode=open` → `opened=true`, then `mode=special` → `started=true, shrapnel=3`), a `grip-slip-drive
mode=break` sent **0.4 s later** answered `no-shrapnel-minigame / "currentMinigame is null"`. So the window is shorter than one
driver round trip even when the session was just started successfully — which is the same conclusion the sheet encodes, now
measured from the other side (start → break, not start → pieces). The coroutine is not an optimisation here; it is the only shape
that can act inside the window.

**Batch `20261025-ac` — the instrumented run says the coroutine's own path did not run.** The declaration was renamed
(`AcceptanceShrapnelBreak2`) so the driver's idempotent presence probe would actually re-send it (`sent=true`), and a `Trace` field
records which branch the coroutine reaches. Sequence: `open` (`opened=true`) → `shrapnel-break count=3` (`trace=started`) →
`special` (`started=true, shrapnel=2`) → patient's limb read at **`shrapnel=2, skin=94.8`** with **`pieces=none`** afterwards. So
exactly ONE piece left the limb and the session ended, while `Trace` never advanced past `started`: the coroutine's own
grab/report/break path did NOT run, and the single removed piece came from the native dispatch of the special itself. The next
question is therefore narrow — why the coroutine's 180-frame wait never saw the minigame the session evidently had, or where its
`ShrapnelMinigame` resolution failed — and `Trace`'s values are the door to it.

**Batch `20261025-ad` — the answer is that the coroutine never RESUMED, and the native dispatch did the work.** The host log for
that run carries the session's own lines: `break-grasp applied to 76561198863287957 limb 4: skin -5.199 bleed +0.886 pain +12.38`,
`piece 2 removed by 76561198281246659`, then `session 2 timed out after 15016 ms idle` and `terminal TimedOut` — so the piece that
left the limb came from the NATIVE dispatch and the session simply idled until its timeout. And `Trace` still reads `started`
even now: no later value was ever written, which means the iterator never resumed past its first `yield return null` — a
`StartCoroutine` on a HotRepl-compiled iterator is not being scheduled by this client. The next cycle pumps the coroutine from a
MonoBehaviour the game itself owns (or a patch-driven tick), not from the declaration's static Start: the break path is already
proven to work when it runs, so only the scheduling is missing.

**Batch `20261025-ae` — after a timed-out session the remote view will not open again.** With the victim moved next to the operator
(`body-place` → `(1,506)`), `seed-shrapnel limb=4 count=3` → `shrapnel=3`, the operator's `remote-wound-special mode=open` answered
**`opened=false, viewOpen=false, displayBodyName=null`** while the member facts were plainly present (`hasSnapshot=true,
hasHealth=true`), and a retry 2 s later answered the same. So the earlier session's timeout left the remote view unable to build its
display body — a state the ticket's rows 5/6 (cancel, disconnect) are about, and one a later cycle should read on its own rather
than as a side effect of this batch. Row 7 still waits on the same scheduling fix.

**Batch `20261025-ab` — the declaration is written, declared, and does NOT break yet; the session turns out to be LONG-LIVED.**
The `shrapnel-break` declaration (a coroutine that waits for the minigame, posts the grab report, sets the held slot, waits two
frames and calls `BreakGrasp()` per piece) plus its thin recipe were written and declared (`-Action declare -Declare
shrapnel-break` → `sent=true`). The batch then started the coroutine FIRST (`count=3`, answered `started`) and started the session
after it (`mode=open` → `opened=true`, `mode=special` → `started=true, shrapnel=3`), and 3 s later `pieces` answered **5** — the
session is alive and holds five pieces, so it does NOT die in 0.4 s as the earlier note inferred: that earlier `currentMinigame is
null` had another cause. The coroutine nevertheless broke nothing, which points at its own reflection (the CUDA type lookups or the
`ShrapnelMinigame` cast) failing silently into a `yield break`; the next cycle instruments the declaration's first steps instead of
re-testing the window.

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
