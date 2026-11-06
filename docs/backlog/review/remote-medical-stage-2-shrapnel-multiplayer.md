# Remote medical parity — Stage 2: multiplayer shrapnel removal

- Status: Review — rows 1-3, 5, 6 and 7 pass on the deployed artifacts: rows 1 and 3 on `0.1.0+c93f074b`, row 2 on the same with the user's refusal decision, row 5 in both halves on the same, row 6 on the same, row 7 on `0.1.0+39300598` (batch `20261106-h`). Row 4's earlier reading was withdrawn (2026-11-06): it was judged against a bystander copy of the minigame that no player ever asked for, and that copy has been removed — row 4 is re-read as non-interference (the bystander's client opens nothing and stays consistent while two operators work). Row 9's tweezers reading is still owed. Readings: `docs/evidence/acceptance/remote-medical-stage-2-shrapnel-20261106-h.md`, `-k.md` and `-l.md`.
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

**Batch `20261025-ae` — NOT a stuck view: the operator's body was DEAD.** With the victim moved next to the operator
(`body-place` → `(1,506)`), `seed-shrapnel limb=4 count=3` → `shrapnel=3`, the operator's `remote-wound-special mode=open` answered
`opened=false, viewOpen=false, displayBodyName=null` while the member facts were present (`hasSnapshot=true, hasHealth=true`), and a
retry answered the same. The host log then named the cause plainly — `native WoundView did not open for 76561198863287957 — local
body is not alive/available` — i.e. the OPERATOR had died, the same trap an earlier cycle hit on the pickup grant. After `/heal @s`
the same call answered `opened=true, displayBodyName=MedicalDisplay_110000135D30695`, so nothing was stuck and the previous
paragraph's suspicion is withdrawn: a failed open on this surface must be read with the operator's own body state first.

**Right after that heal, the start-then-break gap is still there and is now precise.** With the view open and the operator alive,
`mode=special` answered `started=true, shrapnel=3`, and a `grip-slip-drive mode=break` sent **0.3 s later** answered
`no-shrapnel-minigame / "currentMinigame is null"` again. So the minigame the coordinator reports starting is not what
`MinigameBase.main.currentMinigame` holds on the operator's client a third of a second later — the scheduling fix for the coroutine
and this gap are the same question, and the next cycle starts by reading what `currentMinigame` actually is on each client during
the session rather than assuming the host's minigame base is the one.

**Batch `20261025-af` — there is no native minigame holding the session on EITHER client.** `minigame-end` reads the active
minigame through the game's own `MinigameBase`: right after `mode=special` answered `started=true, shrapnel=3`, the OPERATOR
answered `before=none` and, in the same window, the PATIENT answered `before=none` too. So the CUO shared shrapnel session runs
with no `MinigameBase.main.currentMinigame` on either peer in this state, which is why `grip-slip-drive mode=break` (and the
coroutine that mirrors it) finds nothing to act on: they are both written against the native minigame's `objects`/`BreakGrasp`,
and in this session there is no such object to hold. Row 7 therefore needs either a state that DOES create the native minigame
(the earlier batch's successful grip-slip reading came from one) or a session-level API that removes pieces without it — and that
choice is a design question, not another read.

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

## Batch `20261106-h` — row 7 read, and the missing link was the position report

Artifact `0.1.0+39300598`, one session (`session 3`), host as operator and guest as patient. The instrument is
`tools/acceptance/driver/eval-declarations/shrapnel-break6.cs` + `tools/acceptance/recipes/shrapnel-break6.cs`: a
coroutine that, per piece, posts the ownership report, waits two frames, calls the native `BreakGrasp()`, lifts the piece
above the game's y=35 line, and then posts one ordinary position report with `Y = 60`.

What the previous cuts missed: the host marks `piece.Removed` only when a report carries `msg.Y >= RemoveThresholdY`
(35), and `AllPiecesRemoved` is what terminates the session `Completed`. break3/4/5 posted grabs and breaks without a
position, so the pieces left the display but the host's session never learned a piece was gone — run 1 in this batch
idled out (`session 1 timed out after 15016 ms idle`, `terminal TimedOut`) even though five break-grasps had landed.

The row-7 reading: `session 3 created … (5 pieces)`, five `grip slip` + `break-grasp applied` pairs, five
`piece N removed`, then `session 3 terminal Completed` (20:31:08.399); the display limb reads `shrapnel 0` and
`minigame-end` answers `before:"none"` — the screen closed itself. Record:
`docs/evidence/acceptance/remote-medical-stage-2-shrapnel-20261106-h.md`.

Rows 1-6/9 remain: they need two operators on the same limb at once (leases, force-ungrab, cancel, disconnect). A
second operator client (the alt) is the fixture that batch needs, and the `shrapnel-break6` instrument is the shape a
two-operator case reuses.

## Batch 20261106-i — two-operator fixture notes

Artifact `0.1.0+39300598`, three clients in one world (host 18590, guest 18591 as the patient, alt 18592 as the second
operator), lobby `109775243632123666`. Both operators opened the display wound view on the same limb and read
`opened:true, viewOpen:true, displayBodyName:"MedicalDisplay_110000135D30695"`.

Readings that shape the next cut: the host's first `mode=special` was refused (`[Shrapnel] refused start … No line of
sight.`, 20:39:12.987) until the host was moved next to the patient (`teleport x=2.5 y=497.6`); a start creates a
session PER OPERATOR rather than joining one — the alt's start made `session 4` (20:39:21.910, operator
`76561199526807662`), which idled out after 15 s, and the host's later start made `session 5` (20:39:58.888, operator
`76561198281246659`). A second `mode=special` from a client that already holds a shrapnel session answers
`started:false` because its local `_activeShrapnel` is still set; re-entering needs the view closed or the session
cancelled first.

So rows 1-6/9 have two candidate shapes and the next batch must read which one the product intends: two live
per-operator sessions on the same limb, with the host arbitrating piece leases across them; or one shared session that a
second operator can join. Both need the second operator to be able to start while the first session is live, which the
current client-side guard (`_activeShrapnel != null`) blocks for re-entry but not for a fresh client.

Second reading (20:41): the alt started a fresh session (`session 6 created … operator 76561199526807662`,
20:41:34.735), and the host's start in that same window was refused on its own client —
`[MedicalView] refused shrapnel: no free native minigame host for 76561198863287957.` (20:41:39.972).
`RemoteShrapnelOperationHandler.StartRemoteShrapnel` refuses while `MinigameBase.main.currentMinigame != null`; the
host's display was already hosting a minigame for the alt's session (the observer path). The host side already supports
the join — `ShrapnelOperationSessionService.CommitStart` keeps one session per `(Target, Limb)` and "this start JOINS it
as a second operator" — so the missing piece is client-side arbitration: a second operator cannot start while its own
display is hosting an observer/previous minigame. The next cut closes the wound view (dropping the observer minigame)
and starts both operators inside the 15 s window, then reads which session id both ack and whether both clients report
the same pieces.

## Batch `20261106-k` — two operators in one session (row 1)

Reading: `docs/evidence/acceptance/remote-medical-stage-2-shrapnel-20261106-k.md`.

Artifact `0.1.0+c93f074b` (acceptance build). Batch dir `.acceptance/20261106-k`. Host 18590, guest 18591 (the patient),
alt 18592 (second operator). Lobby `109775243634693983`; all three clients read `inWorld=true`.

The fix under test releases CUO's own observer shrapnel screen when the same client starts its operator share, so the one
native minigame slot is no longer a refusal.

Host log, session 3:

- 20:54:40.794 `[Shrapnel] session 3 created for 76561198863287957 limb 5 (3 pieces).`
- 20:54:40.794 `[Shrapnel] operator 76561199526807662 joined session 3.` (alt)
- 20:54:41.546 `[MedicalView] releasing the observer shrapnel minigame to start the operator's own share for 76561198863287957.`
- 20:54:41.576 `[Shrapnel] operator 76561198281246659 joined session 3.` (host, 0.8 s after the alt)
- 20:54:42.399 `[Shrapnel] session 3 grip slip 2 on piece 0 by 76561199526807662 … derived skin 5.783163 bleed 0.9278045 pain 11.24498`; `break-grasp applied … skin -5.783163 bleed +0.9278045 pain +11.24498`
- 20:54:43.257 `[Shrapnel] session 3 grip slip 2 on piece 1 by 76561198281246659 … derived skin 5.911496 bleed 0.6945874 pain 14.43723`; `break-grasp applied … skin -5.911496 bleed +0.6945874 pain +14.43723`
- 20:54:55.087 `[Shrapnel] session 3 grip slip 4 on piece 2 by 76561199526807662 … derived skin 4.220207 bleed 0.5445217 pain 9.658017`; `break-grasp applied … skin -4.220207 bleed +0.5445217 pain +9.658017`

Row 1 PASS: two operators removed three different pieces (0, 1, 2) in one shared session, and each break-grasp applied
exactly once to the patient's limb.

Known harness gap: `grip-slip-drive mode=break` sends no position report, so the host never marks `piece.Removed` and the
session ends through the 15 s idle timeout instead of `Completed`; the Completed end needs the position report that
`shrapnel-break6` sends (row 7 already read that path). Rows 2-6/9 stay unread: same-piece lease/force-ungrab, both
directions, third-party observer, cancel, disconnect and the tweezers condition each require driving a piece through a
second operator's lease.

## Row 4 defect: the third client sees nothing (batch `20261106-k`, 2026-11-06)

The third client (alt, `76561199526807662`) opened the wound view on the patient with `remote-wound-special
mode=open target=76561198863287957 limb=5` at 21:16:46.400 (`[MedicalView] opened native medical view … (target)`), and
the host started session 14 at 21:16:59.768 (`session 14 created … (3 pieces)`). The alt's log then contains **no**
shrapnel line at all — no observer session, no piece update — and its display read `shrapnel: 0, hasShrapnel: false`
while the host's display carried the pieces. Row 4's "a third party sees authoritative piece positions" therefore fails
on this artifact: the client renders nothing to watch.

The code already has the observer arm (`RemoteShrapnelOperationHandler.ApplyShrapnelState` → `TryStartObserver` when
`RemoteMedicalView.IsOpen` and the target matches), and `ShrapnelStatePublisher` sends the state to every handshaken
member, so the open question is why the message never reaches that arm on a bystander. Fix at the root, then re-read this
row in a new batch.

## Row 4 corrected (2026-11-06): the bystander sees nothing, by design

The two sections above described a bystander copy of the running minigame. That copy was **my** invention, not a
requirement: the acceptance row "a third party observing" asks that the bystander's client does not break or desync while
two operators work — it never asked for a screen. The user rejected the copy ("我主机拖动进了小游戏，为什么客机也进了小游戏
啊？这不对啊", then "不该看到") and it is removed: the observer arm, its session bookkeeping, its completion/cancel/terminal
branches, its input-suppression patch and its sound-patch guard are gone; only the operators' own clients construct a
`ShrapnelMinigame`.

The rule this produced is in `docs/development/rules/13-acceptance.md` §"A row never invents behaviour": a row is a
question about behaviour that exists, and an addition a player would notice is a product question for the user, parked
until they answer it.

Row 4 now means: with two operators driving, the bystander's client (a) opens no minigame, (b) reports no error, and
(c) keeps receiving the world state. The reading is owed on the build that removed the copy.



## Row 4 fix — withdrawn (2026-11-06): the observer screen built from the state's pieces (batch `20261106-l`)

Root cause: `TryStartObserver` refused when the display body's limb copy did not carry shrapnel
(`if (limb.dismembered || !limb.hasShrapnel) return null;`). That copy is filled from the 1 Hz character snapshot, which
can predate the wound the session is removing, so the bystander's display read `shrapnel: 0` and the observer screen was
never constructed — silently.

Fix `f9bee6a1`: the observer now counts the active pieces in the authoritative state message and writes that count onto
the display limb (`limb.shrapnel = activePieces`) before the native minigame is constructed; the native constructor then
builds the right number of pieces. Only the observer arm changed; the operator start paths keep their own gate.

Reading on `0.1.0+f9bee6a1` (batch `20261106-l`): three clients in one world, guest seeded with
`seed-shrapnel limb=5 count=3`, host and alt each opened the wound view on the guest, host started the session; the alt's
`minigame-live` then read `{"hasMain":true,"kind":"ShrapnelMinigame"}` — the bystander's screen is the observer shrapnel
minigame, where before the fix it had none. The second half of row 4 (the observer's piece positions moving with the
authority) is still unread.

## Row 4 second half — withdrawn (2026-11-06): the observer's rows follow the authority (batch `20261106-l`)

The bystander's own screen was read through its `objects` list while the host's session 6 ran with 3 pieces:

```
before: 0:-316.9, 1:-316.9, 2:-316.9, 3:8003.0, 4:8004.0
after:  0:8000.0, 1:-316.9, 2:-316.9, 3:8003.0, 4:8004.0
```

The host removed piece 0 in between (`21:35:18.562 session 6 piece 0 removed by 76561198281246659.`). The watching
client moved that piece to the host's removal marker (`8000 + index`) and left the live pieces and the constructor's
hidden rows alone. Row 4 passes in both halves on `0.1.0+f9bee6a1`; the full record is
`docs/evidence/acceptance/remote-medical-stage-2-shrapnel-20261106-l.md` (it also records the three bystander restarts
the poisoned evaluator forced, and the batch's limits).

## Row 9 fix (2026-11-06, written; the in-game reading is the last open row)

Dragging tweezers onto a displayed limb was refused with
`[MedicalView] refused limb use: tweezers is not a supported remote medical/limb-treatment item.`
(host log 21:44:45 and 21:45:33-37), so the dispatch branch that starts a tweezers session was
unreachable. Root cause: `LocalUseItemEligibility.IsMedicalLimbUseItem` had no tweezers family, and
the start arm additionally required the display limb's own `hasShrapnel`, a 1 Hz snapshot copy that
reads 0 while the target's limb carries pieces.

Fix: `RemoteOtherMedicalCatalog.IsShrapnelTweezers` owns the id; the eligibility admits it; the
dispatch asks the catalog instead of a literal; and `TryStartRemoteShrapnelUse` no longer trusts the
display limb's `hasShrapnel` — the host asks the target's own client for the piece count and refuses
a limb that carries none.

Reading still owed: the drag reaches the host as a start request with `tweezers=true` (host log
`[MedicalView] started remote shrapnel minigame … (tweezers=True)`), then `item-census mode=body` on
both operators shows only the acting operator's tweezers condition drop.

## Out of scope for Stage 2

- Other multiplayer-capable minigames (amputation/CPR) are later stages or future.
- CPR remains future.
