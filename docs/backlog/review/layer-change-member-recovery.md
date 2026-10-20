# A member that lost its body to a layer change must come back without a cold restart

- Status: Review — code-complete, waiting for the acceptance run's green half. The attribution reading the
  first job asked for was taken by batch `20261007-a` (2026-10-07), so the door was chosen and the recovery
  is what this cycle built: see `## The reading (batch 20261007-a)` below and
  `docs/evidence/acceptance/layer-change-member-recovery-20261007-a.md`. Batch `20261007-c` (2026-10-07)
  re-read the held and the dropped shapes on the same fixture and changed no part of that verdict; its
  reading is `## The second reading (batch 20261007-c)` below. The defect itself was then staged
  deterministically by batch `20261025-a`'s first session — one dropped `WorldJoin`, the member still in the
  menu with no local body 80 s and 112 s later while the control member with the same change came back —
  and the recovery it drove is `## The recovery (landed 2026-10-20)` below. The same batch's second session
  re-runs the staging against the fix artifact for the rows.
- Priority: Medium
- Category: World generation / layer transition / member recovery
- Source: acceptance batch `20261005-b` (2026-10-05), observed while staging
  `done/drop-pending-single-slot-overwrite.md`'s row 4 and recorded there as a limit. Split out of
  `done/layer-change-member-dropout.md` on 2026-10-06, whose 2026-10-06 cycle bounded that ticket's log-storm
  half but did NOT attribute this half — the cause is unknown, so no fix is claimed here and the first job of
  the cycle that takes this ticket is to ATTRIBUTE it, not to implement a recovery.
- Related: `done/layer-change-member-dropout.md` (the umbrella ticket: its log-storm half landed
  2026-10-06, its item list still carries this work), `done/layer-mod-baseline-divergence-on-continue.md`
  (the same warning from another producer — a Continue that reopened the wrong world, a genuine detector,
  NOT this shape), `done/reenter-baseline-adoption.md` (a member that never left the session generates
  before the host's restored baseline arrives)

## What is observed (batch evidence, cause unknown)

| Fact | Where it comes from |
|---|---|
| At +4.1 s after the layer change the member had no local body at all: `container-read mode=local` → `no-local-body`, so that attempt's destroy probe found nothing to destroy and row 4 had to be staged again | the batch record's Limits; the artifacts `r4b-guest-local-t2.json` / `r4b-guest-local-t3.json` (`"error": "no-local-body"`) in the batch's artifact directory |
| During the same session the member's `state` read `role: Guest`, `inWorld: true`, `gateWaiting: false`, page `Home` — **corrected 2026-10-20**: that `inWorld` is `IWorldPresenceQuery.IsInWorldOrGenerating` (`tools/acceptance/driver/InProcessDriver.cs`: `"inWorld", presence != null && presence.IsInWorldOrGenerating`), so it is the GENERATING half of a re-entry, not a member believing it is in a world it is not in. The reading that distinguishes the two states is batch `20261007-a`'s `inWorld: false` + `no-local-body` at +60 s | `r4-guest-state.json` / `r4-alt-state.json`, same directory; the correction is the driver's own expression, quoted above |
| A SECOND layer advance follows the first on its own about 9 s later, so one `skiplayer` command is already a consecutive change; the host's `[LayerReset]` line appears twice per attempt | the batch record's Limits, `[LayerReset] dropped the previous layer's world-rooted items; …` |
| Nothing recovered on its own — the batch restarted all three clients cold and re-judged the remaining rows in a second session | the batch record's Sessions paragraph |
| The same window carried the log storm this repository has since bounded; whether the storm and the missing body share a cause, one causes the other, or both follow from the layer change is NOT known | `docs/backlog/done/layer-change-member-dropout.md` and `docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md` |

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

## The second reading (batch `20261007-c`, 2026-10-07) — the dropped pair came back by itself

Batch `20261007-c` re-drove the same fixture for `done/layer-change-member-dropout.md`, parking BOTH members
inside one command for the held attempt and driving the same command with no park as its control. Record:
`docs/evidence/acceptance/layer-change-member-dropout-20261007-c.md`.

- **Both members can be held at once.** With host, guest and alt all parked across the change (13 s on the
  members), both members read `inWorld: true` with `container-read mode=local` answering `localCount: 1` — the
  item they carried into the change — at +16 s and +34 s. The park is therefore repeatable per member, not a
  one-member trick.
- **The control's two members BOTH returned on their own.** With no park they were out at +6–9 s
  (`inWorld: false`, `localCount: 0`) and both read `inWorld: true` again at +30 s. "Never" is not this
  fixture's default outcome: the spread across the three batches (recovery inside 20 s, inside 30 s, and one
  member still out at +60 s) is what a recovery must be judged against, and it is also why a recovery cannot
  be "wait for the game".
