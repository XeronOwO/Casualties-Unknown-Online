# Lobby-Domain Refactor — Plan (implemented 2026-08-15)

> **Historical record.** This refactor is implemented. Current session/lobby
> lifecycle behavior is covered by `docs/decisions/active.md` and the active
> architecture docs.

Goal: make the lobby identity a real state machine. Today a client that hosted
its own lobby and later joins another player's lobby stays `SessionRole.Host`
forever, so the new host's `HandshakeAck`/`WorldJoin` are dropped and the run
never follows. This round fixes that transition, the symmetrical transitions,
and the session teardown each transition must perform.

Scope: lobby switching from the **main menu**. Switching while the local player
is in a world or generating is explicitly refused with a visible reason
(recorded degradation). The existing solo-world -> F8-create-lobby flow stays
supported.

## 1. Evidence (current behavior)

- `SessionService.OnLobbyEntered` returns immediately when `Role == Host`
  (`src/CasualtiesUnknownOnline.Runtime/Session/SessionService.cs:284-298`).
- `EndSession` deliberately never clears `Role`
  (`SessionService.cs:425-445`), because the old model assumed one lobby
  identity for the process lifetime.
- `SteamService.JoinLobby` does NOT leave the current lobby before joining
  (`src/CasualtiesUnknownOnline.Runtime/Steam/SteamService.cs:144-147`);
  `CreateLobby` already leaves first (`SteamService.cs:128-141`).
- `RunCoordinator.TryStartWorldJoin` refuses to start while `_inWorld`
  (`src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs:197-202`),
  and `OnSessionEnded` only resets the phase (`RunCoordinator.cs:446`).
- Full-stack simulation (temporary test over `TestNode`/`FakeNetwork`,
  2026-08-15): a node hosting lobby 8001, then entering lobby 9001 owned by
  another SteamId, ended with
  `role=Host sessionActive=True hostSteamId=2001 hostSeesGuestHandshaken=False`.
- A guest switching lobby (old host 1001 -> new host 3001) handshook the new
  host but kept the old presence: `presence=1001:True,3001:True`.
- Baseline: `dotnet test` green, 661 tests, commit `987ea2d`, worktree clean.

## 2. Mechanism inventory

| # | Mechanism | Current behavior | Evidence | Verdict |
|---|---|---|---|---|
| M1 | Steam lobby leave-before-join | Create leaves; Join does not | `SteamService.cs:128-147`; Steam docs: `LeaveLobby` takes effect immediately (`partner.steamgames.com/doc/api/ISteamMatchmaking`) | Join must leave first |
| M2 | Lobby-left notification | No event crosses `ISteamService`; session only hears Create/Enter | `ISteamService.cs:25-27` | Add `LobbyLeft` event |
| M3 | Role transition | Role set once, Host sticks forever | `SessionService.cs:270-298` | Rewrite as lobby-following state machine |
| M4 | Session teardown | `EndSession` clears presence/active but keeps role; no per-member scene teardown | `SessionService.cs:425-445` | Teardown fires `RemoteSceneChanged(false)` per member + `SessionEnded` once |
| M5 | Handshake kick | Guest handshake only on first enter | `SessionService.cs:292-298` | Re-kick after every guest transition |
| M6 | Packet direction table | Reads `Role`; stuck Host drops new-host frames | `PacketReceiver.cs:47-76` | Fixed by M3; no table change |
| M7 | World start state | `HostRunPending`, start gate, `_gateReleased`, `WorldParams` never reset on session end | `WorldService.cs:50-71` | Reset on `SessionEnded` |
| M8 | World damage table + registries | Only reset at new generation (`ResetDamagedBlocks`) | `WorldService.cs:442-450` | Reset on `SessionEnded` |
| M9 | Item session state | World table, transfer table, id watermarks, modifier projection live forever | `ItemService.cs:25-33`, `ItemArbitration.cs:36`, `ItemIdCoordinator.cs:28`, `ItemSnapshotService.cs:35-47` | Reset on `SessionEnded` |
| M10 | Saved characters | Persist across sessions by design; only new-run clears | `CharacterDataStore.cs:34,94` | Clear on `SessionEnded` (host session survives guest leaves, so reconnect semantics are preserved) |
| M11 | Entity/enemy sync | Already reset on `SessionEnded` | `EntitySyncService.cs:359-364`, `EnemySyncService.cs:326-332` | Keep; verify event order |
| M12 | Adapter pending operations | `ResetPending` only on unbind/world-gen, not session end | `ItemWorldSync.cs:269`, `BlockBreakSync.cs:134`, `CraftingSync.cs:38` | Reset on `SessionEnded` |
| M13 | Clone/fact caches | `RemotePlayerRenderer` and `CloneFactTable` have no session-end clear | `RemotePlayerRenderer.cs:49-57`, `CloneFactTable.cs` | Clear on `SessionEnded` |
| M14 | WorldJoin follow while in world | `TryStartWorldJoin` returns while `_inWorld`; no session-end return-to-menu for a former host | `RunCoordinator.cs:197-202,446` | Former-host/guest return to menu on session end (defense in depth; normal switch is menu-only) |
| M15 | In-world/generation switch | Not defined | — | Explicit guard: refuse with reason (degradation recorded) |
| M16 | Lobby owner transfer / host migration | Still out of MVP | architecture.md §7 | No change; presence check semantics unchanged |

