# Host rules reach the members, and both sides check them

- Status: Todo — recorded 2026-10-14 at the user's direction when the member console write closed (S1b of
  `todo/validation-and-source-checks.md`); not started.
- Priority: Medium-High
- Category: Session policy / host rules / client-side checks
- Source: the user's 2026-10-14 instruction, in their words: the rules have to reach the members; KrokMP lets
  the host adjust rules WHILE a session runs, so a mid-session change has to travel as an event too; and a
  client has to check as it operates — what is forbidden is intercepted locally — while the host keeps its own
  check behind it, "the way a web app validates on both the front end and the back end".
- Related: `docs/en/reference/configuration.md` (`[HostRules]`, the twelve rules), `docs/decisions/active.md`
  210 (the admission seam, which stays the last word on anything a member submits),
  `todo/remote-backpack-access-policy.md` (the first consumer: `AllowRemoteInventoryTake` and the member-side
  take affordance), `review/item-data-write-commands.md` (the second: the console write's rule and the flicker
  this ticket removes), `docs/architecture/protocol.md` (world-entry facts and the in-session repair group)

## What is asked

1. **The rules a member's client ACTS ON reach that client.** Which those are is a census rather than the whole
   list: a rule only the host executes needs no replication, and a rule whose value a member's screen, console
   or local gesture reads does. The question is answered per rule and kept answered — the shape
   `WireCommandAuthorityPolicy` uses for wire kinds (one entry each, a gate that fails on an unclassified
   member) is the precedent, and a rule added later must not be able to skip the question.
2. **A change while the session runs is an EVENT.** A host that flips a rule mid-session (the Admin page, the
   config file, or the console's host-rules command) must reach every member at that moment, not at the next
   world entry — KrokMP adjusts rules during play and parity is the requirement.
3. **Both sides check, and the host is still the authority.** A member's client refuses locally what the
   current rules forbid (so a forbidden gesture does not happen and does not have to be undone), and the host
   checks the same thing on its own side, because a client's belief can be stale, lost or forged — the local
   check is an interception layer over the operator, never the verdict.

## What exists today (the gap, with evidence, before anything is designed)

- **Host rules are local configuration by decision**, and the decision is written on the surface every consumer
  uses: `IHostRules`' own summary says "host rules are local host configuration, not a protocol message", and
  `HostRulesPolicy`'s says the same. A member's process therefore holds its OWN values under the same names.
- **A member's screen already reads one of them as if it were the host's rule.**
  `OnlineUiMemberListDrawer` passes `ctx.HostRules.AllowRemoteInventoryTake` into
  `OnlineUiMemberProjection`, which turns it into each row's `CanTake`; on a member that value is the member's
  own config, while the rule itself is enforced by the host (`PlayerInventoryTakeService`). The two can
  disagree in either direction, silently, and neither side can tell.
- **The console write this cycle added is the second instance of the same hole.** `AllowMemberItemDataWrite`
  is judged on the host at the admission seam, and the member's console applies the value and reports it
  before the verdict returns — so on a host whose rule is closed the operator watches the value appear and go
  back (`review/item-data-write-commands.md`, Limits). The rule is not unknown because it cannot be known; it
  is unknown because nothing carries it.

## The questions this stage owns

- **What the announcement carries.** The set of rules the census marks as member-known, sent ABSOLUTE (the
  whole set, not a delta) so a member that missed one can be put right by the next one — the same shape the
  world-entry facts and the in-session repair group already use.
- **How a change travels.** The host's config is read through `IOptionsMonitor` and a change is already
  observable there (the Admin page writes the entry and the monitor fires); the event is what turns that edge
  into one message to every member. A change that arrives while a member is joining, or between two messages,
  must converge — an absolute set plus the repair group is the cheap answer, and the ticket must say which.
- **What the local check may do, and what it may never do.** It may refuse a gesture the current rules forbid,
  with the answer in the operator's words; it may NOT be the reason something is allowed, and it may not
  contradict the host: a member whose belief is stale must not block something the host would accept, so the
  local refusal names the rule and the announced value it acted on, and the host's own refusal stays the one
  that settles the record. The judgement-ownership rule is untouched: the host arbitrates, the affected client
  judges its own experience, latency is never a parameter.
- **What happens with no announcement at all.** A member that never received the set (a version skew, a
  swallowed entry message) must fall back to the behaviour of today rather than to a guess: the local check is
  skipped and the host's verdict is the only one, which is exactly the current model.
- **The wire shape.** Whatever carries it is a protocol member and takes the version policy with it; the
  announcement is host → member only, and the member never reports a rule back.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | A host flips a rule the member's screen reads, while both are in a world | The member's next look at the surface reflects it within one announcement, and no local gesture is refused on the old value |
| 2 | A member's client holds a rule the host has just closed | The forbidden gesture is refused LOCALLY, in the rule's own words, and nothing is applied and undone |
| 3 | The same gesture, forced past the local check (a modified client) | The host refuses it exactly as it does today, so the local check is not the boundary |
| 4 | A member joins a running world after the host changed a rule | The member enters with the current set, not the values from the host's config file at its own launch |
| 5 | An ordinary session, unmodified clients | Nothing changes for the host, and nothing is refused that was not refused before |

## Non-goals

- Not a permissions system for mods or players: the rules stay the host's own switches.
- Not a second verdict: the host's enforcement is unchanged, and no rule becomes a member-side authority.
- Not the anti-cheat stage: `todo/validation-and-source-checks.md`'s S2 and S3 question the CONTENT of a
  report, which no announcement can do.
