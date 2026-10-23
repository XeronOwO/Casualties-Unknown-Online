# Acceptance record — the give gesture respects the player's own UI (row 7 re-read)

- Ticket: `give-gesture-own-ui-and-center-use` — verdict: **row 7 passes**, all ten rows read; moved to
  `done/`.
- Batch: `20261023-j` — this row plus the seven rows of `gamerule-host-rule-command` (record
  `gamerule-host-rule-command-20261023-j.md`), which supplied the driveable host-rule switch.
- Commit: `a2ba208d` · Deployed artifact: `0.1.0+a2ba208dd0a275130175161090467de5e7a4258c` —
  `tools/verify-deploy.ps1` exit 0 before the clients started.
- Run: 2026-10-24 00:20 → 00:22 · Host: physical machine (the giver and the viewer) · Guest: first
  sandbox. One two-client session over the shared world.
- Dependencies: `preflight.ps1` OK, `session-environment.ps1 -Mode ensure-cuo` `active=cuo`, the in-process
  driver on both endpoints, the committed `remote-gesture` and `item-provide` recipes, and the console
  console's `/gamerule` (this batch's other ticket). No cursor or keyboard API was used.
- Artifacts: `j-r2-*.json` (the rule set), `j-b1-item.json`, `j-b2-open.json`, `j-b3-release.json`,
  `j-b4-find.json`, `m-give.txt` in the directory named by `acceptance-artifacts-dir`; players are
  written `<host>`/`<member>`.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 7 | View-only displayed backpack — a centre use/wear is refused with one line and nothing moves | **pass** | The host set `remote_backpack_access` to `unconscious_or_dead` through its own console (record above, row 2; the member's announced value read `unconscious_or_dead`). The host held a `bandage` (id `1172847052739`, slot 0, `j-b1-item.json`) and opened `<member>`'s backpack (`j-b2-open.json`: `viewOpen=true radarOpen=true itemOwner=<member>`); the host log wrote `[BackpackView] opened native backpack view for <member> (acceptance) — view only.` (00:21:34.967, `m-give.txt`). The centre release (`remote-gesture mode=release item=local0 cast=-2`, the game's own `HandleReleaseDragging` with the `RadialCenter` cast; `j-b3-release.json`: `castKind=center`, `dragAfter=none`, `viewOpen=true`) produced exactly one line: `[DragUse] refused the use of bandage on <member>: the displayed backpack is view only.` (WRN, 00:21:41.466) — no `[ItemUse]`, no `[DragUse] used …`, no `[ItemDropped]` line followed it. The same bandage instance was still on the host's body in slot 0 after the release (`j-b4-find.json`: `found=true already=true instanceId=1172847052739 slot=0 onBody=true`), so nothing moved. |

## Limits

- **The release was driven through the game's own release path in process** (`PlayerCamera.HandleReleaseDragging`
  invoked with the staged `RadialCenter` cast, the `remote-gesture` recipe's committed mode), not with the
  physical pointer. The pointer-driven centre release was read in batch `20261023-f` rows 3 and 6 (a real
  right-button drag released on the displayed backpack's centre produced `[DragUse] used …` and kept the
  view open), so this row's entry is that same release path; this batch supplies the view-only state and
  the refusal.
- **One repetition.** The release ran once; a race inside the gate is not excluded.
- **The view was opened through the player's backpack entry** (`remote-gesture mode=open owner=auto`), the
  same registered entry the drag's auto-open uses, not by the drag itself; the drag's auto-open was read in
  batch `20261023-f` rows 2 and 8.
- **The refusal line is a warning log line, not a console line** — the give gesture's refusal is answered
  where the giver's own log records it (`[DragUse] refused …`), as the row asks ("with one line"); no
  player-facing cue was added by this ticket.
