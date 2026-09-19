# Enemy snapshot binding has no recovery path

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / enemies (host-authoritative binding)
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N1, verdict `Event-only gap`); split from the former `enemy-snapshot-and-attack-recovery` umbrella — the attack half stays open in `review/enemy-hit-determination-local.md`
- Related: `review/runtime-entity-markerless-bind-absorption.md` (the runtime-spawn creation key), `review/runtime-entity-spawn-backfill.md`, `review/trap-layout-snapshot-recovery.md` (the same repair-set family, W6)

## Problem (evidence)

`EnemySnapshot` (NetMsg 81) was a world-entry / reconnect one-shot. The entry group sent it
(`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs` `Send`) and the
reconnect-while-InWorld path re-sent the same group
(`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeHandler.cs`: "the world snapshots
fan out here too"), and nothing else ever did — the in-session repair group was a deliberate
subset, so a member that stayed in the world and missed the entry send kept a stale or empty enemy
binding until its next reconnect.

Two facts made the obvious repair (just re-send the snapshot) not work:

- The guest's generated baseline is bound **by position**:
  `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs`
  `OnEnemySnapshotReceived` ordered the host's states and the guest's copies by `(x, y)`;
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySpawnArbitration.cs` `TryPair`
  enforces `PairTolerance` and is all-or-nothing ("the caller must treat that as a generation
  divergence (warn + degrade), not pair anyway").
- The host published the **current** position (`Capture` reads `entity.transform.position` every
  frame), while the guest's copies are frozen at their spawn spots
  (`FreezeOnGenerationComplete`: "The pairing key is the spawn position, so the copies must still
  be at their spawn spots when the host's snapshot arrives"). So the key was only valid in the
  instant after generation; a repair landing later failed the tolerance for the whole set — and
  because `OnEnemySnapshotReceived` assigns `_mappingEstablished = generatedPaired`, a failed
  pairing also switched OFF the runtime-spawn positional bind
  (`TryBindRuntimeSpawns`: "the generation baseline is not safely paired yet — never guess among
  generated enemies"). The same time-varying key is why a late joiner into a world whose animals
  had wandered could not pair at all.

## Landed (2026-09-18)

Root cause: the pairing key's scope ("the instant after generation") did not match the question a
repair asks ("does this host id correspond to this local generated copy, at any time?"). The key
was fixed, not the cadence.

- **The host publishes a stable binding anchor.** `EnemySyncCoordinator.Bind` now records each
  enemy's position ONCE, at the first bind — `EnsureMapping` runs on the host's first capture after
  generation and a runtime spawn is bound when it appears, so this is the enemy's generation /
  creation position. It travels beside the live position: `EnemyEntity.SpawnPosition` →
  `EnemyStateMsg.SpawnPosition` (`ProtoMember(10)`), never overwriting presentation; a capture that
  somehow lacks an anchor degrades to the live position with a warning rather than indexing blind.
- **The guest pairs on the anchor.** `OnEnemySnapshotReceived` orders the generated baseline by
  `.OrderBy(s => s.SpawnPosition, comparer)`; the live position stays presentation-only. Entry and
  repair snapshots now pair identically, and the 0.5 tolerance absorbs only first-frame settling
  jitter instead of the host's whole wander since generation.
- **The snapshot rides the in-session repair group.** `WorldEntryFanout.SendInSessionRepair` gained
  `_enemies.SendEnemySnapshot(steamId)` — the seam its own doc names ("Adding an absolute in-world
  table means adding it HERE as well") — and its doc was rewritten to say why the enemy snapshot can
  ride there while the item snapshot still cannot. The cadence owner is unchanged: the host's 60 s
  cycle (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs`
  `_worldBackfill.SendInSessionRepair(member.SteamId)` per in-world member, whose enumeration
  comment now names the enemy snapshot too). The empty-table no-op stays ("an empty table is a no-op
  — the member's own generated enemies stay"), and the send is logged with its counts so the repair
  is visible in a post-mortem instead of being invisible.
- **The anchor survives the 20 Hz stream.** `EnemySyncService.Merge` rebuilds a buffered entry from
  the stream payload, which carries no anchor, so it now preserves the snapshot's anchor. This was
  not a hypothesis: the first green run of the anchor test failed exactly here (expected the anchor
  10, got 0) — a stream frame between the snapshot and any later read silently zeroed the pairing
  key.
- **Wire / protocol: 22 → 23 (deliberate).** A new `ProtoMember` plus new cross-peer behavior: a v22
  peer would pair on the live position and would never be re-sent a snapshot it missed, so
  `ProtocolVersion.Current` was 23 when this landed and the entry records why.
- **The demanded architecture split.** `EnemySyncCoordinator` sat at exactly the 600 aggregate-line
  gate and this change pushed it to 626. Per
  `docs/backlog/watchlist/architecture-watchlist.md` ("must be split — as a real responsibility
  split — before the next change lands in them") the presentation-apply half moved into
  `EnemyPresentationApplier` — 94 lines, 42 of them the moved code: writing one authoritative state
  onto its bound copy (transform, reconciled health, stun pose, spider leg IK targets, crystal
  wind-up telegraph) plus the in-flight local-damage table it reconciles against. The coordinator
  now owns identity/binding/lifecycle only: 584 aggregate (381 + 203). Every line count in this
  ticket is the gate's own `File.ReadAllLines` convention, because that is what the 600 limit counts.
  The watchlist records both this split and what is left to extract next (the host capture half).

## Verification

**Red first (this is an audit-recorded defect, so the regression test was observed failing on the
pre-change tree).** `EnemySnapshotRecoveryTests.EnemiesPublishedAfterTheEntryEdge_StillReachAStayingMember`
— a member that enters while the host's enemy table is still empty and then stays in the world —
failed with "the in-session repair must deliver the enemy snapshot, got 0" before the change and
passes after. The two guards written in the same step (`EmptyEnemyTable_TheRepairSendsNothing`, the
pairing premise) passed on both sides, which is what makes them guards rather than decoration.

**Tests.** `EnemySnapshotRecoveryTests` (6 cases) covers the repair delivery, the empty-table no-op,
the anchor travelling beside the live position, the repeat apply's idempotence, the anchor surviving
stream frames, and the pairing premise (the live position stops matching once the enemy walks away,
the anchor does not). `EnemyStateRoundtripTests.EnemyState_FullRoundtrip` was extended with the new
field, so a future field added to `ApplyTo` without a roundtrip assertion still fails there.
Existing coverage reused, at the level it actually pins: `EnemySyncServiceTests` (the world-entry
snapshot reaches a member; an explicitly removed id is not restored by a snapshot — the adapter's
destroy half of that lifecycle is Unity-side), `EnemySpawnArbitrationTests` (the all-or-nothing
pairing helper, not the adapter's ordering call site).

**Independent adversarial review (fresh context, FULL tier, against this frozen tree): NOT BLOCKED.**
No blocker-class finding, and the central claim survived attack. The reviewer reproduced the gate run
(56/56), the full suite, the focused classes (50), the evidence counts (JSON 841 → 845, N1 anchors
12 → 16 with exactly four adds and no removals), the split arithmetic (HEAD 600 exactly, now 584)
and — after being handed the gitignored reference assemblies — **executed the red** on a
detached-HEAD worktree: the regression case failed with the exact message this ticket quotes
(1 failed / 5 passed). It also actively probed and could not falsify the anchor's survival across
projections/session boundaries, the extraction's behaviour preservation, or the host-side ordering.
Its accepted findings are fixed in this commit: the guarded anchor lookup in `Capture`, the counting
convention, "7 rows" plus a named home for row 7, the acceptance-row wording, the interface/feature
docs, and the limitations above. One finding was declined with reason: its interim "dangling
reference" report came from a filesystem walk of the gitignored `reversing/*` scratch tree (zero
tracked files reference the removed ticket) — the same reviewer retracted it.

**Gates and suite.** Normative gates 56/56. Whole solution: 3260 + 56 tests green in a building run
(`dotnet test CasualtiesUnknownOnline.slnx`, never `--no-build`), 0 failures — the 56 includes the
delivery-checklist gate, which passes because the checklist's required boxes are checked; an
independent adversarial review reproduced both figures (and the checklist-filtered decomposition
3260 + 55) from this tree. `dotnet format` clean; build 0 warnings / 0 errors.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row N1 rewritten for the landed snapshot half
(verdict stays `Event-only gap` while the attack half is open; the gap cell now points at
`review/enemy-hit-determination-local.md`), 12 → 16 anchors; `docs/evidence/sync-coverage-evidence.json`
841 → 845 entries with its `count` field updated.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `EnemiesPublishedAfterTheEntryEdge_StillReachAStayingMember` (the entry edge no-ops on an empty table, no further InWorld edge comes, so the repair is provably the carrier) |
| 2 | `EmptyEnemyTable_TheRepairSendsNothing` + the documented no-op in `SendEnemySnapshot` |
| 3 | `EnemySyncServiceTests.WorldEntry_ReceivesFullEnemySnapshot` (late joiner: the entry group carries ids + `RuntimeSpawns`; `MaterializeRuntimeSpawns` is identity-keyed and the `CreateRuntimeSpawn` guard returns early for an already-bound id) |
| 4 | `HandshakeHandler`'s reconnect-while-InWorld re-fans the same group (unchanged behavior; the repair group now also covers a member that already had one) |
| 5 | `EnemyRuntimeSpawnArbitrationTests` / the W-era runtime-spawn tests (the snapshot's `RuntimeSpawns` half is untouched by this change) |
| 6 | `EnemySyncServiceTests` — an explicit removal is final, "a snapshot also cannot resurrect an id that already received an explicit removal in this session"; the repair snapshot is the same apply path (`Replace`) |
| 7 | NOT covered here by design: the row moved with the attack half, which carries it as its own row 7 (`review/enemy-hit-determination-local.md`) |
| 8 | Acceptance-only for the view itself: the host sends per member (`SendEnemySnapshot(steamId)`, and the repair pump iterates the in-world members), so no peer is starved by another's repair, and `EnemySyncServiceTests` pins the shared id set on the wire. Peers agreeing on the set AND on terminal health across two live clients is observable only in the dual-client acceptance pass (see limitations) |

## Known limitations (recorded, not hidden)

- **The adapter half is not exercisable in the test host.** The anchor recording inside `Bind`, the
  anchor read in `Capture`, the guest's anchor-ordered pairing, `Freeze`, the runtime-spawn
  materialization and every `transform`/`GetComponent` call are Unity-side, so they rest on code
  review plus the unified dual-client acceptance pass — the same boundary W1/W2 and the rest of the
  enemy domain record. The suite pins the Runtime/wire contract (what the snapshot carries and how
  the guest buffers it), not the scene behavior: the anchor *pairing order* itself
  (`.OrderBy(s => s.SpawnPosition, comparer)`) is asserted nowhere — the tests pin the arbitration
  helper and the buffered anchor, not the adapter call site.
- **The 60 s PUMP itself is untested.** The timer, the `member.InWorld` filter and the per-member
  loop live in the Unity-side `WorldEventSync.Update`, and every repair test (this one and W6's
  trap-layout test) calls `SendInSessionRepair` directly. What is proven by test is the SET's
  membership, not the pump; reachability rests on the pre-existing call site that already pumps the
  other absolute tables. If that seam ever needs a test, the extractable pure part is the pump's
  decision ("now > last + 60 s" plus the in-world filter).
- **The anchor is only as good as the host's first capture.** It is the position at the first bind;
  if the host's frame stalled so long that an animal moved before its first capture, the anchor is
  off by that movement (bounded by the 0.5 tolerance). The entry path had the same exposure, so this
  is not a regression — it is the assumption made explicit.
- **Convergence is bounded by the repair cycle (up to 60 s).** A member that misses the entry send
  keeps unbound (frozen) copies until the next cycle. The alternative — a guest-driven rebind
  request answered immediately — would need a new message and handler for a latency gain no other
  absolute in-world table has; the audit's accepted bound for this family is the 60 s cycle.
- **Attacks dropped by a missing binding are not re-issued here.** While the binding is absent every
  ordered attack is dropped on the victim, and the host has already consumed the enemy's attack
  (retreat/cooldown). This change bounds that window to the repair cycle instead of forever; the
  order itself is `review/enemy-hit-determination-local.md`.
- **The positional key's scope is only fixed for the generated baseline.** A runtime spawn is still
  bound by `TryBindRuntimeSpawns` with the same 0.5 tolerance against CURRENT positions, and its
  stored anchor is "position at bind" — if the host binds that animal tens of milliseconds after its
  creation, the anchor is that later frame, not the creation spot. It is sound today because both
  sides' copies exist from the creation moment and the keyed cases are covered by
  `review/runtime-entity-markerless-bind-absorption.md`; the generated baseline's anchor is the only
  key this change moved.
- **An empty host table stays a no-op**, so a member is never "repaired" into an empty set (there is
  nothing to bind, and its own generated enemies stay) — the entry and repair paths agree.
