# Acceptance record — A member that lost its body to a layer change must come back without a cold restart

- Ticket: `layer-change-member-recovery` — verdict: **stays in `todo/`** (the ticket's first job was the
  attribution reading, and this batch took it; the reading now names the door, and the recovery itself is
  ordinary development work)
- Batch: `20261007-a` — tickets `layer-change-member-recovery` and `layer-change-member-dropout`
  (one session, one fixture, one deployed artifact)
- Commit: `2675221c` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2675221cc96cbe1e8bc39a84e5ac1dfbeab21671`
- Run: 2026-10-07 00:12 → 00:18 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present
- Artifacts: `20261007-a/` in the directory named by `acceptance-artifacts-dir` — the three attempts'
  probes and reads, the window frames and the storm excerpt listed in the sibling record
  `layer-change-member-dropout-20261007-a.md`

## The reading the ticket asked for

The ticket asks for three clients, a settled world, one `skiplayer` on the host (accepting the game's own
second advance ~9 s later), and then, on the member that lost its body, `state`, `container-read
mode=local`, the member's own log around the change and a world screenshot — **at +5 s and +20 s**, because
"not yet" and "never" are different defects. The run did that three times, and added a third attempt with
the members' inbound dispatch parked through the change:

| # | Reading | Verdict | Evidence |
|---|---|---|---|
| 1 | Does the member still believe it is in the world? | **read** | Out of the world in both members at +5 s (`inWorld: false`, `page: Home`, `Handshake: active`, `Members: 3`); back at +20 s in attempts 1 and 2, still out at +60 s in attempt 3 (`t5-`, `t5b-`, `t20-`, `t20b-`, `t20c-`, `t40c-`, `t60c-alt-state.json`) |
| 2 | Does a local body exist? | **read** | `container-read mode=local` → `no-local-body` at +4.2 s (attempt 2) and at +40 s (attempt 3, unprotected member); `localCount: 1` with the carried `emergencylight` at +20 s (attempt 1) and throughout attempt 3 on the parked member (`t5c-guest-local.json`) |
| 3 | What does the member's own log carry around the change? | **read** | `[LayerReset] dropped the previous layer's world-rooted items…` twice per attempt on the host (`LayerReset = 4` in each attempt's census); **no** `[ERR][Unity:Exception]` and no exception stack on any client in any attempt; the member's log carries the world re-generation and, when it stays out, the unbounded `Remote body: no Body component` warning |
| 4 | What is on the member's screen? | **read** | `t5-guest-window.png`: the Online UI over an unloaded dark world — no player, no name tag. `t20-guest-window.png`: same window, world loaded, a character on screen and the peer tag `萧铃 48 m` |

## The door the reading chooses

The ticket says no door is chosen before the reading. The reading names two things:

- **The exit follows an inbound message, not a scene reload the game performs on its own.** Three attempts
  differed only in whether a member's inbound dispatch was held through the change: held for 9.4 s, the
  member **stayed in the world with its body and its carried item**; not held, the member left. A member
  that never processes the message never takes the path — so the defect lives on the message/state-machine
  side of the boundary, not in "the game never creates the body".
- **Recovery exists but is not guaranteed.** Attempts 1 and 2 came back inside 20 s; attempt 3's
  unprotected member did not come back inside 60 s and its `container-read mode=local` still answered
  `no-local-body`. The ticket's own framing — "not yet" against "never" — is therefore answered "both", and
  a recovery that waits for the game cannot be the whole answer.

What the batch does **not** do is choose the implementation: it read, and it did not touch code. The
ticket's rows 1–4 stay open for the cycle that writes the recovery, and row 3 (`the third peer sees that
member's clone in the new layer`) was not read in this batch at all.

## Residuals for the user

None: every reading above is a machine read or a window frame the agent read.

## Limits

- **One direction, one fixture, three attempts.** The spread between attempts 1/2 (recovered) and attempt 3
  (not recovered) is the reading; the run cannot turn it into a rate.
- **The parked member's world is not proven to match the host's.** Attempt 3's protected member kept its
  body and its item while the change happened, which is what row 1 asks; whether its world then matched the
  host's was not read, and the run did not check whether holding the inbound leaves the parked member a
  layer behind.
- **No cold restart was performed in this session**, so the "without a cold restart" half of the ticket's
  row 4 is read from the session staying up (both members' reports present after the change, no client
  restarted, the run's own line counts continuous) rather than from a recovery that was deliberately
  provoked.
- **The member's log was read through the rolling `latest.log`** with its byte-offset marks; the sandbox
  clients' `LogOutput.log` fallback was not needed in this run.
