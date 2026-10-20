# Acceptance record — A pending-drop pickup reports only a slot re-home, so a contained kernel record can survive

- Ticket: `pickup-early-return-kernel-relocation` — verdict: **pass, moved to `done/`**. The member took a
  bag child out onto a ring slot, then put another item INTO the bag; the bag's container report
  (`ItemContainerSync`) did not charge the child, the host's kernel record stayed `Carried` (no
  `[ContainerSync] … no longer names` line, no terminal row), and a later slot move of the child was
  accepted rather than refused. The same run drove the radial wear of a bag child: it landed on the limb
  and left the same clean record. Both gestures were driven in process on the owner's body
  (`Body.PickUpItem` / `Body.WearWearable`), the shape the ticket names because the driver has no mouse.
- Batch: `20261025-b` — tickets `container-content-event-gap-on-repick` (its record is
  `docs/evidence/acceptance/container-content-event-gap-on-repick-20261025-b.md`),
  `pickup-early-return-kernel-relocation`.
- Commit: `23a3cdb0` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+23a3cdb0174a49b0baec12c1a9ecfe4a2ef262e0` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-20 21:48 → 21:56 local · Host: physical machine · Guest: sandbox `<sandbox-guest>` ·
  Third client: sandbox `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`preflight.log`).
- Artifacts: `20261025-b/` under the directory named by `acceptance-artifacts-dir` — `a1*` (the re-pick),
  `a1b*` (the bag's report and the child's survival), `a1c*` (the slot move after it), `a2*` (the radial
  wear), the log excerpts `ev-a1*` / `ev-a1b*` / `ev-a1c*` / `ev-a2*`, and the fixture reads `c1*`–`c4*`.

## The fixture

The member's body carried a `trashbag` (slot 0) with a `dogfood` inside it and an `autopump` loaded beside
it through `Container.LoadItem` (`c2-guest-create-*.json`, `c3-guest-fill-*.json`); the `bandage` in slot 2
was the item the bag's next report loaded after the re-pick. The baseline kernel record has the bag with
two children (`c4-host-table-before.json`).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The member takes a bag child out onto a ring slot, then puts another item INTO the bag: the host shows no `[ContainerSync] … the report for <bag> no longer names …` for that child, and the child's record is not terminal | machine | **pass** | `a1-guest-pickup.json`: `mode: pickup`, `applied: true`, `wasContained: true`, `parentBefore: trashbag`, `slotAfter: 1`; `ev-a1-guest.txt`: `[ContainerUnload] dogfood (id 13787924117) left its container into the world from the CarriedContainer side`, `[ItemTrace] … result=Cancelled events=[RePick]`, the take-out `converged on its first report`, `[SlotMoved] dogfood → slot 1 (Drag) reported.`, `[PickUpResult] dogfood → slot (slot 1)`; `a1b-guest-fill-bandage.json`: `loaded: true`, `contentsAfter: 2`; `ev-a1b-host.txt`: the host applied the bag's carried fact and logged no `no longer names` line; `a1b-host-table.json`: `13787924117` still `Carried` by the owner, contained only the bandage and the autopump, `terminal: []` |
| 2 | The child stays usable: a slot move of it after the bag's report is accepted, not refused | machine | **pass** | `a1c-guest-switch.json`: `ok: true`, `applied: true`; `ev-a1c-guest.txt`: `[SlotMoved] dogfood (id 13787924117) → slot 0 (Hands) reported.` and `[SlotMoved] trashbag (id 9492956821) → slot 1 (Hands) reported.`, no `[ItemReject]`, no `rejected` line; `a1c-host-table.json`: the dogfood still `Carried`, `terminal: []`; `ev-a1c-host.txt`: both carried facts applied (21:51:29.269 / .270) |
| 3 | The third client reads the child at its slot, not inside the bag | machine | **pass** | `a1-alt-clone.json`: the dogfood proxy is top-level (`parentType: ""`), the bag's proxy holds only the autopump (`parentType: trashbag`); `a1-host-clone.json` reads the same |
| 4 | The radial wear of a bag child lands on the limb and leaves the same clean record | machine | **pass** | `a2-guest-wear.json`: `mode: wear`, `applied: true`, `parentBefore: trashbag`, `parentAfter: UpTorso`; `a2-guest-census.json`: the autopump `origin: limb1`, `parent: UpTorso`, `wearable: true`; `ev-a2-guest.txt`: `[ContainerUnload] autopump (id 22377858709) … from the CarriedContainer side`, `[ItemTrace] … result=Cancelled events=[ReHomed]`, the take-out `converged on its first report`, `[SlotMoved] autopump (id 22377858709) → slot -3 (Wear) reported.`; `a2-host-table.json`: the autopump `Carried` by the owner, `terminal: []`, no `no longer names` line; `ev-a2-host.txt` / `ev-a2-alt.txt`: both viewers applied the carrying fact and logged no refusal |

## The chain the run read

Both gestures are the same native bracket: the container unload opens the departure with its home named
(`CarriedContainer`), the re-pick or the wear cancels it, and the arrival is stated as its own relocation
(`ItemContainerTakeOut`) before the slot / wear fact (`… awaits the host's verdict` → `converged on its
first report`). The kernel row leaves the bag for the owner's carried set instead of keeping the contained
row the bag's next report would have terminalized, which is what row 1 and row 4 read.

## Limits

- One world, one run, one repetition per row. Rows 1 and 4 are absence-and-state rows read from the
  gesture's log window (three or more 1 Hz snapshots on the host), not a statistical claim.
- The wear's window carries the clone-fact monitor's in-flight pair on both viewers
  (`[CharSync] divergence … nested container contents changed` / `a new carried item the fact table never
  saw`), and the `SwitchHands` move carries its slot pair. The carrying fact IS reported and applied
  1–3 ms later (`[CarriedSync] applied …`), so the record is clean in the sense this ticket means — no
  terminal record, no refusal — while the monitor's ordering false positive is filed separately as
  `docs/backlog/done/snapshot-overtakes-item-reports.md`. This ticket's own Limits already
  excluded the peers' clone-fact reading for the wear.
- The wound-view apply of a bag child (a splint out of a bag onto a limb) is the applied family's gap and
  was not driven: it stays ticketed as
  `docs/backlog/todo/container-child-limb-install-keeps-a-contained-record.md`.
- The send order (the arrival report before the slot fact) remains a design choice no test distinguishes;
  this run observed both reports in that order on every row and did not compare the reverse order.
