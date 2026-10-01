# Acceptance record — Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Ticket: `block-damage-table-capacity-alignment` — verdict: **stays in `review/`** (row 3 passes in this batch; row 2 passes from batch `20261001-y`; row 1's late-joiner half is still not driven)
- Batch: `20261002-b` — tickets `unhooked-damage-block-callers`, `block-damage-table-capacity-alignment`
- Commit: `9c1b8ae8` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+9c1b8ae89c27609f9d31bf23bc730dc49119bfbd`
- Run: 2026-10-02, 00:35–00:50 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `b-crush-evidence.txt` (the excerpted lines of both crushes, all three clients), `probe-crush-place.cs`, `probe-crush-read.cs`, `b-crush4-host-place.json`, `b-crush5-guest-place.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Long run, more damaged cells than the cap: host, every connected guest and a late joiner hold the SAME damaged-cell set | machine | **unproven** | the connected-peer half stands from batch `20261001-y` (identical 128-row sets after two over-cap fills); the late joiner was not driven in this batch — the capacity family was deferred, see `20261002-b-scope.md` for the exact next steps |
| 2 | Eviction: a cell leaving the table leaves it on every side | machine | **pass** | batch `20261001-y` (not re-run; the change set under acceptance is unchanged since it) |
| 3 | Unhooked writers: damage written by the `DamageBlock` callers CUO does not hook reaches the shared view, or the gap is named | machine | **pass** | the live half — the footstep crush — is now driven: the actor's roll reported (`origin=OnBlockDamaged event=Break` ×3 plus `FlushPendingBlockBreak`), the other two clients presented the same cells through the game's own damage roll, and the host answered the reporter's write reports (`b-crush-evidence.txt`; the rows themselves are in `unhooked-damage-block-callers-20261002-b.md`). The hook-anchor half is unchanged and its static gate passes |

## Limits

- Row 1's late-joiner half needs the world-entry delivery path; the capacity fills, the censuses and the
  leave→continue sequence are written down in `20261002-b-scope.md` as the next batch's opening steps.
- Row 3's pass is the crush's break reaching every side; it adds no surviving-partial-damage case, because
  the staged crush always breaks (see the sibling record's limits).
