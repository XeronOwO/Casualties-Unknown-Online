# Acceptance record — A member that lost its body to a layer change must come back without a cold restart

- Ticket: `layer-change-member-recovery` — verdict: **pass, moved to `done/`**: the recovery's two halves
  were staged deterministically (the entry instruction lost on a member's receive seam), the pre-fix
  artifact left the member in the menu with no local body, and the fix artifact brought it back into the
  host's new layer with its carried items, no cold restart.
- Batch: `20261025-a`, two sessions of one batch — session 1 on the PRE-fix artifact (the red), session 2 on
  the fix artifact (the green). No other ticket was served by this batch.
- Commits / artifacts: session 1 ran against commit `b8628640`, deployed artifact `0.1.0+b8628640f769ea7f79e36ce1e3aa45cf34eef08c`
  (`red/prelude-verify-deploy.txt`); session 2 ran against commit `8d42aca0`, deployed artifact
  `0.1.0+8d42aca0bb3e7b003bcecfd978fe3fdb079f1954` (`prelude2-verify-deploy.txt`, `tools/verify-deploy.ps1`
  exit 0 both times). This record's code commit is `8d42aca0`.
- Run: 2026-10-20 20:16 → 20:54 local · Host: physical machine (the operator) · Guest: sandbox
  `<sandbox-guest>` · Third client: sandbox `<sandbox-alt>`
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible; the alternate third client is configured`)
- Artifacts: `20261025-a/` in the directory named by `acceptance-artifacts-dir` — the red's copies under
  `red/`; the green's fixture, provocation, states and table reads at the top level; the two log excerpts
  `ev-green-host.txt` / `ev-green-guest.txt`; the red's `ev-host-invites.txt` / `ev-guest-entry.txt` under
  `red/`.

## The defect, staged deterministically

The ticket's door was the world-entry instruction: `WorldJoin` is an EDGE the host sends once per entry, so
a member that misses it has nothing to ask again. Batch `20261007-a` read the shape twice by accident (a
parked member held, an unprotected one still out at +60 s) but could not say which attempt would strand.
This batch makes the loss deliberate with the new committed recipe `net-drop-message`
(`tools/acceptance/recipes/net-drop-message.cs`): it parks the client's `PacketReceiver.MessageArrived`
seam and drops exactly one message family — `WorldJoin` — for a window, while every other frame still
arrives.

**Session 1 (red, artifact `+b8628640`).** The fixture was a settled three-client world with a staged
`emergencylight` on each member (`f1-*`, `s0-*`). The drop was armed on the guest for 25 s
(`red/r1-guest-drop-arm.json`: `armed: true`), `skiplayer` was driven on the host (`red/r2-host-skiplayer.json`),
the command held 26 s and cleared the drop (`red/r3-guest-drop-off.json`: `dropped: 1`). The host logged ONE
`World join sent to 2 member(s)` at 20:17:49.104 (`red/ev-host-invites.txt`); the guest's log carries the host's
InMenu, its own `Leaving the world to the main menu`, its InMenu reports — and **no** `World join received`
(`red/ev-guest-entry.txt`). At +80 s and +112 s after the change the guest was still `inWorld: false` with
`container-read mode=local` → `no-local-body` (`red/t1-guest-*`, `red/t2-guest-*`), while the control member
(invite delivered) was back in the world with its two items (`red/t1-alt-local.json`).

**Session 2 (green, artifact `+8d42aca0`).** Same fixture and same provocation (`provoke-green.log`:
`armed: true`, `dropped: 4` — the host's edge invite and the first three answers to the guest's asks). The
guest's own log is the recovery (`ev-green-guest.txt`): the window arms on the host's exit
(`[Reentry] the host left the world …`, 20:52:20.292), the member leaves and reports InMenu, then asks at
20:52:29.167 / 34.156 / 39.157 / 44.156 (`(1/12)` … `(4/12)`); each ask is admitted and answered by the host
(`[Reentry] … asked to be taken back in and this host's world is up — sent the entry instruction (with its
run-baseline checkpoint)`, `ev-green-host.txt`), and the fourth answer arrives after the drop window
(`World join received — starting a run to follow`, 20:52:44.175). The guest generates the host's layer
(`Applied host world params`, `Generation stream reset to captured baseline`, `[GenStream] done — 19 segments`
at 20:52:53.394) and the window closes by name: `[Reentry] the window is closed — this member is back in the
world.` The host logs the member's InWorld report at 20:52:53.422. No `[ERR]`/`Unity:Exception` line on any
client in either session.

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | After a consecutive layer change the member has a local body again and `container-read mode=local` answers with its inventory | machine | **pass** | `g2-guest-state.json` `inWorld: true`; `g2-guest-local.json` `localCount: 2` — the staged `emergencylight` (id `9492956821`, the SAME id the fixture created before the change, `g1` at 20:52:54 was mid-generation with `localCount: 0`) and the run's starting supply; the window's own close line names the return (`ev-green-guest.txt`) |
| 2 | The member's world matches the host's (`[LayerReset]` count and the host's world-item table read after the member's own generation finished) | machine | **pass** | both sides' `[GenStream] done — 19 segments` (host 20:52:29.049, guest 20:52:53.394 — the same generation length), the guest's `Applied host world params` + `Generation stream reset to captured baseline`, the host's `[LayerMod] enter … depth=1` (20:52:29.047); `g2-host-table.json` after the guest's generation: `carriedCount: 5` (both members' carried records), `containedCount: 0` (no container content leaked to the world), `worldCount: 247` |
| 3 | The third peer sees that member's clone in the new layer | machine | **pass** | `g2-alt-clone.json` on the third client, `mode=clone`: `itemCount: 3, ownerCount: 2` with `owners = [{guest: 2}, {host: 1}]` — the recovered member's two carried items are rendered proxies on the third peer; `g2-alt-state.json` `inWorld: true`, `gateWaiting: false` |
| 4 | No cold restart was needed | machine | **pass** | one process per client for the whole session (the prelude started them and this batch's close quit them, `q2-*`); the host's log carries the guest's InWorld report AFTER the change (20:52:53.422) and the guest's own log the ask → join → generation chain in one session; no `[ERR]`/`Unity:Exception` line on any client; the session never left the lobby (`active: true` in every read) |

## What this batch also corrected

- **One `skiplayer` is ONE layer advance, not two.** The ticket and the two older records read a second
  layer advance from the `[LayerReset]` lines ("`LayerReset = 4` in each attempt"). This session's host log
  shows what those four lines are: the item and world resets each fire at the generation's START
  (20:52:20.567) and again at its END (20:52:29.117), so ONE advance emits the four lines, and the host
  logged `[GenStream] done` exactly once (`ev-green-host.txt`). The decompiled `skiplayer` calls
  `RegenerateWorld(false)` (`reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs:721`), which
  advances exactly one layer (`WorldGeneration.cs:1042-1067`), so the "automatic second advance" the older
  records describe cannot be produced by that command. The door the ticket chose does not depend on it (one
  host exit is what pulls the member); the ticket's observation row and the lesson ledger are corrected.
- **`state.inWorld` is `IsInWorldOrGenerating`.** The `+4.1 s` reading the ticket inherited
  (`inWorld: true` with no local body) is the GENERATING half of a re-entry, not a member believing it is in
  a world it is not in; the reading that distinguishes the two is this batch's `inWorld: false` +
  `no-local-body`.

## Residuals for the user

None: every row above is a machine read or a machine event.

## Limits

- **One direction and one host.** The host was the physical machine and both members were sandbox guests;
  the mirrored direction was not driven.
- **The drop is message-selective by construction.** The recipe drops every `WorldJoin` the member receives
  during the window, so the batch reads "the entry instruction was lost" and not "one particular send was
  lost"; that is the family's shape (the lazy-P2P swallow drops a frame, not a caller).
- **The stuck-generating population was not read.** The ticket's `Limits` name it: a member whose own
  generation outlives its transition is outside this window, and this batch did not stage one.
- **No window frame of the recovered member.** Rows 1–4 are machine rows; the visual half of a member coming
  back into a loaded world was already read by batch `20261007-a`'s frames for the held case, not repeated
  here.
- **The recipe is new.** `net-drop-message` is committed with this change and was smoked in the same session
  it staged (arm → status → off, `red/r1b-guest-drop-status.json`); its `dropped` count and the host's one
  `World join sent` line are the two halves that prove the drop landed where the batch intended.
