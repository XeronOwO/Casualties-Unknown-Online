# Acceptance record — S3 mid-run consistent cut and world diff (batch 20261001-q)

- Ticket: `save-mid-run-consistent-cut` — verdict: **stays in `review/`** (row 3 keeps its `unproven`
  on the batch plan's named lockable gap, though this batch closed the fluids/enemies/restore halves of
  it; rows 2, 4, 5 and 6 pass, and rows 1 and 7 passed in batch `20261001-m`)
- Batch: `20261001-q` (Run D solo; Run E and Run E2 host + guest) — siblings
  `save-solo-menu-exit-trigger`, `save-run-clock-not-sent`, `save-new-player-starting-supplies`
- Commit: `2efca14b` (runs) · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:42 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Mined/placed/quaked blocks + partial damage | machine + residual | pass (batch `20261001-m`) | sibling record `save-mid-run-consistent-cut-20261001.md`; every cut of this batch re-carried the world-block rows (`f2-host-cut-line.txt`: 1786 world-block rows) |
| 2 | World items (ground/container/hand) | machine + named gap | **pass** | before the cut (`d2-kernel-before-cut.json`): ground `rosepod#1147077248963` World, `duffelbag#1348940711875`/`burger#1353235679171`/`scrapmetal#1357530646467` Carried; the cut's own files carry them (`characters/…json` 3 items, `items.json` World/Carried rows, verified against `live/` before the continue); after the two Continue cycles the same ids, positions and container tree come back (`d2-kernel-after-restore.json`, `d2-find-rosepod-after-b.json` id + (0, 473.017), `d2-tree-after-restore-b.json`), character restore `(3 items)`, kernel totals 317 → 317. The `worn` resting stays the ticket's named gap |
| 3 | Buildings/traps/fluids/enemies | machine + named gap | **unproven** (all four families and the restore now verified; opened lockables unreachable) | Run E2 staged all four in a live session and restored that cut: the cut carried `80 enemy row(s)` (79 live + 1 tombstone), `235 fluid chunk(s)`, `24 world-entity row(s)` = `building-health 20` + `trap-consumption 2` + `trap-state 2`, `330 item row(s)`, `1786 world-block row(s)`, 2 characters (`f2-host-cut-line.txt`); the Continue restored **that** world (`Continue restored world w-20261001-6986 … revision 913`, `f2-host-restore-account.txt`) and the account reads `1779 block-state row(s) written, 7 partial-damage row(s) applied, … 21 applied / 1 refused world-entity row(s), 1 row(s) not taken` with the shortfall named (`the live world did NOT take 1 of 22 restored world-entity row(s) — the regenerated layer has no such entity where the cut recorded it`), items `332 applied, 0 not taken`. Opened lockables have no drive path (the batch plan's declared gap), so the row keeps `unproven` |
| 4 | In-flight states | machine + partial | **pass**, limit named | `d6-kill-player.json` (the game's own `kill`), then the cut immediately after: `d6-host-log-kill-save.txt` shows the death dropping the carried container (`[ItemDropped] duffelbag (id 1348940711875) … container contents 2`, `origin=FlushPendingDrop result=Committed(1)`) and the cut committing at revision 333 with 317 item rows; the cut's `items.json` carries the dropped bag as a World item at (0.07, 473.4) — no silent loss. The deferral window itself had closed before the cut armed (0.9 s), so the deferral branch was not observed live; the cut report names the classes it never carries |
| 5 | Save → load → save → load | machine | **pass** | two in-session cut/continue cycles plus a fresh-process entry: every cycle restored the staged ids and the character restore applied 3 items (`d2-*-restore*`); the one-shot fingerprint is not re-armed by a Continue, so the second fingerprint came from a fresh client process (`d7-continue-fingerprint2.json`): both `[WorldFingerprint]` lines are identical — `12EFBA9839F8D246 95FED72355D92AEA 5DDD1F916F604803 2734A90A86993A99 FA2192CD5E988E91 A53EEFBE0758A201 1F95719CC926416D B381ECD5764F6587 total CC5BFD4F8EBF8159` |
| 6 | Mid-frame cut consistency | machine + partial | **pass**, limit named | the container-operation attempt (`d3-container-fill-then-save.json`) and `d3-revision-lines.txt`: cut `revision 332` and decoder `revision 332` (manifest `globalRevision` equals the decoder's); the same equality held for the earlier cut (325/325) and the leave cut (332/332). The "half-applied read" is the attempted shape, not an observed one |
| 7 | Same-layer restore | machine | pass (batch `20261001-m`) | sibling record; this batch's solo restores returned to layer 0 with the same layout reads |

## Residuals for the user

- Row 1: whether the replayed block diff looks right on screen (cracked/damaged sprites) — unchanged
  from the batch-`20261001-m` record.

## Limits

- **Row 3's only remaining gap is the opened lockable**: opening an `ItemLock` needs the lockpick
  minigame and no drive path exists, which the batch plan itself declares as the reason the row keeps
  `unproven`. Everything else in the row — buildings, traps, fluids, enemies and their restore — is
  judged above.
- **The entity families are session-gated**: a solo run produces the same staging with zero
  world-entity/enemy/fluid rows (the patch bridges require `IsSessionActive`), which is why Run D could
  not judge them.
- **One restored world-entity row was not taken** (named at error level): the regenerated layer has no
  such entity where the cut recorded it. The account reports it instead of dropping it silently; it is
  the designed refusal path, recorded here rather than counted as a pass.
- Substitutions: the lethal hit is the game's own `kill`; the container operation is `container-fill`
  through `Container.LoadItem`; the trap/building mutations are the game's own `explode`; the refused
  cut (row 5's sibling) is a save-root rename.
