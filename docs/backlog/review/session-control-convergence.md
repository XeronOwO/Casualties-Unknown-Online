# Session control convergence: lost readiness/control messages have no re-report

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / session control
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` rows R3, R4, W7 marker finding)
- Related: `review/guest-block-mutation-re-report.md` (same "swallowed event" family)

## Problem (evidence)

The session/world-entry control path is reliable at the transport but has no
re-report when a message is swallowed while both peers stay connected.

1. **HandshakeAckAck loss is not healed.** The guest retries the handshake at
   1 Hz (`src/CasualtiesUnknownOnline.Runtime/Session/SessionPeerMaintenance.cs`,
   `RetryHandshakeIfNeeded` / `HandshakeRetryInterval`) but only while the session is
   inactive (`src/CasualtiesUnknownOnline.Runtime/Session/SessionService.cs`,
   `if (!SessionActive)`); the guest flips `SessionActive` when the ack arrives
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeAckHandler.cs`,
   `session.SessionActive = true;`). The host only marks the member on the third leg
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeAckAckHandler.cs`,
   `member.Handshaken = true;`). A lost `HandshakeAckAck` therefore leaves the
   host's member unconfirmed for the rest of the connection (start gate and
   `WorldReady` fan-out exclude it) while the guest believes it is connected.
2. **SceneState / PlayerJoin have no re-report.** `SceneState` is emitted only at
   the local InWorld/InMenu edge
   (`src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs`,
   `_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName, pos);`), and
   `PlayerJoin` once per member sync start
   (`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EntitySyncService.cs`,
   `StartMemberSync`). A swallowed `SceneState` means the host never marks the member
   InWorld, never fires the world-entry fan-out
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/SceneStateHandler.cs`,
   `_worldEntryFanout.Send(reporter);`) and the entity stream never starts
   (`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`,
   `Dropping player stream`); the guest's only escape is the 60 s give-up back to the
   menu (`src/CasualtiesUnknownOnline.GameAdapter/Run/StartGateCoordinator.cs`,
   `WorldReady never arrived within`).
