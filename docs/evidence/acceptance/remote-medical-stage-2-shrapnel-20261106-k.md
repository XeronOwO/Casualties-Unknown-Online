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

The host refuses the second operator's grab and the piece keeps its owner. The ticket's row 2 wording expects the other
operator to be "force-ungrabbed"; the implementation refuses the move instead. **This divergence needs a decision**
(refusal is the shipped behaviour; a force-ungrab would be a new interaction) before row 2 can be marked pass.

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
