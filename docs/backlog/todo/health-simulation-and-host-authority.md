# Health on every client, reconciled to the host's value

- Status: Todo — designed with the user on 2026-10-14; not started. **Reopened 2026-10-18** by the user's
  authority question below: whether the VALUE moves to the host (the client only presenting, with a rollback on
  divergence), which would strengthen this ticket's model and re-decide its (a)/(b) recommendation at kickoff.
- Priority: Medium-High
- Category: Player state / cross-player sync / authority
- Source: the user's 2026-10-14 request while the validation family was being discussed: health should be
  simulated on EVERY client so each player's own state stays continuous (native feel), reported to the host
  periodically, and reconciled to the host's value — the host is the final result, which is also what makes
  the state authoritative. The 2026-10-18 question (below) asks whether the host should be the SOURCE of the
  value rather than the reconciler of a client-stated one.
- Related: `done/validation-and-source-checks.md` (the same conversation's other half: source, permission and
  data checks — this ticket owns the continuous-value half), `docs/backlog/future/generic-prediction-runtime.md`
  (this is a NARROW slice of it: one known family, one model), `docs/decisions/active.md` 184 (the design
  principle this must not break: experience judged locally, conflict arbitration on the host, latency is never
  a decision parameter — the 2026-10-18 question may supersede it FOR PLAYER STATE, which is a ruling, not an
  implementation detail), `docs/backlog/todo/cheat-permission-and-op-commands.md` (the cheat verbs' two-sided
  check — note that the tree's `/heal` is NOT in that family: it consumes the healer's own item, so it is the
  player's own legitimate action, and the exclusion is recorded on that ticket; a future no-item heal verb is
  the one that would join it), `docs/development/agent-reference.md` (the sync model), `docs/architecture/current.md` §7.1 (the authority
  inventory), `done/item-data-write-commands.md` (the same "who may state what" question on the item side)

## The user's authority question (2026-10-18) — is the value the HOST's, or the player's own report?

In the user's own words: "当前玩家的健康状态计算是以对应玩家的本地实例为准的吧?那会出现本地玩家可以肆意修改自己状态
的情况吧,例如使用控制台的 `heal` 等命令。是否有可能把状态的权威值挪到主机上,客机跟其他人一样也只是个类似本地播放
的形式,只是利用本地计算让玩家看起来像是完全本地计算的一样,实际还是应当走上报主机仲裁回退这条路。可行吗?"

So the question is not the framing above (reconcile the client's stated value); it is whether the host should be
the SOURCE of a player's own state, with that player's client a local presentation/prediction over it: the client
computes locally for feel, reports, the host arbitrates, and a divergence ROLLS BACK. Feasibility is to be
established and the decisions taken at kickoff, not now.

**What this changes relative to the model above**: this ticket's 2026-10-14 model has EVENTS judged by the side the
existing rulings give them to (the affected client judges its own injury and medical actions — decision 184) and
only continuous values reconciled to the host, with the reported value kept as the fact when it is plausible. The
user's question is what the *hard question* below calls (b) pushed all the way: the host COMPUTES/states the value
and the client predicts. That reverses who is the source for a player's own body, so decision 184's ownership rule
must be revisited for player state — with the user, as a ruling.

**What is already in place, so the work is bounded**:

- the 1 Hz self-report and its reconcile rule (`CharacterDataSync.ReportCharacterData`, the host value wins and an
  alignment is immediate — the 2026-10-14 decision accepts the jump);
- the divergence monitor that makes a missed change observable (`CloneFactTable.WarnOnDivergence`);
- the heal request is already guest → host (`NetMsg.PlayerHealRequest` = "guest → host: use a carried medical item
  on another in-world player", handled by `PlayerHealRequestHandler` → `HandleHealRequest`), so a member's
  `/heal` is not a purely local effect — but the player's own CONTINUOUS state and its events are still produced
  by that player's client, which is the surface the question names.

**What research must answer before a design is chosen**:

1. **What the host has to run to be the source** — the injury model is game code in client-side limb scripts; the
   host would have to compute it for every body it does not render (the hard question's option (b)), including the
   frame-cost measurement this ticket already demands.
2. **What rolls back, and how the player sees it** — the 2026-10-14 decision accepts a visible jump for a
   continuous value; a rollback of a whole state on a divergence may be more visible, and whether it is presented
   as the host's value or as an explicit "corrected" moment is a user ruling.
3. **Which events stay locally judged** — decision 184 covers injury, interaction gates and action initiation.
   Moving the player's own STATE to the host does not move those automatically; the split has to be named, and the
   seat of "the affected side judges its own experience" restated for the cases that keep it.
4. **What a self-targeted cheat verb becomes** — under a host-stated value, `/heal`'s own-body effect is no longer
   the client's to apply; it becomes a request the host judges (the permission ticket's family row), which is where
   the two tickets meet.
5. **Which line this crosses** — the MVP excludes generic client prediction, and this asks for a narrow form of it
   for one family. `future/generic-prediction-runtime.md` stays deferred, and the ticket must say what makes this
   slice narrow (one model, one family, no movement/animation).

**The decision the user will be asked at kickoff** (recorded here so it is not re-derived): does the host become
the source of a player's own health — superseding this ticket's (a)-first recommendation and, for this family,
decision 184's ownership — and on a divergence does the client present the host's value as a jump, or as an
explicit correction?

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
   events. A peer that invents an event is a `done/validation-and-source-checks.md` problem, not this one.
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

- **A plausible lie is not caught by (a).** Only event/data validation (`done/validation-and-source-checks.md`)
  or a host-side model (b) closes that.
- **Simulation for everyone costs work on every client** (N bodies, not one) — the frame budget has to be
  measured before the design is called affordable, and the measurement belongs in this ticket.
- **This is not the generic prediction runtime.** It is one family with a known model; the generic item
  (`future/generic-prediction-runtime.md`) stays deferred.

## Non-goals

- Movement, animation and other behaviour: left as they are (the user's own call, 2026-10-14) — they cannot be
  inferred from a numeric model the way a continuous value can.
- Anti-cheat as such: this ticket makes the host the final value for the continuous half only.
