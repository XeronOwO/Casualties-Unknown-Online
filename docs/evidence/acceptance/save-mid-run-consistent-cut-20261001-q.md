# Acceptance record — S3 mid-run consistent cut and world diff (batch 20261001-q)

- Ticket: `save-mid-run-consistent-cut` — verdict: **stays in `review/`** (row 3's restore half is not
  verified; rows 2, 4, 5 and 6 are newly judged below, and rows 1 and 7 passed in batch `20261001-m`)
- Batch: `20261001-q` (Run D solo; Run E host + guest) — siblings `save-solo-menu-exit-trigger`,
  `save-run-clock-not-sent`, `save-new-player-starting-supplies`
- Commit: `2efca14b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:31 · Host: physical machine (Steam) · Guest: the primary sandbox (Run E)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Mined/placed/quaked blocks + partial damage | machine + residual | pass (batch `20261001-m`) | sibling record `save-mid-run-consistent-cut-20261001.md`; this batch re-cut the same world family (7 world-block rows in every cut, `d-archive-listing-cut1.txt`) |
| 2 | World items (ground/container/hand) | machine + named gap | **pass** | before the cut (`d2-kernel-before-cut.json`): ground `rosepod#1147077248963` World, `duffelbag#1348940711875`/`burger#1353235679171`/`scrapmetal#1357530646467` Carried; the cut's own files carry them (`characters/…json` 3 items, `items.json` kind World/Carried rows, verified against `live/` before the continue); after the two Continue cycles the same ids and positions come back (`d2-kernel-after-restore.json`, `d2-find-rosepod-after-b.json` id + (0, 473.017), `d2-tree-after-restore-b.json`: the bag with its nested burger at slot 0, the scrapmetal at slot 1, character restore `(3 items)` in `d2-host-log-restore.txt`); kernel totals stable (317 → 317). The `worn` resting stays the ticket's named gap |
| 3 | Buildings/traps/fluids/enemies | machine + named gap | **unproven** (half executed) | a live session cut **did** capture all four families: `e-host-cut-line.txt`: `Cut Command committed for world w-20261001-b0d7 … revision 1949 … 678 enemy row(s), 243 fluid chunk(s), 31 world-entity row(s)`, and the archive rows read back `world-entities.json` = `building-health 19, trap-consumption 6, trap-state 6`, `enemies.json` = `enemy 673, removed 5`, `fluids.json` = 243 chunks. The **restore** of that cut was never verified: the repository pointer sent the host's Continue to another world (`Continue restored world w-20261001-95ec` in the host log), and the host then died in the transport runaway. Opened lockables stay the ticket's named gap |
| 4 | In-flight states | machine + partial | **pass**, limit named | `d6-kill-player.json` (the game's own `kill`), then the cut immediately after: `d6-host-log-kill-save.txt` shows the death dropping the carried container (`[ItemDropped] duffelbag (id 1348940711875) … container contents 2`, `origin=FlushPendingDrop result=Committed(1)`) and the cut committing at revision 333 with 317 item rows; the cut's `items.json` carries the dropped bag as a World item at (0.07, 473.4) — no silent loss. The deferral window itself had closed before the cut armed (0.9 s gap), so the deferral branch was not observed live; the cut report names the classes it never carries (`d6-console-after-kill-save.json`) |
| 5 | Save → load → save → load | machine | **pass** | two in-session cut/continue cycles plus a fresh-process entry: every cycle restored the staged ids and the character restore applied 3 items (`d2-*-restore*`); the one-shot fingerprint is not re-armed by a Continue, so the second fingerprint was taken from a fresh client process (`d7-continue-fingerprint2.json`): both `[WorldFingerprint]` lines are identical — `12EFBA9839F8D246 95FED72355D92AEA 5DDD1F916F604803 2734A90A86993A99 FA2192CD5E988E91 A53EEFBE0758A201 1F95719CC926416D B381ECD5764F6587 total CC5BFD4F8EBF8159` (`d2-fingerprint-lines.txt`, `d7-fingerprint-lines.txt`) |
| 6 | Mid-frame cut consistency | machine + partial | **pass**, limit named | the container-operation attempt: `d3-container-fill-then-save.json` (the scrapmetal loaded into the bag immediately before the cut) and `d3-revision-lines.txt`: cut `revision 332` and decoder `revision 332` (the manifest's `globalRevision` equals the decoder's); the same equality held for the earlier cut (325/325) and the leave cut (332/332). The "half-applied read" is the attempted shape, not an observed one |
| 7 | Same-layer restore | machine | pass (batch `20261001-m`) | sibling record; this batch's solo restores returned to layer 0 with the same layout reads |

## Residuals for the user

- Row 1: whether the replayed block diff looks right on screen (cracked/damaged sprites) — unchanged
  from the batch-`20261001-m` record.

## Limits

- **Row 3's restore half is the blocking gap.** The cut-side facts are complete and machine-readable,
  but the run never restored *that* cut: the repository's `lastOpenedWorldId` still named the solo
  world, so the host's Continue opened it instead (the ticket's own recorded lesson that a new run does
  not move the pointer). A re-run that selects the session world first, or a continue from the
  mid-run cut itself, can close it.
- **The entity families are session-gated.** In a solo run the same staging produced
  `enemies.json`/`fluids.json`/`world-entities.json` with 0 rows (the domains' patch bridges require
  `IsSessionActive`), which is why the family had to be staged in Run E.
- **Substitutions**: the lethal hit is the game's own `kill` command; the container operation is the
  `container-fill` recipe through `Container.LoadItem`; the trap and block mutations are the game's own
  `explode` debug entry.
- The follow-up ticket for the transport runaway (files: `docs/backlog/todo/`) carries the Run E
  incident that ended the batch's guest session.