3. **`WorldSnapshotComplete` is inert.** The marker is sent last in the ordered
   world-entry group
   (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs`,
   `_world.SendWorldSnapshotComplete(steamId);`) and the receiver fires
   `WorldSnapshotCompleteReceived`, but no production code subscribed to it, so a
   partial backfill was not distinguishable in behavior.

## Goal

A lost control/readiness message converges while the peer stays connected:
the host's member state, the guest's in-world state and the world-entry group
completion all reach a consistent state without requiring a leave/re-enter.

## Design direction (frozen before implementation)

1. **Re-ack unconfirmed members** — the host's existing 1 Hz warm-up pump
   (`SessionPeerMaintenance.SendPeerWarmup`) already targets un-handshaken lobby
   peers; also re-send `HandshakeAck` to peers that have a presence record but
   `Handshaken == false`. The guest already re-sends `HandshakeAckAck` on every
   received ack, so the loop closes in 1 s.
2. **Scene-state reconciliation** — a low-frequency (5–10 s) absolute scene-state
   report (or a host request) so a lost InWorld edge heals without the 60 s timeout.
3. **Marker decision** — wire `WorldSnapshotCompleteReceived` to the readiness
   gate / late-join completion, or delete the marker and the message id; do not
   leave an inert marker documented as a safety mechanism.

## What landed (2026-09-19)

**1. The guest's bounded absolute scene re-report window.**
`src/CasualtiesUnknownOnline.Runtime/Session/SessionControlConvergence.cs` (new, one
`ICuoService`) is the whole mechanism's clock and state. After each scene edge the guest
re-asserts its ABSOLUTE scene report — the same state, never a delta, through the new
`ISessionControl.ResendSceneState()` (SessionService stores the last report and re-sends it
verbatim) — at a 5 s cadence. The window is opened by the report EDGE itself, the new
`ISessionControl.LocalSceneReported` event raised before the message leaves: polling the
scene flag could miss an edge that came and went between two updates, and a peer that
answers inside the send call must not have its answer cleared by an arming that happens
after it. A repeat counts against the budget only when it actually left the client
(`ResendSceneState` returns that verdict), so a session that is not active cannot spend the
window on reports that never went out.

**2. The entry window's budget outlasts the host's own gate.**
`MaxReports = 12` (60 s, the guest's valve timescale) is not arbitrary: the start gate can
legitimately stay armed for its whole 30 s force-start, its release broadcast is itself a
one-shot, and the window is the only thing that can heal that broadcast — a budget shorter
than the gate's lifetime would stop re-asserting before the message it heals could even be
lost (review finding MAJOR-1; `GateArmedPastTheFirstWindows_StillConvergesWhenItReleases`
pins it). The window closes the moment BOTH facts are in: the entry-group completion marker
and the start-gate release. A member the host never answers is NAMED in a warning and the
re-reports stop — the same bounded-trickle rule the recipe-unlock fallback uses.

**3. The exit edge is re-asserted too, with the budget AS the policy.** A swallowed InMenu
report used to leave the host holding the member in world for the rest of the session (its
entity stream, its clone, its start-gate slot), because nothing re-asserted the exit. The
same window re-sends the absolute exit report for `MaxExitReports = 6` (30 s, the documented
swallow window) and then stops: no answer exists for that direction — nothing the host sends
acknowledges "I saw you leave" — so there is nothing to wait for and the bound is logged
rather than warned about (review finding MINOR-3). A member that leaves and re-enters gets a
fresh window per edge, and the entry direction is unaffected.

**4. The host answers a repeat report with exactly its two control facts, and never
re-runs the fan-out.** `SceneStateHandler` keeps its edge path untouched and adds the
repeat branch: a report from a member that is already InWorld and Handshaken is
answered with `IWorldControl.AnswerRepeatInWorld` (new; `WorldStartGate` re-sends the
targeted `WorldReady` when the gate is released, stays silent while it is armed, and
NEVER releases the gate — a repeat is not a new arrival) plus
`SendWorldSnapshotComplete` (the marker is truthful: `member.InWorld == true` means the
entry group went out on the edge). The entry fan-out itself is not repeated: that would
duplicate every absolute table and would stop the marker from meaning "the entry group
is complete".

**5. The W7 inert marker got its production consumer instead of a removal.** The
window's entry fact IS `WorldSnapshotCompleteReceived` — the marker is now the
guest-side acknowledgement that the host ran the entry fan-out for this member. The
matrix's W7 note and the row R3 text record the decision (consumer, not deletion).

**6. The handshake's third leg converges.** `HandshakeHandler` stores the ack it sent
on the member record (`MemberPresenceTable.MemberPresence.SentHandshakeAck`), and the
host's existing 1 Hz warm-up pump re-sends that ack — refreshing only the scene field,
the one control fact that moves; re-asserting a stale scene would drive the guest's
scene handlers — to every presence member that is not `Handshaken`
(`SessionPeerMaintenance.SendPeerWarmup`). The guest answers EVERY ack with the third
leg (`HandshakeAckHandler`), so the loop closes within ~1 s. The same handler became
edge-idempotent: a repeat ack no longer re-fires `MemberAdded` (the Mod API's
`PlayerJoined`, the item domain's watermark grant) or the scene event — the exact rule
the host-side `HandshakeAckAckHandler` already documented for its own repeats.

**7. The roster is an absolute table too.** `IEntitySyncControl.ResendRoster(steamId)`
re-sends one member's activation join with the SAME entity id (never a new allocation)
plus every other synced member's row, and `WorldEntryFanout.SendInSessionRepair` calls
it with the other absolute tables, so a swallowed `PlayerJoin` converges in-session
(≤ 60 s) instead of waiting for a leave/re-enter; a third party's row converges through
its own repair pass. Repeats are absorbed by identity: `EntitySyncService.ProcessPlayerJoin`
skips a join whose SteamId + EntityId it already holds (no `RemoteJoined` re-fire, no
stream-sequence reset) and a roster row whose id is unchanged is a no-op.

**8. A real split, demanded by the architecture gate.** `EntitySyncService` sat at 598
lines; the roster half of this change would have crossed the 600-line gate, so the
announcement writes moved into a new collaborator
`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerRosterAnnouncer.cs` (105
lines: `SendRosterTo`, `AnnounceJoin`, `AnnounceLeave`, the join/leave message shapes,
the announce fan-out and the leave row) — the entity table, id allocation and lifecycle
stay in the service (598 → 580 lines). The extraction is behaviour-preserving: the same
frames in the same order at `StartMemberSync` and `OnMemberRemoved`.

**9. No wire change.** Nothing in this cycle adds, removes or re-shapes a `NetMsg`, a
`WireCommandKind`, a `WireEventKind` or an `AdaptiveStreamId`; the protocol version
stays 31 and the delivered build's `+sha` identity is unchanged in kind. The whole
mechanism is convergence over messages that already exist.

**10. Tests.** `tests/CasualtiesUnknownOnline.Tests/Session/SessionControlConvergenceTests.cs`
(one new class, 9 cases, `[Trait("Category", "Integration")]`): the swallowed third leg
healing on the host's re-ack, the swallowed InWorld report healing inside the window
with the entry group firing exactly once, the swallowed `WorldReady` healing through
the repeat answer with no second release, the swallowed roster converging through the
repair group with no duplicate join, a lost marker keeping the window open and closing
it when it arrives, the spent budget stopping the re-reports, the gate staying armed past
the first windows and still converging when it releases (the branch no test covered before
the review), the swallowed world EXIT re-asserted and then bounded, and a third member's
wire surface standing still (BOTH guests in world, only one missing its release) while
another member's window is answered. The swallow is injected precisely:
`LinkFaults.DropMessageId`
(new knob in `tests/.../Fakes/FakeNetwork.cs`) swallows one message id on one link while
the link still reports the send as successful — the production swallow contract.

**11. Evidence and matrix.** Rows R3 and R4 moved from `Event-only gap` to `OK`: their
fallback/recovery/loss cells now carry the window, its corrected budget, the exit
direction, the repeat answer, the roster repair and the marker consumer, the gap list
records the closure, and the verdict summary moved (`OK` 51 → 53, `Event-only gap` 4 → 2).
The evidence file gained 30 anchors (896 → 926; R3 17 → 38, R4 16 → 25) and six stale
anchors were re-pointed to the code that now carries the quoted line (the roster writes and
the ack send moved with items 4-8; the window's const, its guard and its send call changed
with the review fixes).

## Acceptance matrix (result)

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | `HandshakeAckAck` dropped | Host re-acks on its 1 Hz pump; the member is Handshaken within ~1 s and the start gate's filter includes it | `HandshakeAckAckDropped_TheHostReAcksUntilTheMemberIsConfirmed` |
| 2 | `HandshakeAck` dropped | Unchanged: the guest's existing 1 Hz handshake retry heals it (regression) | `HandshakeTests.Handshake_FirstMessageSwallowed_RetriesAfterInterval` (untouched path) |
| 3 | `SceneState` (InWorld) dropped | Host converges without the 60 s timeout; the entry fan-out fires exactly once (the healed re-report is an edge, not a repeat) | `InWorldReportDropped_TheWindowHealsItAndTheEntryGroupFiresOnce` |
| 4 | `PlayerJoin` dropped | Host and guest converge through the in-session repair group carrying the same entity id; a repeat is absorbed (no second join) | `RosterDropped_ConvergesThroughTheRepairGroupWithoutADuplicateJoin` |
| 5 | `WorldReady` dropped | Guest converges on the next re-report; repeated answers never release the gate twice | `WorldReadyDropped_TheRepeatReportIsAnsweredWithTheGateState` |
| 6 | Third-party member | Unaffected: the repeat answer is targeted at its member and no duplicate fan-out reaches the other guest (both guests in world, so the zeros are not by construction) | `ThirdMember_ReceivesNothingFromAnotherMembersConvergence` |
| 7 | Reconnect while in world | Unchanged: the handshake path still fans out on `member.InWorld` (`HandshakeHandler`), which item 6 only added the stored ack to | `ReconnectWorldSnapshotTests` (existing family, still green) |
| 8 | `WorldSnapshotComplete` | Consumed: it is the readiness window's entry-group acknowledgement (no inert wire id) | `LostMarker_KeepsTheWindowOpenAndClosesItWhenItArrives` + `SpentWindow_StopsReReportingInsteadOfTrickling` |
| 9 | Scene exit (`InMenu`) dropped | Host stops holding the member in world within one window; the repeats then stop at their bound (no trickle, no answer to wait for) | `ExitReportDropped_IsReAssertedInBoundedWindows` |
| 10 | Gate armed longer than the first windows, then released with the release lost | Still converges inside the guest's 60 s valve instead of falling through to it — the window outlives the gate it heals | `GateArmedPastTheFirstWindows_StillConvergesWhenItReleases` |

## Verification

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~SessionControlConvergenceTests"`
  — 9/9.
- Gates: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/...` — 56/56
  (the evidence contract included: every anchor quote still matches its source line, the
  declared anchor counts match the file, the verdict summary matches the rows).
- Full: `dotnet test CasualtiesUnknownOnline.slnx` — 3388 passed, 0 failed on the
  implementation-complete tree (the pre-review tree was 3386 with 7 cases; the review
  fixes added the exit and gate-armed-late cases). The final post-checklist run is
  recorded in the handoff.
- Independent adversarial review: one FULL round plus a fix-verification round, both in a
  fresh context against the frozen tree, before the commit. The first round reproduced
  every number and falsified nothing in items 4-9; the second reproduced the fix numbers
  (focused 9/9, gates 56/56, full 3388) and confirmed each fix. Findings and same-round
  fixes below; the only residual it left was the terminal log wording, fixed here.

### Review findings and fixes (same commit)

- **MAJOR-1 — the entry window could stop before the gate it heals.** The gate can stay
  armed for its whole 30 s force-start and its release is a one-shot; a 6-report (30 s)
  budget could therefore expire before the release happened, so a lost release fell
  through to the 60 s valve — the failure the window exists to prevent. Fixed: the budget
  is `MaxReports = 12` (60 s, the valve's own timescale), and the previously unexercised
  "gate armed, so the host answers nothing" branch is now pinned by
  `GateArmedPastTheFirstWindows_StillConvergesWhenItReleases`.
- **MAJOR-2 — a report that never left could spend the window.** `ResendSceneState` now
  returns whether the report actually went out, and the window counts (and logs) only real
  sends; the arming edge is the new `LocalSceneReported` event, so the window is also
  armed before any synchronous answer can be cleared by it.
- **MINOR-1 — the row-4 claim read too much into its test.** Clarified: the roster repair
  reaches members the HOST KNOWS are in world; a member whose InWorld report was lost is
  covered by the scene window first, and the roster follows on the next repair. The test
  calls the exact adapter seam, and the 60 s cadence lives in `WorldEventSync.Update`
  (declared gap).
- **MINOR-2 — the third-party test was near-vacuous.** Rebuilt: both guests are in world,
  only one is missing its release, and the assertions now show the answered member growing
  while the other's surface stands still (no longer two zeros by construction).
- **MINOR-3 — the reverse edge was not re-asserted.** Fixed as item 3 (the bounded exit
  window) with the acceptance row 9.
- **NIT-1 — the stored ack was mutated in place.** The pump now sends a COPY of the stored
  admission facts with only the scene read live, so the record of what the host admitted is
  not a moving target (and the copy does not depend on the sender serializing
  synchronously).
- **NIT-2 — the frozen design heading read as an open decision.** Renamed.
- **Nit — the exit window's terminal log said the opposite of the truth.** It read "the
  host keeps the member out of world unless one arrives"; a swallowed exit is exactly what
  leaves the host holding the member IN world. Fixed in the same commit (round-2 review).
- **Note — the gate-armed-late test deliberately over-extends the production timeline.**
  It holds the gate armed ~36 s, while the adapter's `MaybeForceStartGate` fires at 30 s;
  the runtime suite cannot pump that adapter cycle, so the extra seconds stand in for the
  entry latency a live lobby adds on top of the force-start. The production worst case
  (30 s + entry latency) is still inside the 12-report budget, and the test discriminates:
  the old 6-report budget cannot satisfy its assertion (the release lands at ~36 s).
- **Note — the exit window's accepted loss, stated with its consequence.** If all six exit
  reports are swallowed, the host keeps the member InWorld for the rest of the session: it
  keeps a start-gate slot, an entity row and a frozen render clone for a player who is
  standing in the menu, and only the next entry edge (or the member's removal) clears it.
  Nothing acknowledges an exit, so no answering fact exists to wait for; the bound is the
  honest policy and the matrix row states it.

## Known coverage gaps (declared, not silently carried)

- The Game Adapter's half is not exercised: `RunCoordinator`'s local gate phase
  (`WaitingForReady`), the 60 s valve in `StartGateCoordinator`, and the real
  `WorldReady`/`WorldSnapshotComplete` presentation path. The suite drives the Runtime
  seams (`IWorldControl.StartStartGate`, `IWorldControl.WorldReadyReceived`, the fanout)
  because the adapter needs a live Unity world.
- The real lazy-P2P swallow window is simulated by a targeted drop, not by Steam's
  session establishment; the real 60 s in-session repair cadence lives in the adapter's
  `WorldEventSync.Update` (the suite calls `WorldEntryFanout.SendInSessionRepair`, the
  exact seam that cycle calls).
- The host's gate/adapter state cannot be driven from the runtime suite: the tests arm the
  gate through `IWorldControl` and release it by reporting the second member, so the
  "release broadcast is lost" case is covered while the adapter's own 30 s force-start is
  not (declared above).
- The EXIT window is a bounded repeat by design, not a converging one: if every one of its
  six reports is swallowed the host keeps the member in world until the next entry edge or
  the member's removal. Nothing acknowledges an exit, so there is nothing to wait for; the
  bound is the honest policy and it is logged.
- Dual-client acceptance (two real processes) is the user's release-cycle action: a
  mid-join guest must end up in world with its roster row and the start gate released,
  a guest that misses the entry report must converge without returning to the menu, and a
  guest that returns to the menu must stop being simulated by the host.

## Non-goals

- Host migration, reconnect semantics changes.
- The kernel checkpoint/journal recovery (already covered by rows K2/K3).
