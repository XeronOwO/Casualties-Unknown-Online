# Shrapnel, two operators in one session — batch `20261106-k`

Artifact: `0.1.0+c93f074bc6f92695f482fa7da546782aebc1d9af` (acceptance build; the fix that lets a start replace CUO's
own observer shrapnel screen).
Batch directory: `.acceptance/20261106-k`.
Clients: host 18590 (`76561198281246659`), guest 18591 (`76561198863287957`, the patient), alt 18592
(`76561199526807662`, the second operator). Lobby `109775243634693983`; all three clients read `inWorld=true`.

## Fixture

- The guest's hand carried three shrapnel pieces: `seed-shrapnel limb=5 count=3` → `{"shrapnel":3,"hasShrapnel":true}`.
- Both operators opened the game's own wound view on the patient (`remote-wound-special mode=open`) and started the
  special action (`mode=special`), the alt first and the host inside the same 15 s window.

## Reading (host log)

Session 3:

- 20:54:40.794 `[Shrapnel] session 3 created for 76561198863287957 limb 5 (3 pieces).`
- 20:54:40.794 `[Shrapnel] operator 76561199526807662 joined session 3.` (alt)
- 20:54:41.546 `[MedicalView] releasing the observer shrapnel minigame to start the operator's own share for 76561198863287957.`
- 20:54:41.576 `[Shrapnel] operator 76561198281246659 joined session 3.` (host)
- 20:54:42.399 `[Shrapnel] session 3 grip slip 2 on piece 0 by 76561199526807662 … derived skin 5.783163 bleed 0.9278045 pain 11.24498`; `break-grasp applied … skin -5.783163 bleed +0.9278045 pain +11.24498`
- 20:54:43.257 `[Shrapnel] session 3 grip slip 2 on piece 1 by 76561198281246659 … derived skin 5.911496 bleed 0.6945874 pain 14.43723`; `break-grasp applied … skin -5.911496 bleed +0.6945874 pain +14.43723`
- 20:54:55.087 `[Shrapnel] session 3 grip slip 4 on piece 2 by 76561199526807662 … derived skin 4.220207 bleed 0.5445217 pain 9.658017`; `break-grasp applied … skin -4.220207 bleed +0.5445217 pain +9.658017`

Before the fix the same sequence was refused on the second operator's client with
`[MedicalView] refused shrapnel: no free native minigame host for 76561198863287957.` (batch `20261106-i`, 20:41:39.972).

## Same-piece contention (row 2)

The first attempt used `grip-slip-drive mode=break`, which grabs and breaks in one call, so ownership was released
before a second client could contend. A dedicated instrument was added for this row:

- `tools/acceptance/driver/eval-declarations/shrapnel-lease.cs` (`AcceptanceShrapnelLease`) sends one ownership report
  and stops; modes are grab, hold, release and status.
- `tools/acceptance/recipes/shrapnel-lease.cs` (`mode=s piece=n`) drives it and reports `ok` + `result`.

Reading (session 4, host log):

- 20:59:17.343 `[Shrapnel] session 4 created for 76561198863287957 limb 5 (3 pieces).` / `operator 76561199526807662 joined session 4.`
- 20:59:18.186 `[MedicalView] releasing the observer shrapnel minigame to start the operator's own share for 76561198863287957.`
- 20:59:18.218 `[Shrapnel] operator 76561198281246659 joined session 4.`
- 20:59:19.904 `[Shrapnel] session 4 rejected non-owner move on piece 0 from 76561198281246659 (owner 76561199526807662).`
- 20:59:38.299 the same refusal again after the first operator re-joined.

The host refuses the second operator's grab and the piece keeps its owner. The user's decision (2026-11-06) keeps
that behaviour: a piece stays with the operator who took it, and a second grab is refused rather than forcing the
piece out of the owner's hand. Row 2 therefore passes as shipped, with this reading as its evidence.

## Both directions on one piece (row 3)

Session 5 (host log, 21:06): the alt created the session and the host joined it, then the host grabbed piece 1 and the
alt tried the same piece:

