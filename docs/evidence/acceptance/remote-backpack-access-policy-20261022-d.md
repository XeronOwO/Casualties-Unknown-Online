# Remote backpack access policy — acceptance, batch 20261022-d

- Ticket: `docs/backlog/done/remote-backpack-access-policy.md`
- Commit: `cda1b5d29d1d546ddcadd1f85ffce720dbe51897`
- Deployed artifact: `0.1.0+cda1b5d29d1d546ddcadd1f85ffce720dbe51897` (`tools/verify-deploy.ps1` exit 0 on
  the deployed tree)
- Batch: `.acceptance/20261022-d` — three clients in one world (operator = the guest, owner = the host,
  third peer = the alt), one run; the prelude brought all three in and the host's rule was changed live
  with `/hostrules`. The record names the files in the local artifact area; the peer ids in the quoted
  log lines are written as roles (the batch logs carry the real values locally).

## Verdicts

| Row | Scenario | Verdict | Evidence of this run |
|---|---|---|---|
| 1 | Allow, owner conscious | **pass** | Guest opened the host's view: `r1-open.json` (`viewOpen=true`), probe `r1-view.json` (`isOpen=true`, `isReadOnly=false`, `canPickUp=111111`, `redButtons=0`). The guest's give/transfer under the same rule: `r1-give.json` ok (`slot=2`), host's `r1-host-items-after-give.json` carries the transferred item; host log `[Take] ... takes emergencylight ...` and `[PlayerInteraction] placed ... in local slot 2`. |
| 2 | UnconsciousOrDeadOnly, owner conscious | **pass** | After `/hostrules {"remoteBackpackAccess":"unconscious_or_dead"}` the guest's announced rule read `unconscious_or_dead` (`r2-guest-rules.json`) and the ALREADY OPEN view became read-only live: `r2-view.json` `isOpen=true, isReadOnly=true, canPickUp=111111, bodyButtons=6, redButtons=6, sample=1,0,0`; frame `r2-red-slots-2.png` (the displayed body inventory is open; the six body buttons are red). A release of the owner's proxy was refused locally: guest log `[RemoteIntent] refused the release of item … the remote backpack is view only under host rule RemoteBackpackAccess=unconscious_or_dead, so no intent was sent.`; the host's items before/after are identical (`r2-host-items-before.json` / `r2-host-items-after.json`). |
| 3 | UnconsciousOrDeadOnly, owner unconscious | **pass** | The host was forced down (`body-force`, `r3-host-force2.json`: `conscious=false`, `brainHealth=1`); the guest's open view was OPERABLE (`r3-view2.json`: `isOpen=true, isReadOnly=false, canPickUp=000000` — the clone's native answer for a downed body, not the patched one). The guest's give then committed: host log `[RemoteIntent] … GiveToBody …`, `[Take] <host> takes bandage … from <guest>`, `[PlayerInteraction] placed bandage (id 9492956821) in local slot 2`, and the kernel projection line; the owner's own unconscious slot then dropped it (`[ItemDropped]`), which is the game's own drop-on-unconscious rule, and the guest's `item-census` shows the host's item as a proxy (`r3-guest-items-after.json`). The frame's red slots under row 2 are the display; the native `canPickUp` is unchanged here, which is the M3 design point. |
| 4 | Deny | **pass** | After `/hostrules {"remoteBackpackAccess":"deny"}` the open view closed live: guest log `[BackpackView] closing … the host rule became RemoteBackpackAccess=deny`; `r4-view.json` `isOpen=false`. A direct open was refused: `[BackpackView] refused open … RemoteBackpackAccess=deny` (`r4-open.json`). The Players page control list has NO `member.open_backpack.<peer>` entry under deny (`r4-state.json`). The give intent was refused by the host with the rule named: `[RemoteIntent] refused GiveToBody: host rule RemoteBackpackAccess=deny forbids all remote inventory manipulation.` and the guest received the sentence (`r4-give.json`). |
| 5 | Solo / no session | **pass** | The guests quit and the host left the lobby; the host then started a LOCAL run (`r5-host-state-solo.json`: `active=false, role=None, inWorld=true`). A local grant and read worked with no policy in the path: `r5-host-local-item.json` (`create` ok, `picked=true`, slot 0), `r5-host-local-census.json` lists the created bandage and the carried emergency light; `r5-host-rules.json` reports `sessionActive=false`. |
| 6 | Guest's local config differs from the host's | **pass** | The guest's own BepInEx config reads `RemoteBackpackAccess = allow` (read from the guest's tree), while the host announced `unconscious_or_dead` then `deny`; the guest's `host-rules-read` reports the HOST's value (`r2-guest-rules.json`, `r4-guest-rules.json`, `announced=true`) and every refusal names that value. |
| 7 | Give: conscious receiver refused / unconscious receiver works | **pass** | Conscious receiver (before the force): the guest's give was captured and the host refused it — `[RemoteIntent] refused GiveToBody: <host> is conscious/alive; host rule RemoteBackpackAccess=unconscious_or_dead allows operating only an unconscious or dead player's backpack.` — and the guest received the same sentence in `InventoryIntentRefused`. Unconscious receiver: the same give committed (row 3's `[Take]` + `placed` lines). |
| 8 | Live rule change (allow → middle → deny) while a view is open | **pass** | The view opened under allow (row 1), became read-only on the middle announcement (row 2's `r2-view.json`), became operable when the owner went down (row 3's `r3-view2.json`), and closed on the deny announcement (row 4's `r4-view.json`), each transition with its own log line. |

## Limits and notes

- The row-2 red slots are judged from the machine read of the six button colours (`redButtons=6`,
  `sample=1,0,0`) plus the frame `r2-red-slots-2.png`; the frame was inspected by the agent and shows the
  displayed body's inventory open, while the small slot fill is more legible in the probe than in the
  1298x767 frame.
- Row 3's "unconscious" window is short by the game's own design: the forced body's consciousness rises
  again at the native rate, so the operation was driven immediately after the force and the host's own
  log is the commit evidence. The item was then dropped by the owner's own unconscious-slot rule — the
  game's rule, not the access policy.
- The row-2 refusal was produced by an owner-side release of a proxy (any intent is refused in a view-only
  view); the take-into-the-opener's-body variant needs the double-Tab flow, whose release seam is guarded
  by the same dispatcher refusal and was exercised by the unit tests (`RemoteDragIntentDispatcher`'s
  `ReadOnlyDrag` path) rather than driven by the run.
- The alternate third client was present in the world for the whole batch but was not the operator in any
  row; the third-party view is the same member card every member sees (row 4's control list is the
  guest's own).
