# Acceptance record — Consecutive layer changes drop the members out of the world and storm the log

- Ticket: `layer-change-member-dropout` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (…)`): the two producers this cycle bounded ARE bounded on the real shape, but
  the row's other expectation fails and a THIRD producer of the same family reproduces the pre-fix growth
  rate.
- Batch: `20261007-a` — tickets `layer-change-member-dropout` and `layer-change-member-recovery` (the
  attribution readings the second ticket's acceptance asks for: same session, same fixture)
- Commit: `2675221c` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2675221cc96cbe1e8bc39a84e5ac1dfbeab21671`
- Run: 2026-10-07 00:12 → 00:18 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`)
- Artifacts: `20261007-a/` in the directory named by `acceptance-artifacts-dir` — the three staging
  attempts (`p1-*`, `p3-*` blackout arms, the `skiplayer` probes), the state and body reads (`s1-*`,
  `t5*`, `t20*`, `t40c-*`, `t60c-*`), the three log marks (`m1-`/`m2-`/`m3-before-stage*.txt`), the window
  frames (`t5-*-window.png`, `t20-*-window.png`) and the storm excerpt (`ev-alt-storm-head.txt`,
  `ev-alt-storm-tail.txt`)

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The staging re-drives with the members staying in the world | machine | **fail** | attempt 1 (`p1-*`): `t5-guest-state.json` and `t5-alt-state.json` both read `inWorld: false` at +5 s (00:14:12). Attempt 2 (`p2-*`): `t5b-guest-state.json` `inWorld: false` and `t5b-guest-local.json` answering `"error": "no-local-body"` at +4.2 s. Attempt 3 (`p3-*`, the members' inbound parked through the change): the parked member held (`t5c-guest-state.json` `inWorld: true`, `t5c-guest-local.json` `localCount: 1` with `emergencylight`) while the unprotected one was out — `t5c-alt-state.json` `inWorld: false`, and still out at +25 s, +40 s and +60 s (`t20c-`, `t40c-`, `t60c-alt-state.json`; the last two answering `no-local-body` on `container-read mode=local`) |
| 2 | The client's log growth read as a size is bounded | machine | **fail** | Attempt 3's third client: 58,960 lines / 8.796 MB in ~90 s, of which **58,148** are `Remote body: no Body component in "Experiment" clone.`; a further 10-second sample measured **1.212 MB / 10 s = 7.25 MB/min** at 639–866 lines/s, still climbing when the session closed — against ~8.2 MB/min for the storm this cycle bounded. Excerpts: `ev-alt-storm-head.txt`, `ev-alt-storm-tail.txt`, and the census `ev-alt-storm-census.txt` taken after the clients closed (**82,618** such lines / **12.442 MB** for that client since the mark) |
| 3 | *(the half this cycle's fix owns)* the two producers the cycle bounded stay bounded on the same shape | machine | **pass** | Attempt 1 census (guest / alt): `[ItemPhysics] settle` **14 / 13** lines (was 4,445 of 7,368) and `[Fluid] region` **5 / 7** (was 415). Attempt 2: `[ItemPhysics] settle` **0** lines in both clients. `[LayerMod] baseline divergence`: 0 (attempt 1), 1 + 1 (attempt 2), 3 + 2 (attempt 3) — the 5-second keyframe cadence this cycle corrected. Zero `[ERR][Unity:Exception]` in all three attempts and all three clients |

## What the run read about the row's premise

- **The members leaving the world has a trigger, and the run isolated it.** Three attempts differ only in
  whether the members' inbound dispatch was parked through the change. With the park ineffective
  (attempt 1, a 0.1-second window) *both* members left the world; with no park at all (attempt 2) *both*
  left; with one member parked for 9.4 s (attempt 3) the **parked member stayed in the world with its body
  and its carried item** and the **unprotected member left**. So the exit follows an INBOUND message the
  member processes during the change, and a member whose inbound is held does not take that path — it is
  not a scene reload the game performs on its own.
- **"Not yet" and "never" are both real, which is why the ticket's two readings exist.** In attempts 1 and
  2 both members were back by +20 s (`t20-guest-state.json`, `t20b-guest-state.json` `inWorld: true`), the
  first of them with `container-read mode=local` answering `localCount: 1`. In attempt 3 the unprotected
  member was still out at +60 s with `no-local-body`. The batch's own `+4.1 s` reading could not tell those
  apart; this run can, and the answer is that recovery is neither guaranteed nor bounded.
- **The visual half of the reading.** `t5-guest-window.png` (+5 s, out of the world) shows the Online UI
  over an unloaded dark world: `Handshake: active`, `Owner: 萧铃`, `Members: 3` — the member still believes
  it is in an online session — with no player and no name tag visible. `t20-guest-window.png` (+20 s, back)
  shows the same window with the world loaded: a character on screen and the peer tag `萧铃 48 m`. So the
  state a dropped member sits in is "in the lobby, out of the world", not a crash and not the loading
  banner.
- **The bounded half is real and measurable.** The two producers the cycle named — the item follow pump's
  per-frame correction line and the fluid region receive handler — collapsed from thousands of lines to
  single digits, and the divergence warning the batch originally blamed settles at the corrected 5-second
  keyframe cadence. What the cycle did NOT cover is the family's third member below.

## The family's next producer (filed, not fixed here)

`RemotePlayerRenderer` writes `Remote body: no Body component in "Experiment" clone.` at **Warning**, once
per clone per frame, for as long as a clone has no `Body`. On a member that has left the world that
condition does not resolve, so the line is unbounded and reproduces the storm this ticket is about — same
shape, same level, different producer, and outside the census `LogVolumeGateTests` pins. Filed as
`done/remote-clone-warning-storm-on-member-dropout.md` with this reading as its evidence.

## Residuals for the user

None: every row above is a machine row or a window frame the agent read.

## Limits

- **One direction and one host.** Every attempt drove `skiplayer` on the physical-machine host with both
  members as sandbox guests; the mirrored direction was not driven.
- **Three attempts, one fixture, one layer edge.** The run cannot say how often a dropped member recovers:
  attempts 1 and 2 recovered inside 20 s and attempt 3's unprotected member did not recover inside 60 s.
  That spread is the reading, not a rate.
- **The third attempt protected one member only.** Park covers the member it is armed on, so the run has no
  attempt in which BOTH members were held through the change; row 1's pass condition was therefore never
  reached on both members at once.
- **The layer count.** Three consecutive changes were driven, each followed by the game's own second
  advance ~9 s later; the run did not descend further and did not try the elevator path, which
  `layer-change-member-dropout` still lists as open.
- **No wire or save reading.** No messages and no saves were read, and no hand-feel was judged.
- **`container-read` on the parked member read `localCount: 1` twice**, before and after; the item is the
  one the batch's own fixture carried across the boundary, not a newly created one.
