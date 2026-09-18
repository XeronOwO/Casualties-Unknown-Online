# Remote interaction gates are judged by the host

- Status: Todo
- Priority: Medium-High
- Category: Network / sync coverage / player interaction (gate authority)
- Source: User ruling 2026-09-18 (design alignment session): the line-of-sight / precondition gates of remote interactions must be judged locally by the two clients involved; a host verdict on stale streamed positions refuses interactions the actor's own screen shows as valid.
- Related: `review/enemy-hit-determination-local.md` (the same authority rule for hits), `todo/concurrent-medical-operations.md` (the reservations half of the same services), `review/remote-medical-panel-acceptance-issues.md`

## Problem (evidence)

Two host-side judgment sites decide whether a guest may interact, using data that is
stale by the peer's own latency:

- Line of sight: `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionVisibility.cs`
  resolves both players' positions from the entity stream (the local body only for the
  local player) and runs a Ground linecast between them. It is consumed by host-side
  services — `OtherMedicalOperationSessionService`, `MedicalOperationSessionService`,
  `ShrapnelOperationSessionService`, `PlayerCarryService`, `PlayerPushService`,
  `PlayerInventoryTakeService`, `PlayerRemoteInventoryService`, `PlayerItemUseService`,
  `PlayerHealService` — so a guest-vs-guest interaction is gated by the HOST's picture of
  where both players stand.
- Operation preconditions: `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalOperationStartValidator.cs`
  validates the target's limb state and the operator's item from `CharacterDataMsg` inside
  `OtherMedicalOperationSessionService` ("Host-authoritative Stage 3 medical operation
  domain"), so a refusal can be produced from a report the target's own client would
  contradict.

The policy is already deliberately forgiving (missing evidence does not block, only a
confirmed wall does), which limits the damage but does not change whose eyes decide. The
user's rule for this family is that the actor's own screen decides reachability, and the
target's own body decides what its body allows.

## Goal

The actor's client judges "can I reach/see the target" from its own view; the target's
client judges "does my body currently allow this operation" from its own body. The host
stops being the judge of either and keeps what is genuinely its own: conflicting-claim
arbitration and the commit/broadcast of the effect.

## Design direction (decide at implementation)

1. Visibility moves to the ACTOR's client: the scene it renders is the scene it acts in.
   The host-side services stop calling the visibility oracle for a remote actor and take
   the actor's own verdict (reported with the request), or the request is only formed
   after the actor's client has passed its local gate.
2. Preconditions move to the TARGET's client for anything about the target's body (limb
   state, existing splint/tourniquet, dislocation, shrapnel count) and to the ACTOR's
   client for anything about the actor's own items. A refusal is then a fact the refusing
   client actually observed, not an inference from a 1 Hz report.
3. The host keeps the exclusive-claim arbitration and the effect commit; the answer to a
   losing claim stays a precise, immediate refusal (accepted tradeoff, user ruling
   2026-09-18: conflict arbitration stays host-authoritative).
4. Audit the whole family, not the reported case: every `IPlayerInteractionVisibility`
   caller and every precondition validator moves in the same cycle, or is recorded with
   its reason for staying.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | High-latency actor whose own screen shows a clear line to the target | The interaction is not refused for visibility |
| 2 | The actor's own screen shows a wall between the two | The interaction is refused (the actor's own view is the judge) |
| 3 | Target limb state differs between the target's body and the stale report | The target's own body decides the answer |
| 4 | Two actors claim the same exclusive unit | The host still arbitrates; the loser gets an immediate precise refusal |
| 5 | Host-local interaction | Unchanged (native path) |
| 6 | Third-party view | Unchanged: the effect and its report are the shared truth |

## Non-goals

- Removing host arbitration of conflicting claims (user ruling: it stays).
- Changing the native UI of any interaction.
- Anti-cheat hardening (a client that lies about its own reachability is out of scope).
