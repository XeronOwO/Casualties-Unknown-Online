# Sleep behavior policy decision

- Status: Todo
- Priority: Medium
- Category: Gameplay / sleeping policy
- Source: User backlog (2026-09-05)

## Open question

Sleep behavior needs a deliberate policy decision before implementation.

Candidate direction: disable/limit sleeping in multiplayer sessions, but this must
also account for forced-sleep effects such as the mushroom tail, which can
unavoidably put a player to sleep. The final rule needs to decide:

- whether normal player-initiated sleeping is allowed in a shared session;
- how forced sleep (mushroom tail / other mandatory sleep effects) is handled;
- how sleeping affects world-time acceleration, unconsciousness, and remote
  presentation;
- whether any existing sleep/synchronization behavior is affected by the chosen
  policy.

## Action

Record the decision (allow/disable/conditional sleep) and implement it as a
cooperative host-authoritative policy rather than a local time scale hack.
