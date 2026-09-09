# Runtime entity creation the host cannot represent must be REJECTED (and the creator's copy destroyed)

- Status: Todo
- Priority: High
- Category: Network / sync coverage / world entities
- Source: user review 2026-09-09 — the round-3/round-4 handling of an unmaterializable creation was rejected as a spec violation; spec corrected in `AGENTS.md` (accept-first precondition) + decision 161
- Related: `review/runtime-entity-spawn-backfill.md` (its finding-3 fix is SUPERSEDED by this ticket), `AGENTS.md` "Accept-first sync arbitration — only for state the host can represent", `docs/decisions/active.md` #161

## Problem (evidence)

`AGENTS.md` now states the accept-first precondition explicitly: accept-first exists to avoid
hard validation and player-blocking corrections, **never to accept state the host cannot own**.
A report the host cannot represent is rejected, and the rejection must be visible.

The current code violates that. `RuntimeEntityChannel.ReportEntitySpawnUnmaterialized`
(`src/CasualtiesUnknownOnline.Runtime/Session/World/RuntimeEntityChannel.cs`, called from
`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs` in
`OnRemoteEntitySpawned`'s failure branch) **relays the creation to every member without
recording it** — neither accept nor reject:

- peers that have the prefab materialize an entity the host can never own, back up or retract;
- the reporter's own echo clears its pending entry, so the divergence is silent;
- the E3 ticket carries a "a member joining after the creation never receives it" limitation
  that only exists because of this half-state;
- a creation can therefore exist for some peers and not others with no owner responsible for
  it — exactly the unowned-accept the corrected rule forbids.

## Required behaviour (decided — do not re-litigate)

1. **Host rejects.** A reported creation the host cannot materialize is neither recorded nor
   relayed. Log at Warn with the concrete mismatch: prefab id, creation key (id + cell +
   creator + sequence), reporter SteamId, and the fact that it was rejected.
2. **The rejection is visible.** The host answers the reporter with a dedicated message
   (`NetMsg.RuntimeEntityRejected`, host → guest, creation key + reason code) so the
   reporter's `PendingEntityReportTable` entry is dropped and the 60 s fallback stops; the
   guest logs the reason ("host lacks prefab X").
3. **The creator destroys its local copy** (user decision 2026-09-09: this is the obvious
   answer — nobody keeps an entity the host cannot own). Destroy through the same death
   funnel the rest of the mechanism uses, so the pending/accepted tables are cleaned in the
   same step; locate the copy by its stamped `RuntimeEntityCreation` key (never by position),
   and make it idempotent when the copy is already gone. Log the removal with the reason.
   The round-trip window is real (the entity may have moved or been used) — the destruction
   still happens, and the log is what makes it understandable to the player.
4. **Third parties receive nothing.** A member that happens to have the prefab must not
   materialize a creation the host rejected; the session's content sets are inconsistent and
   the rejection log is the signal to fix that, not a partial relay.
5. **The accepted path is unchanged**: when the host CAN materialize the creation, today's
   accept-first flow (materialize → record → relay to every member, source included) stays.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Host lacks the prefab; guest creates and reports | Host records nothing, relays nothing, logs the mismatch; no third-party materialization |
| 2 | The reporter receives the rejection | Pending entry dropped; the local copy destroyed via the death funnel; warning logged |
| 3 | The rejection message is lost | The fallback re-reports; the host rejects again (idempotent); the state converges on the next successful rejection |
| 4 | The rejection arrives after the local copy already died | No-op (key gone) |
| 5 | The local copy moved before the rejection arrived | Found by creation key and destroyed (position-independent) |
| 6 | The host HAS the prefab | Unchanged accept-first path: materialize + record + relay |
| 7 | A member joins after a rejected creation | Nobody has it (converged) |
| 8 | A non-reporter receives the rejection message | Ignored (direction-locked host → guest; only the reporter's own key matches) |

## Red before green (hard gate)

Add the regression test first and record it failing on the current tree: the host relays an
unmaterializable creation and the reporter keeps its copy. The test must assert all three of
"no relay to a third member", "the reporter's pending entry is cleared", "the reporter's local
copy is destroyed" — it fails today on at least the first and third.

## Implementation notes

- New wire message: `NetMsg.RuntimeEntityRejected` (host → guest), carrying the creation key
  (`RuntimeEntityKeyMsg`) + a reason code; `ProtocolVersion.Current` bump; the matrix's wire
  vocabulary index entry + a `NetPacketTests` round-trip; the evidence anchors for the new
  members.
- The adapter owns the destruction: `EntitySpawnSync` (or its channel bridge) finds the copy
  by `RuntimeEntityCreation` key and routes it through the death funnel; `BuildingEntity`'s
  own death hook then drops the pending entry as usual.
- Reason codes: start with one (`PrefabUnavailable`); extend only when a second real cause
  appears.

## Docs to update when this lands

- `review/runtime-entity-spawn-backfill.md`: finding 3 rewritten (reject, not relay) and the
  "late joiner never receives it" limitation removed — it was a symptom of the unowned accept.
- `docs/evidence/sync-coverage-matrix.md` row E3 loss-semantics cell + the wire vocabulary
  index + `docs/evidence/sync-coverage-evidence.json` anchors.
- `docs/backlog/README.md` index entry (the README is in flight in a concurrent session at
  ticket-creation time, so this line is intentionally not added here).
