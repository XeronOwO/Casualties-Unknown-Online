# Acceptance record — the item and entity data umbrella, stage B re-judged in SNBT

- Ticket: `item-and-entity-data-commands` — verdict: **stays in `todo/`**; stage D (`spawn`) is unbuilt, so
  the ticket does not move whatever these rows say. Stage B's rows were re-read here against the SNBT
  renderer and the write verbs' new answers.
- Batch: `20261018-q` — tickets `give-item-to-a-player-command`, `snbt-state-language`,
  `item-data-read-commands`.
- Commit: `30dbb252` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+30dbb2525ccd07d72ddd810c1781d068de2959a2` (verified by `tools/verify-deploy.ps1`, exit 0 before the
  clients started).
- Run: 2026-10-17 23:42:24 → 23:50:37 by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts`.
- Artifacts: the `g0*`, `d*` and `m*` readings in the directory named by `acceptance-artifacts-dir`; Steam
  IDs are written `<host>`/`<member>`.

## The host's own carried inventory (the section's rows 1-4 re-read)

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The host runs `/data get` and its starting supplies and worn items are listed with instance ids | machine | **pass** | `g0-host-body.json`: `Items recorded for <host> (1): #1190026921923 emergencylight [slot 3] {Condition:1f,…}` — the state that used to be empty for a host who had not used anything yet |
| 2 | The host writes one of them WITHOUT having used, moved or dropped that item; both peers read it back | machine | **pass** | the untouched supply is the emergencylight, and the write is its captured component field rather than its condition (the same seam, the same read-back): `d10-component-modify.json` `Components.CustomItemBehaviour.state 0 -> 3`; `m2-host-local-tree.json` shows the item still in slot 3; `d10-guest-component.json` reads `3` |
| 3 | The member reads the host's row | machine | **pass** | `d10-guest-component.json` — the member's replay names the host as the owner through the answer's path and returns the host's value |
| 4 | The member's own supplies still register the way they did (a regression row) | machine | **pass** | `g0-guest-body.json`: `Items recorded for <member> (1): #5197989525 emergencylight [slot 3] …` |

Row 5 of that section (the next-level transition) and the cursor section's rows 1-5 were not re-run in this
batch: row 5's plain-layer substitution was read in `20261017-q`, and the cursor rows still wait on the
pointer being inside the host window (record `item-and-entity-data-commands-20261017-q.md`). Nothing in this
change touched either mechanism.

## Stage B's write path in the new language

- **A write's answer is the re-read document**, not the requested text: `d3-modify-amount.json` reads
  `Liquids[0].Amount 300f -> 200f`, `d4-merge.json` reads `Condition 1f -> 0.8f; Favourited false -> true`,
  and `d6-remove-stack.json` reads the removed node — the same three clauses the lander carried, rendered
  from the SNBT tree.
- **A merge is still atomic**: `d5-merge-refused.json` refuses the whole state on its unknown second key and
  `d5-host-cond-after.json` still reads `Condition = 0.8f` at revision 2.
- **A member's write rides the same rule**: `d8-member-modify.json` applies and reports,
  `d8-host-cond.json` follows at `0.7f`, and the announced closed switch refuses the next member verb on the
  member's own console (`g7-member-give-closed.json`).

## Residuals for the user

None.

## Limits

- **Stage D (`spawn`) is the ticket's remaining work**, and the ticket stays in `todo/` on it regardless of
  these rows.
- **No save/restore and no reconnect** were exercised: whether a command-written property survives a rejoin
  the way a played one does is the stage's old open question, still not read.
- **One member, no third client.**
