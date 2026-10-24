# Cross-player item use: row 1's grip slip, with before and after on both sides

- Ticket: `docs/backlog/done/cross-player-item-use-random-determinism.md`
- Batch: `20261025-b` — one live two-client session, artifact `0.1.0+c08c99ad…` (both clients' own load line)
- Instruments: `grip-slip-drive` (probe → open → start → `pieces` → break), `limb-read` (local / display), `seed-shrapnel`, `body-force`

## What the row says

Row 1: a bare-hand shrapnel pull that breaks the grip — the affected client's skin/bleed/pain before and after agree with
what the host committed, and the operator's display agrees too.

## How the drive had to change

Two earlier attempts broke with no game to break in (`no-shrapnel-minigame`). The missing capability was a read of the
minigame surface, added as `grip-slip-drive mode=pieces` (the minigame's type and piece count). That alone was not enough:
the start also refuses unless the patient is responsive and the displayed limb carries shrapnel
(`MedicalTargetBodyValidator`: `Shrapnel` requires `health.Conscious`, and `limb.Shrapnel <= 0` is refused). So the batch
wakes the patient first (`body-force consciousness=90,brainHealth=100,bloodPressure=100`), seeds two shrapnel, and **polls
the operator's display until the seed has propagated** before starting.

## The reading

Host body awake; both sides read the same before state after the seed propagated (`skin=100 bleed=0 pain=0 shrapnel=2`).
Then the guest (operator) started the remote special and broke the grasp on limb 1:

- `start` → `started=true`
- `pieces` → `ShrapnelMinigame`, 5 pieces
- `break piece=0` → `ok`

One second later:

| side | skin | bleed | pain | shrapnel | muscle |
|---|---|---|---|---|---|
| patient (host local, limb 1) | 94.805 | 0.587 | 5.404 | 2 | 100 |
| operator display (guest, limb 1) | 94.805 | 0.587 | 4.837 | 2 | 100 |

Four values agree **exactly**; pain is the one continuously decaying value and its two readings were taken about a second
apart, which is why it differs (the same decay is visible across every pair of readings in this session).

## Row 2 — the late joiner

The alt client joined the running session after the slip (`join-lobby` answered ok, `role Guest`, `inWorld` true on the first
poll), opened the host's medical view through the production entry (`medical-open` → `opened true`), and read the same limb
through its own display:

| side | skin | bleed | pain | shrapnel |
|---|---|---|---|---|
| late joiner, display of the host's limb 1 | 94.805 | 0.587 | 0.779 | 2 |
| host, its own limb 1 (read immediately after) | 94.805 | 0.587 | 0.779 | 2 |

All four values identical, including pain this time because the two reads were taken back to back: the late joiner landed on
the committed outcome of the slip, not on a fresh roll of its own. **Row 2 passes.**

## Verdict

**Row 1 passes**: the slip's damage appears once, identically on the patient's own limb and on the operator's display, so
the two machines did not each roll their own outcome. **Row 2 passes**: a client that joins afterwards reads the same
committed values. Rows 3, 4, 5 and 8 are settled elsewhere in the ticket: row 3 is withdrawn as unreachable (no injectable
liquid rolls per call), row 4 is the same unreachable shape on the drink chain and is withdrawn with it, row 5's equality was
read by the earlier batch, and row 8 is the user's perception residual the ticket already names.
