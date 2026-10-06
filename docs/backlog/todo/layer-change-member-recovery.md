# A member that lost its body to a layer change must come back without a cold restart

- Status: Todo — the attribution reading this ticket's first job asks for was taken by batch `20261007-a`
  (2026-10-07), so the door is chosen and the recovery itself is what is left to develop: see
  `## The reading (batch 20261007-a)` below and
  `docs/evidence/acceptance/layer-change-member-recovery-20261007-a.md`.
- Priority: Medium
- Category: World generation / layer transition / member recovery
- Source: acceptance batch `20261005-b` (2026-10-05), observed while staging
  `done/drop-pending-single-slot-overwrite.md`'s row 4 and recorded there as a limit. Split out of
  `todo/layer-change-member-dropout.md` on 2026-10-06, whose 2026-10-06 cycle bounded that ticket's log-storm
  half but did NOT attribute this half — the cause is unknown, so no fix is claimed here and the first job of
  the cycle that takes this ticket is to ATTRIBUTE it, not to implement a recovery.
- Related: `todo/layer-change-member-dropout.md` (the umbrella ticket: its log-storm half landed
  2026-10-06, its item list still carries this work), `done/layer-mod-baseline-divergence-on-continue.md`
  (the same warning from another producer — a Continue that reopened the wrong world, a genuine detector,
  NOT this shape), `done/reenter-baseline-adoption.md` (a member that never left the session generates
  before the host's restored baseline arrives)

## What is observed (batch evidence, cause unknown)

| Fact | Where it comes from |
|---|---|
| At +4.1 s after the layer change the member had no local body at all: `container-read mode=local` → `no-local-body`, so that attempt's destroy probe found nothing to destroy and row 4 had to be staged again | the batch record's Limits; the artifacts `r4b-guest-local-t2.json` / `r4b-guest-local-t3.json` (`"error": "no-local-body"`) in the batch's artifact directory |
| During the same session the member still believed it was in the world: `role: Guest`, `inWorld: true`, `gateWaiting: false`, page `Home` | `r4-guest-state.json` / `r4-alt-state.json`, same directory |
| A SECOND layer advance follows the first on its own about 9 s later, so one `skiplayer` command is already a consecutive change; the host's `[LayerReset]` line appears twice per attempt | the batch record's Limits, `[LayerReset] dropped the previous layer's world-rooted items; …` |
| Nothing recovered on its own — the batch restarted all three clients cold and re-judged the remaining rows in a second session | the batch record's Sessions paragraph |
| The same window carried the log storm this repository has since bounded; whether the storm and the missing body share a cause, one causes the other, or both follow from the layer change is NOT known | `docs/backlog/todo/layer-change-member-dropout.md` and `docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md` |

## Why this is not fixed yet

The umbrella ticket's own reading of the layer-modifier path explains the WARNING (the two sides' decision
entry states differ, so their worlds were generated from different baselines), and the storm's volume is
attributed. Neither explains a body that the game never rebuilt while CUO believed the member was in the
world — the world generation is the game's own scene reload, and the local body's creation is not a path
CUO drives. Any recovery code written now would be a guess at a cause nobody has read, and a guessed
recovery is unverifiable in the exact scenario that matters.

## The reading (batch `20261007-a`, 2026-10-07) — the door is chosen

That batch took this ticket's reading on three clients in one session: three attempts, differing only in
whether a member's inbound dispatch was parked through the host's `skiplayer` change. Record:
`docs/evidence/acceptance/layer-change-member-recovery-20261007-a.md`.

- **"Not yet" and "never" are both real, which is what the two readings were for.** Attempts 1 and 2: both
  members out at +5 s, both back at +20 s (`inWorld: true`; attempt 1's `container-read mode=local` answering
  `localCount: 1`). Attempt 3: the parked member stayed in the world with its body and its carried item, and
  the unprotected member was still out at +60 s with `no-local-body`. The batch's own `+4.1 s` reading could
  not tell those apart; this one can.
- **The exit follows an inbound message, not a scene reload the game performs on its own.** The three attempts
  differ only in the park and the park decides them: held inbound, the member stays; not held, the member
  leaves. That answers `## Why this is not fixed yet`'s first question and rules out the "the game never
  creates it" door as the whole story.
- **What the member sits in is "in the lobby, out of the world"**, not a crash: at +5 s the window frame shows
  the Online UI over an unloaded dark world with `Handshake: active`, `Members: 3`, no player and no name tag;
  at +20 s the same window shows the loaded world with a character on screen and the peer tag.
- **Nothing throws**: no `[ERR][Unity:Exception]` and no exception stack on any client in any attempt, on
  either side of the change.

## Acceptance

- **Attribute first, in one run, with the fixture the batch already used** — **done as batch `20261007-a`**,
  whose reading is recorded in `## The reading (batch 20261007-a)` above: three clients, a settled world,
  one `skiplayer` command on the host (accepting that a second advance follows on its own), and then — this
  is the reading that decides it — on the member that lost its body: `state` (does it still believe it is in
  the world?), `container-read mode=local` (does a local body exist?), the member's own log around the
  change (`[LayerReset]`, the world-generation lines, any exception, `[ERR][Unity:Exception]` in the rolling
  log), and a world screenshot of that member's window (is it looking at a loaded world with no player, at a
  black screen, or at the loading banner?). Repeat the reading at +5 s and +20 s, because "not yet" and
  "never" are different defects and the batch's own `+4.1 s` reading cannot tell them apart.
- **Then let the reading choose the door** — **chosen by batch `20261007-a`**: the exit is decided by the
  inbound the member processes during the change, so the door is that message path, not "the game never
  creates the body". The ticket's original statement of the choice stands: if the body is created but CUO's
  world-entry state machine suppresses or re-enters it, the fix belongs in that state machine; if the game
  never creates it, the fix is an entry re-drive CUO can trigger (the same shape
  `done/reenter-baseline-adoption.md` established); if it appears at +20 s, the "defect" is a slow generation
  and the row becomes a timing expectation. The reading shows the +20 s outcome is real for some attempts and
  NOT for others, so the recovery cannot be "wait for the game".
- **Rows, all machine-read**: (1) after a consecutive layer change the member has a local body again and
  `container-read mode=local` answers with its inventory; (2) the member's world matches the host's
  (`[LayerReset]` count and the host's world-item table read after the member's own generation finished);
  (3) the third peer sees that member's clone in the new layer; (4) no cold restart was needed — the
  session's own line count and the presence of both members' reports after the change prove it stayed up.
- **Schedule this run last in its batch**: the storm and the members' exit out of the world are the
  machine's own cost, and the staging procedure, the recovery shape and the 15-second black-window ceiling
  are machine facts of the acceptance area's gitignored local files.

## Non-goals

- Re-attributing the log growth: bounded and pinned on 2026-10-06
  (`docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md`).
- Removing or weakening the divergence detector, and guarding the debug `skiplayer` command (it is the
  game's own console command).