- **The exit still follows the inbound the member processes.** The two attempts differ only in the park and
  the park decides them, on the same world minutes apart — the door batch `20261007-a` chose still stands.

## The recovery (landed 2026-10-20)

The door the reading chose is the world-entry instruction: `WorldJoin` is an EDGE the host sends once per
entry, and a member can miss it (the lazy-P2P swallow) or discard it against its own entry gate — an invite
that arrives while the member is still in its old world waits for the menu and is dropped when the old world
unloads. The fix makes the entry convergent without inventing a second one:

- **The member reports the fact the host cannot see.** `NetMsg.ReentryRequest` (guest → host, no payload; the
  transport sender is the identity) says "I am out after your own exit, take me back in". The window arms only
  when the HOST's exit is what takes this member out of a world — the member must be in, entering or generating
  a world at that moment (`ReentryCoordinator.OnRemoteSceneChanged` asks the run state), so a member that chose
  the menu never asks and the host never pulls anyone back on its own. The window is bounded: 12 asks at 5 s
  inside a 180 s window, sent only while the member is out with no entry in hand and the host is back in a
  world, and it ends when the member is in the world. The window itself is the pure `ReentryWindow`, and its
  first frame is the one that matters: the host's exit arrives while the member is still inside its old world
  (the menu return is deferred to the frame-end seam), so "in a world" is not "back" until the window has seen
  this member go out — `ReentryWindowTests` pins that frame, the regression of the first draft that armed and
  dropped the window in one frame and would never have asked at all.
- **The host answers with the ordinary instruction.** `ReentryRequestChannel` admits the ask only from a
  handshaken member its own presence table holds OUT of the world (a member it still holds IN world is refused
  by name — its bounded scene re-report is the settling path) and only outside a 2 s per-sender cooldown;
  `ReentryPolicy` then requires this host's live world and a committed kernel run baseline (the same fact the
  instruction makes its baseline promise from), and the answer is `SendWorldJoinTo` — the SAME targeted
  `WorldJoin` plus run-baseline checkpoint the click and the world-entry edge send. Both refusals are named and
  logged rather than filled with a fallback that would enter the member into a layer this host has not
  generated.
- **The red was staged, not waited for.** The acceptance recipe `net-drop-message` parks the client's receive
  seam and drops exactly one message family for a window, so the batch lost the single `WorldJoin` while
  every other frame arrived. Batch `20261025-a`'s first session ran it against the pre-fix artifact
  (`0.1.0+b8628640…`): the host logged one `World join sent to 2 member(s)`, the guest's log carries the host
  exit, its own exit and NO `World join received`, and at +80 s and +112 s it was still `inWorld: false` with
  `container-read mode=local` answering `no-local-body`, while the control member (invite delivered) returned
  with its two carried items. That is the defect's own reproduction, not a rate.
- **Self-check**: the mechanism inventory, the claim table and the independent review's findings are
  `docs/evidence/selfchecks/world/layer-change-member-recovery-selfcheck.md`.

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
  creates the body". The chosen shape is the entry re-drive the ticket names (the same shape
  `done/reenter-baseline-adoption.md` established): the member asks, the host re-issues the entry
  instruction.
- **Rows, all machine-read**: (1) after a consecutive layer change the member has a local body again and
  `container-read mode=local` answers with its inventory; (2) the member's world matches the host's
  (`[LayerReset]` count and the host's world-item table read after the member's own generation finished);
  (3) the third peer sees that member's clone in the new layer; (4) no cold restart was needed — the
  session's own line count and the presence of both members' reports after the change prove it stayed up.
  **Status after the first session of batch `20261025-a`**: row 1 is RED on the pre-fix artifact (the
  stranded member above); rows 1–4 are unjudged until that batch's second session re-runs the same
  provocation against the fix artifact.
- **Schedule this run last in its batch**: the storm and the members' exit out of the world are the
  machine's own cost, and the staging procedure, the recovery shape and the 15-second black-window ceiling
  are machine facts of the acceptance area's gitignored local files.

## Limits (the recovery's own)

- **It recovers a member that is OUT of the world, not one stuck reporting `IsGenerating`.** Batch
  `20261005-b`'s `+4.1 s` reading (`inWorld: true`, no local body) is the generating state as corrected
  above; whether a generation can outlive its window was not read, and this fix neither waits for nor
  repairs one.
- **A host that never comes back into a world is never entered.** The window only asks while the host is in
  a world; if the host stays in the menu, the member stays there too and the window ends after 180 s, which
  is what the log says.
- **The ask is not a second invitation path for a member that left on purpose.** The origin is the host's
  own exit; a deliberate leave, a death menu return and a session end do not arm it.

## Non-goals

- Re-attributing the log growth: bounded and pinned on 2026-10-06
  (`docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md`).
- Removing or weakening the divergence detector, and guarding the debug `skiplayer` command (it is the
  game's own console command).
