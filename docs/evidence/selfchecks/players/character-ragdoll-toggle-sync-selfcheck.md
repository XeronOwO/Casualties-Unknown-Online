# Character ragdoll-toggle sync self-check

Owner cycle: backlog "Ragdoll-toggle presentation sync". Decision: add a
dedicated reliable `CharacterRagdoll` message (NetMsg 120, ProtocolVersion 50)
reported from the game's own ragdoll-key input branch. The 20 Hz
`EntityStateMsg.Standing` flag remains the fallback; the event makes the
discrete collapse visible immediately on the owner's render clone.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native ragdoll-key input | `PlayerCamera.cs:958-961` — while the ragdoll key is held, `Body.Ragdoll()` is called; `Body.cs:1713-1730` flips `standing` to false and enables limb physics. |
| 2 | Existing lying pose path | `SessionStatePump.cs:149-162` + `LyingPose.cs` — the clone already plays `ExperimentLayDown`/`ArmsLayDown` when the stream reports `Standing=false` (not sleeping/alive). The gap is the wait/loss on the unreliable 20 Hz stream. |
| 3 | Dedicated one-shot event pattern | `CharacterAttackAnimMsg` / `CharacterLandingVisualMsg` — same star semantics: guest → host report, host applies + relays, guest replays inside `RemoteApply`. |
| 4 | External ragdoll sources | Traps, enemy attacks, cross-player push, timed medicine have their own event/state chains; this event is intentionally scoped to the manual key input branch only. |

## 2. Design

- `PlayerCameraHandleInputPatch` records the local body's `standing` flag in
  Prefix and observes the standing → collapsed transition in Postfix; it
  reports `OnCharacterRagdoll`.
- `CharacterRagdollSync.Report` sends one reliable `CharacterRagdollMsg`
  (`OwnerSteamId`, position) through the existing character-data channel.
- `CharacterRagdollSync.OnReceived` replays the native lay-down clip pair on
  the owner's clone, forces the clone's `standing=false`, and seeds
  `RemoteBodyDriver.PrevLying=true` so the next state-stream snapshot does not
  replay the same transition.
- Receiver replay runs inside `CallContext.RemoteApply`; no state is held
  across calls; a lost message is acceptable (the 20 Hz stream fallback).

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Wire | new `CharacterRagdoll` bidirectional message roundtrips | `CharacterRagdollSyncTests.CharacterRagdoll_RoundTripsEveryField` |
| Star relay | guest report reaches host and relays to the other guest | `CharacterRagdollSyncTests.GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest` |
| Star relay | host's own collapse broadcasts to every guest | `CharacterRagdollSyncTests.HostOwnCollapse_BroadcastsToBothGuests` |
| Star relay | relayed event fires on the other guest | `CharacterRagdollSyncTests.GuestRelay_FiresTheEventOnTheOtherGuest` |
| Direction registry | new message classified as bidirectional | `DirectionTests` |
| Replay | clone plays lay-down clips and seeds `PrevLying` inside `RemoteApply` | `CharacterRagdollSync.OnReceived` (static evidence) |
| Input scoping | only the game's ragdoll-key branch reports, not traps/pushes | `PlayerCameraHandleInputPatch` observes only `HandleInput`'s standing transition |

## 4. Verification

- **L0 unit**: `CharacterRagdollSyncTests` +4; `DirectionTests`/registry tests
  pass (100 targeted).
- **Code gates**: `dotnet build` 0 warnings/0 errors; full suite green;
  `dotnet format`; check-architecture / check-event-replay /
  check-entity-event-dispatch.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
