# Acceptance record — Guest carried container contents appear as world drops on the host view

- Ticket: `guest-container-contents-ghost-drops-on-host` — verdict: **moves to `done/`**
  (rows 1–2 stand from batch `20260930-f`; row 3 **pass** in this batch — the reconnect now
  hands the container back WITH its contents)
- Batch: `20260930-h` — the reconnect single-point session on the fixed build
- Commit: `be0555a4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+be0555a42754e62fea93582b1dac37956b0207f3` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-30 23:52 → 23:58 (+08:00) · Host: physical machine (evaluator `18590`) ·
  Guest: primary sandbox (evaluator `18591`; relaunched once) · Third peer: not used
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the probe JSON cited below by name under the batch directory, plus the two clients'
  logs. Setup substitution (declared): the container was built through the committed recipes —
  `item-provide type=trashbag mode=create`, `item-provide type=dogfood mode=create`,
  `container-fill container=trashbag item=dogfood` — never by hand.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | No dog food appears as a world drop on the host while the guest carries the trash bag | machine | not re-run — batch `20260930-f` verdict stands | — |
| 2 | The guest view and the host view show the dog food inside the container consistently | visual | not re-run — batch `20260930-f` verdict stands | — |
| 3 | No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection | machine + visual | **pass** | **Preconditions** (`h7-*`): the guest's body carried the trash bag `10156476526` (slot 0) with the dog food `14451443822` inside it plus the emergency light `5861509230` (slot 3); the host's transfer table held the bag WITH `contents=[14451443822]`, and the kernel held the dog food as `Contained(parent=10156476526)` — the restore's input was complete and nothing was cleared. **Reconnect**: the guest quit (`h8-guest-quit.json`, process gone) and was relaunched into the same lobby without any offline clear; the rejoin re-activated (6 `IdWatermark` grants, 20 `Carried inventory of …` frames in the host log) and entered the world. **After the rejoin** the guest's body read back as the SAME tree, stable across three reads 6 s apart (`h11-guest-local-1..3.json`): bag in slot 0 **with `children=dogfood:14451443822`**, dog food `parent=trashbag`, light in slot 3. The host's authoritative tables were unchanged and duplicate-free (`h12-host-tables.json`): `tableCount=2` (light, bag — the bag still `contents=[14451443822]`), kernel `carried=2`, `contained=1` (dog food → bag), `terminal=0`, `world=253`. The host's view of the guest's proxies was complete (`h12-host-clone.json`): 3 proxies, each `underClone=true`, `orphanCount=0`. Both logs carry **no `Conflict` and no `rejected`** anywhere in the run, and the new merge line names the path that ran — `Merged 2 transfer-table items onto the restore of … (0 appended — the snapshot did not carry them; 0 unplaceable; 2 items total)` — with no `cannot place` warning. Contrast, same scenario on the pre-fix build (batch `20260930-g` control): `trashbag … children: []` with the dog food gone and the kernel's containment flattened. |

## Residuals for the user

- None: every reading this run could decide is machine- or log-judged.

## Limits

- Rows 1–2 were not re-run: the display/domain-boundary mechanism they judge is untouched by this
  change, and batch `20260930-f`'s panel frames remain their evidence.
- No third peer in this batch: the third-party view of the restored container is read from the
  host's proxy list (`mode=clone`), not from a third client's screen.
- The visual half of row 3 is the local item tree plus the host's proxy tree, not a captured frame;
  the frame reading stands for rows 1–2 (`20260930-f`).
- One session does not disprove a rare race, and the batch-f P2P wedge remains an unreproduced
  observation — this batch neither reproduced nor addressed it.
- A content moved into a container AFTER the guest's last 1 Hz report is a named loss on the merge
  path (`unplaceable`, ticket *Limits*); this run's precondition had the content inside the
  snapshot, so that branch did not fire.
