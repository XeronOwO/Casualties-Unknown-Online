# Acceptance record — The archive's two id carriers can disagree about where one id is

- Ticket: `id-carriers-can-disagree-about-placement` — verdict: **moved to `done/`** (all five rows pass;
  the pre-fix red was observed in the earlier probe batch `20261022-b`).
- Batch: `20261022-c` — this ticket. A three-client session (host + two sandboxed members) served the
  pickup window, a continue, the drop window and a second continue.
- Commit: `d8bb0a16` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+d8bb0a164c01ff1f2a9bf0df210d59e42555b4cf` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-22 19:48 → 19:52 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>` (three clients).
- Dependencies: the preflight read `11 present`; the prelude's `session-environment (ensure-cuo) exit=0`.
- Artifacts: `20261022-c/` under the directory named by `acceptance-artifacts-dir` — the prelude
  (`prelude.log`, `prelude-verify-deploy.txt`), the row logs (`accept.log`, `accept-result.json`), the
  per-row driver answers (`r1a1-*`, `r2-*`, `r3-*`, `r4-*`), the archive copies (`r1-pickup-cut-*`,
  `r3-drop-cut-*`), the host log windows naming the corrections (`r1a1-host-log.txt`, `r3-host-log.txt`),
  the stored-character probes (`r5-store-store.json`, `r3-store-store.json`), and the body/kernel reads
  (`r2-body-body.json`, `r2-kernel-kernel.json`, `r4-body-body.json`, `r4-kernel-kernel.json`). The red's
  artifacts are in
  `20261022-b/`: `p1-red-pickup-items.json`, `p1-red-pickup-character.json`, `probe.log`,
  `red-host-window.txt`, `red-guest-window.txt`.

## The red, before the change (batch `20261022-b`, deployed `0.1.0+7b9ddfe0`)

Inside the member's report window (the host's incoming `CharacterData` family parked for the attempt;
the member created and carried a `bandage`, the host took `/save`), the archive stored one id in two
places: `items.json` said `location.kind = 2` (`Carried`, owner = the member) with
`data.slotIndex = -1`, while `characters/steam-<member>.json` carried no row for it
(`p1-red-pickup-items.json`, `p1-red-pickup-character.json`). The same probe showed the host's stored
character and the clone presentation sharing one object: a 2-item report was stored at 19:11:51.740, the
drop's `[CarriedSync] removed <id> from <peer>'s snapshot` mutated it, and the 19:11:54 cut wrote a
1-item file (`red-host-window.txt`).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The fixed build, the member creates+carries an item and the host cuts inside the report window | machine | **pass** | `r1a1-create.json` `ok:true id:<item> slot:0`; the host parked the family (`r1a1-freeze-arm.json` `armed:true`) and released it after the cut (`r1a1-freeze-off.json` `dropped:8`); `r1-pickup-cut-items.json` row `location.kind=2`, owner = the member, `data.slotIndex=-1`; `r1-pickup-cut-character.json` carries the item with `slotIndex=-1`; `r1a1-host-log.txt` — `Cut Command character placement: added item <id> (bandage) at no body slot (the body places it)` and `reconciled the member-item placement: 1 item(s) the character files were missing and 0 their stale copy` |
| 2 | Continue the cut and let the member receive its character | machine | **pass** | `r2-leave.json` (`inWorld:false`), `r2-continue.json` (`ok:true`), all three clients back `inWorld` (`state-host.json`, and the guest/alt reads in `accept.log` at t+22.1 s); `r2-body-body.json`: the member's body carries the item exactly once (local count 2, one row for the id); `r2-kernel-kernel.json`: kernel `carried` holds it once and `world` holds it zero times |
| 3 | The member drops the item and the host cuts inside the window (the store still carries it) | machine | **pass** | `r3-drop.json` `ok:true slot:0 dropped:true`; `r3-store-store.json` still carries the item right before the cut (`count:2`, the item at slot 0); `r3-drop-cut-items.json` row `location.kind=1` (World); `r3-drop-cut-character.json` has no row for the id; `r3-host-log.txt` — `removed item <id> (bandage) (the kernel holds it in the world)` and `0 item(s) the character files were missing and 1 their stale copy` |
| 4 | Continue the drop cut | machine | **pass** | `r4-continue.json` (`ok:true`), all three back in world; `r4-body-body.json` has no row for the id (onBody=0); `r4-kernel-kernel.json` holds it in `world` exactly once (world=1) |
| 5 | The host's stored character after the clone presentation removed a carried item | machine | **pass** | `r5-store-store.json` after the pickup cut: `count:2` with the item at slot 0 (the presentation's own copy was mutated, the store's was not); `r3-store-store.json` after the drop's carried-sync: still `count:2` with the item, so the next cut had to correct the file (row 3) |

## Residuals for the user

None: every row is machine-read.

## Limits

- **The window was held open, not raced.** The host's incoming `CharacterData` family was parked by the
  acceptance-only `net-drop-message` recipe for the attempt. The natural window — up to one 1 Hz report —
  is what the code already has (`Body.PickUpItem`/`DropItem` report the item fact and do not fire
  `OnInventoryChanged`); the probe stretched it instead of claiming a race was won.
- **The layer-end world-removal exception was not staged.** A descent would have to drop an item and
  descend inside the report window; the exception is recorded in the format page and the ticket, and the
  add half is unit-covered (`ALayerEndCutReconcilesTheMemberFileToo`).
- **The unplaced refusal path was not staged** (a restored body whose every free slot refuses the item):
  it destroys the copy and names the item in the log; staging it needs a deliberately full body.
- **IP-direct and the shared-key owner mapping were not exercised**; both are code-read only.
- The window reads are `machine` rows; no frame judgement was needed for this ticket.
