# Acceptance record — A container child moved into a slot loses its container fact when the pick-up cancels the departure

- Ticket: `container-content-event-gap-on-repick` — verdict: **pass, moved to `done/`**. The operator's
  release of a focused owner's container child onto an OCCUPIED and an EMPTY ring slot left the bag one
  child fewer on every client (the owner's tree, the host's kernel record, the third client's proxy tree),
  with the child in the destination slot and **zero** `[CharSync] divergence` on every client over each
  gesture's window. The same run drove the owner-local control the ticket asked for — the owner moving the
  same kind of child into a slot locally — and it is clean too, so the gap was the missing arrival event,
  not the intent replay.
- Batch: `20261025-b` — tickets `container-content-event-gap-on-repick`,
  `pickup-early-return-kernel-relocation` (its record is
  `docs/evidence/acceptance/pickup-early-return-kernel-relocation-20261025-b.md`).
- Commit: `23a3cdb0` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+23a3cdb0174a49b0baec12c1a9ecfe4a2ef262e0` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-20 21:48 → 21:56 local · Host: physical machine · Guest: sandbox `<sandbox-guest>` ·
  Third client: sandbox `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`preflight.log`: `RESULT: OK - a full two-client
  run is possible; the alternate third client is configured`).
- Artifacts: `20261025-b/` under the directory named by `acceptance-artifacts-dir` — the prelude reads
  (`prelude.log`, `session.json`, `prelude-verify-deploy.txt`, `preflight.*`), the fixture reads `c1*`–`c4*`,
  the row reads `a1*` (control, the bag's report, the slot move), `a2*` (the wear, the sibling ticket),
  `b1*` / `b2*` (the two rows), and the per-row log excerpts `ev-*`.

## The fixture

The member's body was given a `trashbag` (slot 0), a `dogfood` (slot 1), a `bandage` (slot 2) and an
`autopump` (slot 4) through the game's own grant, beside the `emergencylight` it already carried (slot 3)
(`c2-guest-create-*.json`, `c2-guest-local.json`); the dogfood and the autopump were loaded into the bag
through `Container.LoadItem` (`c3-guest-fill-*.json`). The baseline is the bag holding two children on the
host's kernel record (`13787924117<-9492956821`, `22377858709<-9492956821`,
`c4-host-table-before.json`), on the host's clone and on the third client's clone (`parent=trashbag` for
both children in `c4-host-clone-before.json` / `c4-alt-clone-before.json`).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The operator releases the focused owner's container child (`bandage`) onto an OCCUPIED ring slot: the child ends in that slot, the occupant leaves to the world, and the bag reads one child fewer on every client | machine | **pass** | `b1-host-release.json`: `castSlot: 3`, `castItem: emergencylight` (the owner's proxy), `castOverlaps: true`, `focusedSteamId` = the owner, `calls: none`; `ev-b1-guest.txt`: `[ContainerUnload] bandage (id 18082891413) left its container into the world from the CarriedContainer side`, the take-out `converged on its first report`, `[SlotMoved] bandage (id 18082891413) → slot 3 (Drag) reported.`, `[PickUpResult] bandage → slot (slot 3)`, `[RemoteIntent] item bandage now holds the owner's slot 3`, `[ItemDropped] emergencylight (id 5197989525)`; `b1-guest-local.json`: bandage `slot: 3`, the bag's `children: []`; `b1-host-table.json`: the bag's contents `[]`, `containedCount: 0`, `terminalCount: 0`, the emergencylight a world row; `b1-host-clone.json` / `b1-alt-clone.json`: no proxy is parented to the bag any more |
| 2 | The same release onto an EMPTY ring slot: the child ends in that slot and the bag reads one child fewer on every client | machine | **pass** | `b2-host-release.json`: `castSlot: 5`, `castItem: none`, `castOverlaps: true`, `focusedSteamId` = the owner, `calls: none`; `ev-b2-guest.txt`: the same chain (`[ContainerUnload] dogfood (id 13787924117) … from the CarriedContainer side`, take-out `converged on its first report`, `[SlotMoved] dogfood → slot 5 (Drag) reported.`, `[PickUpResult] dogfood → slot (slot 5)`, `[RemoteIntent] item dogfood now holds the owner's slot 5`); `b2-guest-local.json`: dogfood `slot: 5`, the bag's `children: []`; `b2-host-table.json`: the bag's contents `[]`, no contained or terminal row; `b2-host-clone.json` / `b2-alt-clone.json`: no proxy under the bag |
| 3 | Zero `[CharSync] divergence` for the bag and the child on every client over each gesture's window | machine | **pass** | the host processed four post-gesture snapshots of the owner per row with no warning (`ev-b1-host.txt`, `ev-b2-host.txt`, and the post-event windows 21:53:35–38 / 21:54:06–08), the third client logged none (`ev-b1-alt.txt`, `ev-b2-alt.txt`) and the owner never compares its own snapshot; both viewers applied the carrying event (`[CarriedSync] applied bandage …` at 21:53:34.256 / .259 on the host and .301 on the third client; `[CarriedSync] applied dogfood …` at 21:54:05.122 / .123 and .137 / .139) |
| 4 | The owner-local control the ticket asked for: the owner moves the same kind of child out of the bag into a slot locally, and the viewers read the same clean change | machine | **pass** | `a1-guest-pickup.json`: `mode: pickup`, `applied: true`, `wasContained: true`, `parentBefore: trashbag`, `slotAfter: 1`; `ev-a1-guest.txt`: `[ContainerUnload] dogfood … from the CarriedContainer side`, the take-out `converged`, `[SlotMoved] dogfood → slot 1 (Drag) reported.`, `[PickUpResult] dogfood → slot (slot 1)`; `a1-guest-local.json`: dogfood `slot: 1`, the bag's `children: [autopump/22377858709]` (one fewer); `a1-host-table.json`: the dogfood carried by the owner, the bag's contents one child; `a1-host-clone.json` / `a1-alt-clone.json`: the child top-level, not inside the bag; zero divergence on both viewers (`ev-a1-*`) |

## The chain the run read

The owner's unload opened the departure with its home named (`CarriedContainer`), the re-pick cancelled it
(`[ItemTrace] … result=Cancelled events=[RePick]`) and the arrival was stated before the slot fact — the
take-out command was sent and answered (`… awaits the host's verdict` → `converged on its first report`),
then `[SlotMoved] … reported`. The host's kernel applied the relocation and its batch projection published
the child's carried fact to both viewers, which applied it and pruned the child out of the bag's nested
contents. The bag's own report taken after the gesture (`a1b-*`) charges the child no more: no
`[ContainerSync] … no longer names` line and no terminal row (`a1b-host-table.json`).

## Limits

- One world, one run, one repetition per row; the zero-divergence windows are 3–4 s each (three or more
  1 Hz snapshots on the host), which is the periodic cycle of the mechanism that can emit the warning,
  not a statistical claim.
- Two sibling gestures driven for the other ticket still show the monitor's in-flight pair, because their
  caller posts the character snapshot before the item report: the radial wear out of the bag (`a2-*`) and
  the `SwitchHands` slot move (`a1c-*`). The event itself is reported and applied 1–3 ms later
  (`[CarriedSync] applied …` on both viewers), so this is not the gap this ticket closes; it is filed as
  `docs/backlog/review/snapshot-overtakes-item-reports.md`.
- An unrelated `[Enemy] generation spawn pairing failed (71 host vs 73 guest generated enemies …)` warning
  appears once on the owner and once on the third client after the rows; it is outside this ticket and is
  not judged here.