Unverified, to be resolved by runtime verification:
- Steam callback behavior when `LeaveLobby` + `JoinLobby` are issued back-to-back
  (the plan makes CUO independent of any auto-leave behavior by leaving
  explicitly, but the real-client callback order is runtime evidence).
- Duplicate `LobbyEnter_t` delivery for an already-current lobby (the state
  machine treats same-lobby re-entry as an idempotent re-handshake).

## 3. Design

### 3.1 Steam layer (`SteamService`, `ISteamService`, `LobbyLifecycle`)

- `ISteamService` gains `event Action<ulong>? LobbyLeft`.
- `SteamService.JoinLobby`: if a lobby is current, `LeaveLobby` it, call
  `_lobby.OnLobbyLeft()`, fire `LobbyLeft`, then request the join.
- `SteamService.CreateLobby`: same `LobbyLeft` fire on the existing
  leave-before-create path.
- `LobbyLifecycle.MustLeaveBeforeCreate` is renamed to a join/create-neutral
  name (`IsInLobby`) because the same verdict now serves both operations.
- Failed `LobbyEnter_t` remains a non-transition (already true at
  `SteamService.cs:165-183`); after the early leave the session layer is
  already in `None`, which is the correct degraded state.

### 3.2 Session state machine (`SessionService`)

New private field `_currentLobbyId` and transition helpers.

```
OnLobbyCreated(id):
    if same id already host-active -> no-op
    if different id -> TeardownSession(leaveLobby: true)
    currentLobbyId = id; Role = Host; HostSteamId = LocalSteamId; SessionActive = true

OnLobbyLeft(id):
    TeardownSession(leaveLobby: true)
    Role = None            # identity follows the actual lobby state

OnLobbyEntered(id):
    owner = _steam.GetLobbyOwner()
    if owner == LocalSteamId:
        if same id already host-active -> no-op     # creator's normal LobbyEnter_t
        ensure Host identity as above
    else:
        sameSession = currentLobbyId == id && Role == Guest
                      && HostSteamId == owner && SessionActive
        if !sameSession:
            TeardownSession(leaveLobby: true)
            currentLobbyId = id; Role = Guest; HostSteamId = owner
        KickHandshake()
```

`TeardownSession(leaveLobby)`:

1. `SessionActive = false` (stops all sends first).
2. For every presence member: `FireRemoteSceneChanged(member, false)` — this
   destroys render clones and is the existing "host left the world" pull for a
   guest (`RunCoordinator.cs:425-439`).
3. `_presence.Clear()`; `HostSteamId = 0`.
4. If a session actually existed (`SessionActive` or non-empty presence or
   non-zero `HostSteamId`): fire `SessionEnded` exactly once.
5. If `leaveLobby`: `_currentLobbyId = 0`.

`EndSession()` becomes `TeardownSession(leaveLobby: false)` and keeps its
idempotent no-op when there is nothing to tear down. `Role` is still not
cleared by `EndSession` (same-lobby rejoin semantics, existing tests); it is
cleared only by a real `LobbyLeft`.

### 3.3 Domain resets on `SessionEnded`

- `WorldService` subscribes `SessionEnded` (unsubscribes via `IDisposable`):
  clears `HostRunPending`, start-gate set/timer/`_gateReleased`, `WorldParams`,
  `_damagedBlocks`, trap-consumption/opened-entity/trap-layout registries.
