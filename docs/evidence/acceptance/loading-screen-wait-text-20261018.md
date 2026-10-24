# Acceptance — what the waiting line says, read off two clients

- Date: 2026-10-18
- Ticket: `docs/backlog/review/loading-screen-progress-detail.md`
- Batch: `.acceptance/20261018-b/run.ps1` (two clients: host + one sandboxed member)
- Delivered artifact: `0.1.0+c13cebff9120b57d038e9d2784171cea357e8d21` — the tree's own HEAD, confirmed by
  `verify-deploy.ps1` before the clients started
- Log: `.acceptance/20261018-b/run2.log`. The readings are `gateText` from the driver's `state` action, which
  is the overlay's own line — nothing here is inferred from a row changing.

## The three claims, judged

An adversarial review found the FIRST wording of this feature asserting things the printing client cannot
know, and each of the three sentences below is one of those claims, now read off a live session:

| # | Which side | What it read | Verdict |
|---|---|---|---|
| 1 | member | `Waiting for the host to start the run…` | **no phase claimed for the host** — the flag it reads is false for the menu, for loading and for "no report seen yet" alike, so the line says what this client is waiting for and nothing about what the host is doing |
| 2 | member | the same line | **no "everyone is here"** — a member's last stage is its own wait, not a claim that all players arrived |
| 3 | host | `Waiting for the remaining players to load… (0s). The group starts together.` | **names rather than counts** — the old line was `Waiting for 0 player(s) to load…` |

Readings repeated 9 s apart were identical, so the line is stable while the wait lasts.

## What this run did NOT judge

- **A name in the host's line.** The host fell back to `the remaining players` because by the time it was read
  the member was already in the world, so the roster was empty. The fallback is itself a case (judged), but the
  NAMED form — `Waiting for Ada to load…` — has no runtime reading. Reaching it needs the member held back
  while the host is read, which this batch does not have a lever for.
- **Whether the longer lines fit the overlay panel.** The new lines are about twice the old length and
  `StartGateOverlay` was not resized; whether they wrap or clip needs the screen, not a reading.
- **The gate's own window.** `gateWaiting` read `false` on both sides in both samples, i.e. the wait had already
  been released by the time the batch looked. The text still reads correctly outside the gate, which is why the
  three claims could be judged at all — but a reading taken DURING the hold would be stronger.

## The first run of this batch was worthless, and said so

Run 1 read `Waiting for 0 player(s) to load… (0s)` and `Starting…` — the OLD wording. It was not a regression:
`verify-deploy.ps1` had already failed with `Deployment mismatch: … Commit first, then re-run tools/deploy.ps1`,
because the batch ran against the artifact deployed before the commit that changed the wording. The batch
printed that verdict and the stale text side by side, which is what made the cause obvious in one reading.

**What that changes**: a batch that reads a deployed artifact must treat a failed verification as a reason to
STOP, not as a line to log. Run 1 wasted a full two-client cycle because the batch continued past it. The
deploy step is now part of the same cycle as the commit, and the verification line is the gate for it.
