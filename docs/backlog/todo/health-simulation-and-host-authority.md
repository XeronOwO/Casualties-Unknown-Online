# Health on every client, reconciled to the host's value

- Status: Todo — designed with the user on 2026-10-14; not started.
- Priority: Medium-High
- Category: Player state / cross-player sync / authority
- Source: the user's 2026-10-14 request while the validation family was being discussed: health should be
  simulated on EVERY client so each player's own state stays continuous (native feel), reported to the host
  periodically, and reconciled to the host's value — the host is the final result, which is also what makes
  the state authoritative.
- Related: `review/validation-and-source-checks.md` (the same conversation's other half: source, permission and
  data checks — this ticket owns the continuous-value half), `docs/backlog/future/generic-prediction-runtime.md`
  (this is a NARROW slice of it: one known family, one model), `docs/decisions/active.md` 184 (the design
  principle this must not break: experience judged locally, conflict arbitration on the host, latency is never
  a decision parameter), `docs/development/agent-reference.md` (the sync model), `docs/architecture/current.md`
  §7.1 (the authority inventory), `done/item-data-write-commands.md` (the same "who may state what" question
  on the item side)

## What the user decided (2026-10-14)

- **Simulated on every client, for EVERYONE** — not only one's own body. Multiplayer exists so players
  interact, and the other players' state has to look continuous on my screen too.
- **Reconciliation is immediate**: any divergence is aligned at once, and a visible jump is accepted (the
  values are not expected to differ much).

## The model this ticket has to implement

Two halves, and the whole design turns on keeping them apart:

1. **Events** — what happened: a bite, a fall, a medical action, a limb breaking. These are NOT simulated;
   they are decided once, by the side the existing rulings already give them to (the affected client judges
   its own injury and medical actions; the host owns the enemies and the environment), and they travel as
   events. A peer that invents an event is a `review/validation-and-source-checks.md` problem, not this one.
2. **Continuous values** — what the state does BETWEEN events: bleeding, decay, the slide a wound causes.
   This is what each client simulates for every body it can see, from the last authoritative event, so the
   panel moves smoothly instead of stepping once per report.

Only continuous values are simulated; an event-shaped flag (a broken limb, a splint, infection, unconscious,
dead) is broadcast and never predicted — predicting one would create a disagreement with no way to hide it.

**The reconciliation rule (the user's second decision):** the host's value is the final result and a
divergence is aligned immediately, jump accepted. The local simulation is therefore only ever ahead of or
behind the host's number by the time one report takes to travel, and the next authoritative value overrules
it in place.

## The hard question this ticket must answer first

**What lets the host compute the value it is the authority for?** Today the kernel stores player state but
carries no injury model — the model lives in the game's client-side limb scripts. Two shapes:

- **(a) Plausibility only**: the host checks the reported value against bounds, its own last value and the
  elapsed time (a blood volume cannot fall 80 in one second), refuses or aligns what cannot be true, and keeps
  the reported value as the fact.
- **(b) A host-side model** of the same continuous values for every body, so the host computes rather than
  checks. This is what makes "the host is the final result" mean something against a client that reports a
  plausible lie, and it is the expensive half: the model is game code, and the host would run it for bodies it
  does not render.

The ticket's first job is to establish which of the two the landed implementation is, with evidence. The
recommendation is (a) first **with the limit written down in the player-facing terms** ("a client that lies
plausibly is not caught by this; that is the validation family's job"), and (b) as its own later step.

## What the implementation reuses rather than invents

The pattern already runs for the item family and its cost is known:

- `CharacterDataSync.ReportCharacterData` — the 1 Hz self-report of a character snapshot
  (`CharacterDataMsg`: health, limbs, items).
- `CloneFactTable` — events apply immediately, the next snapshot is the authority, and
  `WarnOnDivergence` reports what the event chain missed; `CloneFactTableDivergenceMonitorTests` pins both
  directions (an announced change leaves the next snapshot quiet, a missed one warns on it).

Health follows that shape instead of growing a second one: per-owner continuous state simulated locally,
events applied on arrival, the periodic snapshot reconciling, divergence observable rather than silent.

## Acceptance (the rows a run must read)

Two clients (plus a third where a row needs it), one of them with an artificial delay and packet loss:

| # | Scenario | Expected |
|---|---|---|
| 1 | A guest bleeds while its own panel is open | The panel moves continuously (no 1 Hz stepping), and the host's row agrees once the report lands |
| 2 | The same on the host's screen for the GUEST's body | The guest's state reads continuously on the host too — every client simulates every body |
| 3 | The host's value is forced away from the client's simulated value | The client aligns at once (a visible jump is accepted) and both sides read the host's value afterwards |
| 4 | An event the event chain missed (the report lost) | The next snapshot carries it, and the divergence is warned on the side that can see it — never a silent step |
| 5 | A client reports a value the host's bounds cannot produce | The host refuses or aligns it, and the refusal is a named log line (this is where (a) ends and the validation family begins) |
| 6 | A limb flag (broken, splinted, infected) changes | It is broadcast, not predicted: no peer shows it before the authoritative event |

## Limits to write down rather than hide

- **A plausible lie is not caught by (a).** Only event/data validation (`review/validation-and-source-checks.md`)
  or a host-side model (b) closes that.
- **Simulation for everyone costs work on every client** (N bodies, not one) — the frame budget has to be
  measured before the design is called affordable, and the measurement belongs in this ticket.
- **This is not the generic prediction runtime.** It is one family with a known model; the generic item
  (`future/generic-prediction-runtime.md`) stays deferred.

## Non-goals

- Movement, animation and other behaviour: left as they are (the user's own call, 2026-10-14) — they cannot be
  inferred from a numeric model the way a continuous value can.
- Anti-cheat as such: this ticket makes the host the final value for the continuous half only.