- `ItemService` subscribes `SessionEnded` and resets:
  - world table (`ResetItems()`),
  - transfer table (`ItemArbitration.ResetForSessionEnd()` — clears regardless
    of current role),
  - id watermarks (`ItemIdCoordinator.ResetForSessionEnd()`),
  - layer-modifier projection (`ItemSnapshotService.ResetForSessionEnd()`).
- `CharacterDataStore` subscribes `SessionEnded` and clears `_savedCharacters`.
  Guest disconnect/reconnect is untouched: the host session survives a guest
  leaving, so the save table still bridges that reconnect.
- `GameAdapter` subscribes `SessionEnded` and resets:
  `_characterDataSync.ResetSessionState()` (pending restore + fact table),
  `_renderer.DestroyAllClones()`, `_itemWorldSync.ResetPending()`,
  `_blockBreakSync.ResetPending()`, `_craftingSync.ResetPending()`.
- `RunCoordinator.OnSessionEnded`: reset phase + join kind + countdown anchor +
  fingerprint flag, call `WorldParamsService.ResetForSessionEnd()`, and if the
  local player is in a world, `PlayerCamera.main.ToMainMenu()` (covers the
  former-host case; the existing `OnRemoteSceneChanged` covers a guest).
- `EntitySyncService`/`EnemySyncService` keep their existing `SessionEnded`
  resets (M11) — the only change is the order guarantee above.

### 3.4 Menu-only switch guard

- `IGameAdapter` gains `bool IsInWorldOrGenerating { get; }`;
  `GameAdapter` returns `_run.IsInWorldOrGenerating`
  (`_inWorld || HarmonyTraverse.IsGenerating()`).
- New pure class `LobbySwitchGuard` (Runtime/Session, one type per file):
  - `CanCreateLobby(role, sessionActive, worldFlowActive)`:
    false when world flow is active and there is any active session role;
    true for menu and for the solo-in-world -> host conversion.
  - `CanJoinLobby(worldFlowActive)`: false while a world/generation is active.
- `Plugin` applies the guard to F8 (create), F9 (join), Steam-friend
  `JoinRequested`, and the delayed `+connect_lobby` F8 path. Rejection sets
  `_lastJoinError` so the test HUD shows why.

### 3.5 Explicitly out of scope (recorded, not silent)

