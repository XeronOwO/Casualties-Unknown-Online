# Source, permission and data checks on what a member reports

- Status: Todo — the user promoted this on 2026-10-14 (it was `future/strict-validation-anti-cheat.md`,
  deferred); the old ticket stays as the inventory behind it.
- Priority: Medium-High
- Category: Security / report validation / host rules
- Source: the user's 2026-10-14 request: no advanced anti-cheat, but BASIC checks from the game's own
  mechanics — a destruction report carries its source and must match the drop table (kinds, counts, the
  probability window for a non-integer roll), a command's report needs the sender to hold the cheat
  permission, an item use needs the item to exist in a state that allows it.
- Related: `future/strict-validation-anti-cheat.md` (the inventory this ticket works from — the wire mapper
  stamps every member command with the same authority kind, so the admission seam is a no-op today),
  `todo/health-simulation-and-host-authority.md` (the continuous-value half of the same conversation),
  `review/item-data-write-commands.md` (its write verb is host-only today for exactly this reason: a command
  write is byte-identical to a native use on the wire), `docs/decisions/active.md` 210 (the admission seam),
  `docs/evidence/sync-coverage-matrix.md` (a new wire kind owes it a row), `docs/architecture/current.md` §7.1

## The three stages

1. **S1 — source and permission.** Declare each wire command's real authority in the mapper (today every
   member command is stamped `OwnerPredictedHostValidated`, so the seam that judges eligibility reads the same
   value for a member's `RunStart` and for a member's item use); give a command its own identifiable path so
   the host can tell "the player ran a command" from "the player used an item"; admit it only when the sender
   holds the permission the host's rules grant. Also close the gap the inventory names: a member's item-state
   report is accepted for ANY existing non-terminal row — there is no ownership check on that command kind.
   This is the cheapest stage and the one the write verb is waiting for.
2. **S2 — the drop table.** A block or entity destruction report carries its drops; today the host registers
   them first-writer-wins and never checks them against the block's own table. The host re-derives the allowed
   set (kinds, counts, and for a random roll the probability window) and refuses what is outside it — the
   refusal path already exists (`ItemRejectMsg.Reason.BlockAlreadyBroken` makes the reporter destroy the drops
   it rolled). FIRST: establish with evidence whether the drop table is reproducible on the host at all, and
   how its randomness is drawn; the answer decides whether this is a check or a bound.
3. **S3 — state preconditions.** An item use must name an item the host records, in a state that permits that
   use; today the use report's digest is compared with the record and the client is CORRECTED when they
   disagree (`ItemArbitration`) — "wrong but equal-shaped" heals, "not allowed at all" passes. Turning the
   correction into a refusal needs the missing half named: what the client does locally when the host says
   "that did not happen".

## Rules every stage obeys

- **A refusal needs a way back to agreement.** A host-side refusal with no correction path leaves the two
  sides' pictures different, which is worse than not checking. The models that exist: accept-and-correct
  (items), and refuse-and-roll-back (block drops).
- **Reordering is not cheating.** A report can arrive before the creation it depends on (the codebase already
  handles creation-before-operation); a check that cannot tolerate that would refuse ordinary play.
- **Say what cannot be verified.** "Is the item really in that member's body" has no proof on the wire today;
  it is trusted and logged, and the ticket says so instead of implying coverage.
- **No compatibility layer.** The protocol may change freely before release (the handshake refuses a mismatch),
  so a new check replaces the old shape rather than living beside it.
- **The design principle is not negotiable**: the affected client judges its own experience, the host
  arbitrates and owns the world, latency is never a parameter of a judgement.

## Acceptance (the rows a run must read)

A two-client session where one client is modified by hand or by a test harness to send what a player cannot:

| # | Scenario | Expected |
|---|---|---|
| 1 | The client reports a block break whose drops are outside the block's own table | Refused by name on the host, the client rolls its drops back, both sides agree |
| 2 | The client reports a break whose drops are inside the window but at the edge | Accepted (a legitimate roll must never be refused) |
| 3 | A member sends a command report | Refused unless the host's rule grants that member the command, and the refusal is a named log line |
| 4 | A member sends an item-state report for somebody else's item | Refused by name (the ownership check S1 adds) |
| 5 | A report arriving before its creation | Accepted in the documented order, not refused |
| 6 | An ordinary session, unmodified clients | Nothing changes: no new refusals, no new logs at Warn |

## Non-goals

- No player-state simulation, no movement validation: the continuous-value half is
  `todo/health-simulation-and-host-authority.md`, and behaviour stays as it is.
- No bans, kicks or scoreboards: a check answers the report, and what to do about a repeat offender is a
  later decision (the user's 2026-10-14 call: "refuse and correct, log it").
