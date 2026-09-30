# Acceptance record — Carried-inventory registration has no re-report

- Ticket: `carried-inventory-registration-re-report` — verdict: **moves to `done/`**
  (rows 1–2 stand from batch `20260930-f`; row 3 **pass** in this batch — with the host's
  per-guest table cleared offline, the rejoin rebuilt it exactly once per id through the
  registration path)
- Batch: `20261001-i` — the cleared-table reconnect single-point session
- Commit: `2303758b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+be0555a42754e62fea93582b1dac37956b0207f3` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-10-01 00:36 → 00:45 (+08:00) · Host: physical machine (evaluator `18590`) ·
  Guest: primary sandbox (evaluator `18591`; relaunched once for the reconnect half) ·
  Third peer: not used in this batch
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`; the primary sandbox carries no plugin
  shadow, so the guest read the physical deployment through
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON and the two
  clients' logs, cited below by name. Setup substitution (declared): the container was built
  through the committed recipes — `item-provide type=trashbag mode=create`,
  `item-provide type=dogfood mode=create`, `container-fill container=trashbag item=dogfood` —
  and the "swallowed registration" precondition is the evaluator's removal of the host's
  per-guest entry map, both as the ticket's earlier batches declared them.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest starting inventory; registration dropped → host converges; take arbitration works | machine | not re-run — batch `20260930-f` verdict stands | — |
| 2 | An id added after the first report → converges on the next report | machine | not re-run — batch `20260930-f` verdict stands | — |
| 3 | Reconnect while in world → table rebuilt exactly once | machine | **pass** | **Preconditions** (`i8`, `i9`): the guest's body carried the trash bag `5861509230` (slot 0) with the dog food `14451443822` inside it, plus the emergency light `10156476526`; the host's per-guest table held both ids (`countBefore=2`, the bag entry `contents=[14451443822]`, both `isTransferred=true` with kernel location `Carried`). **Offline clear** (`i10`, `i11`): the guest quit and no game process but the host remained, then the host's entry map was removed through the evaluator — `countBefore=2` → `countAfter=0` with the kernel facts untouched. **Rejoin** (`i12`, `i13`): the sandbox client was relaunched, joined the same lobby `109775243469342099` and entered the world (`inWorld=true`); the host's watermark handler saw the rejoin's watermark frames (`Item id watermark 1/2/3 from …`) and granted up to 3. **Rebuilt through the registration path** (`i16`, `i18`, host log): the table came back as exactly 2 entries — bag `5861509230` (`contents=[14451443822]`) and light `10156476526`, each `isTransferred=true`, kernel `Carried`, no duplicate id — and the host log carries exactly ONE registration line for the whole rejoin: `Registered 2/2 carried items of 76561199526807662 in the transfer table (0 already registered, 0 refused by the kernel).` The `0 already registered` is the cleared table's fingerprint: the registration path, not the kernel-rebuild path (batch `20260930-g` converged through that one), filled it. `ItemArbitration.RegisterCarried` logs at Information only when it inserts something, so one such line is this run's "exactly once"; the later reports are Debug-level no-ops (fourteen `Carried inventory of …` frames arrived in total). **The restore survived the cycle** (`i14`, `i15`, `i17`): the guest's body came back as the same tree and stayed stable across reads — bag in slot 0 with its `dogfood:14451443822` child, dog food `parentType=trashbag`, light in slot 3 — and the host's merge line names its input: `Merged 2 transfer-table items onto the restore of … (0 appended — the snapshot did not carry them; 0 unplaceable; 2 items total)`. **No conflict anywhere**: `Conflict`/`rejected` count is 0 in both logs and the host log carries no `[ItemBind]` warning. |

## Residuals for the user

- None: every reading this run could decide is machine- or log-judged.

## Limits

- The clear is the declared substitution for a really swallowed frame: the host's per-guest entry
  map is removed through the evaluator while the guest is offline (a clear while the guest is online
  is reverted by its own steady report — batch `20260930-g` cycle 1's lesson). The kernel facts were
  left untouched, so this run proves the table's rebuild, not a kernel-side repair.
- The rejoin's registration report is what opened the rebuild here, and it arrived BEFORE the host
  sent the restore: the guest's local body was captured (its own set) while the merge line still
  shows a 2-entry table. The "restore reads an EMPTY table" path therefore did not occur in this
  cycle — but the container's CONTENT still came from the snapshot, not the table: the table held
  only the two top-level ids, an entry states state and never contents or placement
  (`TransferTableRestoreMerge.TakeState`), and the merge appended nothing (`0 appended`,
  `0 unplaceable`).
- The mid-restore capture reports 3 items once (`i` host log line `Carried inventory of …: 3
  items.`) while the settled set is 2: the extra id is the content while it is briefly top-level,
  and it left no table entry (the table's two unique ids are read directly). Named because it is
  the only reading in this run that does not match the settled shape; it is not a divergence the
  arbitration seam sees.
- One session does not disprove a rare race; the batch-f P2P wedge remains an unreproduced
  observation, neither reproduced nor addressed here.
- Rows 1–2 were not re-run: they were judged in batch `20260930-f` and the mechanism they cover is
  untouched by this cycle's clearing (which is a precondition substitution, not a product change).
