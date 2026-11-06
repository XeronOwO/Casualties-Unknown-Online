# The pre-run lobby: rows 1-2 read in batch 20261106-a

- Date: 2026-11-06, 18:20-18:24 (Asia/Shanghai)
- Artifact: `0.1.0+ef9d9c3419faddf348fff80d1afcf0d61df87660` (acceptance build; `tools/verify-deploy.ps1` exit 0)
- Session: host on 18590 with the Chinese UI; guest on 18591 in the Steam1 box; alt 18592 up but not joined
- Batch dir: `.acceptance/20261106-a/`
- Ticket: `docs/backlog/done/the-pre-run-lobby-can-deadlock.md`
- Driver: `tools/acceptance/drive-in-process.ps1` (in-process; every step below is one driver call)
- The client renders its gate lines in Chinese; the gate texts quoted below are English translations of the exact strings.

## Row 1 — a member that re-joins before the run starts is counted again, and both gates clear without a restart

| step | reading |
|---|---|
| 18:21:39 host `create-lobby` | lobby `109775243623135194`, role Host, `hostId=76561198281246659`, `inWorld=false`, gate text (translated) "waiting for the rest of the players to load ... (0 s) — the party departs together." |
| 18:21:44 guest `join-lobby` | role Guest, the same lobby, `inWorld=false`, gate text (translated) "waiting for the host to load ... the host has not entered the world yet." |
| 18:21:49 guest `click home.leave` | `applied=true`; the next state reads role None, lobby `0`, controls back to create/join |
| 18:21:57 guest re-join | role Guest on the same lobby id |
| 18:22:03 host `start-run` | `inWorld=true`, `gateWaiting=false`; gate text (translated) "waiting for Kaguya (alt) to load ... (0 s) — the party departs together." |
| 18:22:14 both states, same batch | host `inWorld=true gateWaiting=false`; guest `inWorld=true gateWaiting=false` |

Host log on the same chain:

- 18:21:44.992 `[Reentry] 76561198863287957 is out of the world and asks to be taken back in — the world half is this host's to answer.`
- 18:21:44.992 `[Reentry] ...'s ask is not answered yet: this host has no live world (entering or regenerating) — the member's bounded window asks again.`
- 18:22:03.429 `World join sent to 1 member(s) (tutorial: False, run 1, baseline follows: True).`
- 18:22:14.557 `Remote entered the world — clone rebuilt on rejoin.`
- 18:22:14.669 `World join sent to 0 member(s)` + `No confirmed members waiting — releasing the start gate immediately.`

Verdict: PASS — the member re-joined a lobby whose run had not started, the host counted it again, and both gates cleared with no client restart.

## Row 2 — a member that drops while the gate waits does not hold the gate

| step | reading |
|---|---|
| 18:22:5x both `leave-world` | host `inWorld=false` role Host; guest `inWorld=false` role Guest; nobody in the world |
| 18:22:58 guest `net-receive-blackout mode=on` | the guest's inbound traffic is dropped (subscribers 1→0, `parked=true`) |
| 18:23:0x host `start-run` | returns `inWorld=true gateWaiting=false` (the snapshot at the click); a later `state` reads `gateWaiting=true` and gate text (translated) "waiting for Kaguya (alt) to load ... (23 s)." |
| 18:23:07.141 (host log) | `Start gate armed — waiting for 1 member(s) to finish loading.` |
| 18:23:07.144 (host log) | `the wait line now owns the game's loading text` |
| 18:23:37.165 (host log) | `Start gate forced after 30 s — still waiting for 1 member(s); they join when they finish loading.` |
| 18:23:37.165 (host log) | `The session rose around an already-running world — the host resumes playing.` |
| after 18:23:30 guest `home.leave` | guest role None, lobby `0`; host `inWorld=true gateWaiting=false` |

Verdict: PASS — the host named the missing member and departed at its own 30 s bound; it did not wait forever.

Honest note on the fixture: the blackout was lifted after it had already run past the guest's 15 s host-silence watchdog, so the guest's own session had ended (`hostId=0`) before its `home.leave`. That is a fixture overshoot, not a gate hold: the row is about the HOST's behaviour while the member is absent, and the host's own log and gate read it directly.

## Row 3

Row 3 passed on 2026-11-01 (build `70c8ad04`, recorded in the ticket); this batch did not re-run it.