- Switching lobby while in a world or mid-generation (guard refuses; the
  session layer's teardown is still safe as defense in depth).
- Host migration (unchanged: old host leaves -> guests keep current behavior).
- Dedicated leave-lobby UI (not introduced).

## 4. Verification design

Automated (pre-deployment):

1. Session transition suite over `TestNode`/`FakeNetwork`:
   - None -> Host (existing).
   - None -> Guest (existing).
   - Host -> Guest: old presence cleared, `SessionEnded` fired once, role
     Guest, new host owner recorded, three-leg handshake completes.
   - Guest -> Guest (new lobby): old host presence gone, new handshake
     completes.
   - Guest -> Host (F8-style): old presence cleared, role Host, `SessionActive`
     true at lobby creation.
   - Host -> Host (recreate): presence cleared, fresh host session.
   - Same-lobby duplicate `LobbyEntered`: idempotent, no second teardown.
   - `EndSession` still keeps role; existing tests stay green.
2. Domain-reset tests:
   - `WorldService`: seed `WorldParams`, `HostRunPending`, a damage block and
     an armed gate; end session; assert all cleared.
   - `ItemService`: register a world item, a transferred item and a watermark;
     end session; assert tables/watermarks empty and modifier projection reset.
   - `CharacterDataStore`: save a character; end session; assert gone.
3. `LobbySwitchGuard` decision tests (menu/world/generation matrix).
4. Full gates: build, `dotnet format`, `check-architecture.ps1`,
   `check-event-replay.ps1` (no event matrix row is touched), `dotnet test`
   (661 existing + new tests).

Runtime acceptance (real game dir only, sandbox shadow check first):

1. Real host creates lobby A (F8).
2. Real guest FIRST creates its own lobby (F8), then joins A via Steam friends
   / F9. Expected logs: guest leaves old lobby, `Session role: Guest`, host
   member handshake confirmed; HUD shows GUEST.
3. Host starts a run. Expected: guest receives `WorldStartParams` + `WorldJoin`,
   follows into the same world, `WorldReady` releases both, fingerprint lines
   match on both sides.
4. Negative guard: while either side is in the world, attempt a lobby switch;
   expected warning + HUD error, no lobby change.
5. Old lobby residue check: the original host-side presence poll removes the
   switcher from the old lobby's roster (Steam `LobbyChatUpdate`); no CUO
   session remains on the switcher after `LobbyLeft`.

## 5. Self-check table (mechanism x change x evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Steam lobby leave/join | Join leaves current lobby before joining; both operations fire `LobbyLeft` | `SteamService.cs` diff; runtime step 2 logs "Leaving current lobby" -> "Entered lobby A" |
| Lobby-left notification | `LobbyLeft` on `ISteamService` + fake | compile + fake-driven session tests |
| Role transition | Lobby-following state machine in `SessionService` | unit tests Host->Guest / Guest->Host / Guest->Guest; simulation repro flipped from `role=Host` to GUEST+handshake |
| Session teardown | Per-member `RemoteSceneChanged(false)` + one `SessionEnded` | session tests assert event counts and empty presence |
| Handshake | Re-kick on every guest transition | full-stack fake tests `Handshaken` both directions |
| Packet directions | No table change; role now correct before frames arrive | full-stack fake test (HandshakeAck reaches guest) |
| World start state | Reset on `SessionEnded` | WorldService unit test |
| World damage/registries | Reset on `SessionEnded` | WorldService unit test |
| Item session state | World/transfer/watermark/modifier reset | ItemService unit test |
| Saved characters | Clear on `SessionEnded` | CharacterDataStore unit test |
| Entity/enemy sync | Existing reset preserved; order documented | existing tests + new transition tests |
| Adapter pending ops | `ResetPending` on `SessionEnded` | build + existing world/item simulations |
| Clone/fact caches | Clear on `SessionEnded` | build + runtime step 3 (no stale clone/fact logs) |
| WorldJoin follow | Former host becomes Guest; menu follows host run | runtime step 3 (both in world, matching fingerprint) |
| In-world switch | Guard refuses with reason | `LobbySwitchGuard` unit matrix + runtime step 4 |
| Protocol/wire | No wire change | `ProtocolVersion` unchanged; event-replay gate green |

## 6. Delivery sequence after approval

1. Implement Steam/Session/Runtime changes + fake updates.
2. Add/adjust unit and full-stack tests; green local suite.
3. Implement GameAdapter/Plugin guard + reset wiring.
4. `dotnet format` + build + architecture/event gates.
5. Deploy real game dir; sandbox-shadow timestamp check.
6. Runtime acceptance (section 4), evidence logged.
7. Structure review; update `docs/backlog/README.md`, `docs/decisions/active.md`,
   `docs/history/architecture-blueprint.md` (lobby lifecycle paragraph) in the same round.
8. Delivery checklist box-by-box (Edit tool, no bulk checks); final commit.

## 7. Implementation record (2026-08-15)

Implemented per sections 1-6, plus two root-cause fixes found by the runtime verification:

1. `SteamTransport.Poll` now processes each received message in try/catch/finally — one throwing
   handler (an enemy-snapshot materialization with a missing prefab) used to lose every later
   message in the same batch, including `WorldReady` (observed twice as the full 60 s gate timeout).
2. `EntitySyncService.Update` refreshes the local entity's SteamId when Steam initializes on a later
   F8 retry; the stale 0 made the self-activation `PlayerJoin` fall into the roster branch and the
   host's 20 Hz stream was dropped as "no member with that entity id".

Runtime acceptance evidence (real host + Sandboxie Steam1 guest, deploy `23:18`/`23:26`):

- Guest hosted its own lobby (`Session role: Host`), then joined the real host's lobby:
  `Leaving current lobby ... before joining ...`, `Left lobby ...`, `Session ended (role Host kept)`,
  `Session role: Guest (..., host 76561198281246659)`, `Handshake complete ...`.
- Host `StartRun` via HotRepl: guest `World join received — starting a run to follow`,
  `Applied host world params (16 bytes)`, identical `[WorldFingerprint]` on both sides,
  `PlayerJoin received: local ... host ...`, and `World ready — start playing.` No
  `WorldReady never arrived`, no dropped entity states, guest phase probe = `Playing`.
- In-world negative guard: F9 on the in-world guest logged
  `Lobby join refused: a world is running or generating.` and the role stayed `Guest`.