- 21:06:10.877 `[Shrapnel] session 5 created for 76561198863287957 limb 5 (3 pieces).` / `operator 76561199526807662 joined session 5.`
- 21:06:13.237 `operator 76561198281246659 joined session 5.`
- 21:06:16.038 `session 5 rejected non-owner move on piece 1 from 76561199526807662 (owner 76561198281246659).`

Together with session 4 (20:59:19.904, alt owned piece 0 and the host was refused), both directions are read: whichever
operator takes a piece first keeps it, and the other operator's grab on that piece is refused on both clients' behalf.
Row 3 passes; the refusal is the user's chosen behaviour (row 2's decision).

## Cancel mid-removal (row 5, partial)

Closing an operator's own wound view (re-opening it on the same client, which is how a player leaves the panel) is the
cancel path. Host log:

- 21:07:54.749 `session 6 created … (3 pieces)` / `operator 76561199526807662 joined session 6.`
- 21:07:57.367 `operator 76561199526807662 left session 6 (Cancelled).` / `session 6 terminal Cancelled.` — the session
  had no other operator, so it ended with the reason `Cancelled`.
- 21:08:09.029 `session 7 created … (3 pieces)`; 21:08:11.287 the host joined; 21:08:17.173 the alt left
  `session 7 (Cancelled)` and **no terminal line followed** — the host stayed in the session and it kept running.

So the cancel path is real and the session survives a single operator leaving. What this run did NOT show is the other
half of the row: no piece had been removed before the cancel (the break call did not land in this window), so "already
removed pieces stay removed" is still unread.

## Disconnect while holding (row 6)

Session 8: the alt created the session and the host joined it, the alt grabbed piece 0, and then the alt's client quit
(the driver's `quit`, the same path a player closing the game takes). Host log:

- 21:09:05.802 `session 8 created … (3 pieces)` / `operator 76561199526807662 joined session 8.`
- 21:09:08.091 `operator 76561198281246659 joined session 8.`
- 21:09:17.659 `operator 76561199526807662 left session 8 (Disconnected).`

The host released the departing operator's participation with the reason `Disconnected`; no terminal line followed, so
the session stayed alive for the remaining operator. The host then grabbed the same piece 0 and **no**
`rejected non-owner move` line appeared, which is the release: the departed operator's lease no longer blocks the piece.
Row 6 passes.

## Cancel mid-removal, removed pieces stay removed (row 5, second half)

With the guest at three pieces and the host alone in the session, one piece was removed and the session cancelled
inside the 15 s idle window:

- 21:11:19.665 `session 10 created … (3 pieces)`; 21:11:23.979 `session 10 piece 0 removed by 76561198281246659.`
  (the limb's saved state then read `shrapnel=2 skin=78.553 bleed=4.128`).
- Session 10 ended by the idle timeout before the cancel; session 11 started with `(2 pieces)` — the removed piece
  stayed gone. Then, inside one driver round trip: 21:12:04.058 `session 11 created … (2 pieces)`,
  21:12:04.836 `session 11 piece 0 removed`, 21:12:06.094 `session 11 terminal Cancelled.`
- After that cancel the saved limb read `shrapnel=1 skin=78.553 bleed=4.92` — the piece removed during the cancelled
  session did not return, and the health effect of the break stayed.

Row 5 passes in both halves: an operator leaving ends its own participation (`Cancelled`), and pieces already removed
stay removed.

## Verdict

- Row 1 PASS: two operators removed three different pieces (0, 1, 2) in one shared session; each break-grasp applied
  exactly once to the patient's limb.
- Row 7 already passed on `0.1.0+39300598` (batch `20261106-h`).

## Limits

- `grip-slip-drive mode=break` sends no position report, so the host never marks `piece.Removed` and session 3 ended
  through the 15 s idle timeout, not `Completed`; the Completed end needs the position report that `shrapnel-break6`
  sends (row 7's reading).
- Rows 2-6/9 stay unread: same-piece lease/force-ungrab, both directions, third-party observer, cancel, disconnect and the
  tweezers condition each require driving a piece through a second operator's lease.
