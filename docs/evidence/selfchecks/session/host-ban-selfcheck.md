# Host ban — second admin slice — self-check (2026-08-23)

Backlog §2.7 listed admin/kick/ban/vote as a lower-priority KrokMP candidate.
The host-kick slice has already landed; this cycle closes the **ban** slice: the
host can permanently reject a guest SteamID, send it a dedicated wire message,
and persist the ban so future handshakes are refused before the member enters
the roster.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Session control plane | `SessionService` owns the presence table and the host member-removal path; `ISessionControl.RemoveGuestMember` is the same surface the kick slice reuses. |
| Wire registry | Every new message must be explicitly classified in `DirectionTests` (NetMsg direction fail-closed). |
| Host UI | `OnlineUiOverlay` member list already renders host-only `Kick`; a host-only `Ban` button is the natural adjacent admin action. |
| Guest teardown | `ISessionControl.EndSession()` is the existing no-host-migration teardown, identical to the kick path. |
| Persistence | The host already owns file-backed stores (`CharacterDataFileStore`, `ModStateFileStore`); the ban list follows the same atomic file pattern. |

## 2. Changes

- `BannedMsg` (NetMsg 112, ProtocolVersion 40) — host → guest: the host
  permanently banned this member. Carries a short human-readable `Reason`.
- `BannedHandler` — guest-side handler: logs the reason and calls
  `ISessionControl.EndSession()`.
- `HostBanFile` + `HostBanFileStore` — versioned protobuf disk store under
  `BepInEx/config/CasualtiesUnknownOnline.host-bans.bin`; missing/corrupt/
  unknown-version degrades to an empty list; writes are atomic and leave no
  `.tmp` residue.
- `HostBanService` / `IHostBanService` — host-only ban collaborator: rejects
  non-host calls, self-bans, unknown members and already-banned SteamIDs;
  persists the ban before sending the dedicated `Banned` message and removing
  the member through the existing `ISessionControl.RemoveGuestMember` path.
  `Unban` is also exposed so a host can reverse a mistake through the same
  service/API.
- `HandshakeHandler` — checks `IHostBanService.IsBanned(sender)` before mod
  consistency / member creation; a banned SteamID never enters the roster,
  including reconnect attempts.
- `OnlineUiOverlay` — host-only `Ban` button next to `Kick`; the existing
  `Recruit` button was nudged to `x=700` so the new button does not overlap it.
- `Plugin` — wires the UI Ban button through `IHostBanService.Ban` and passes
  the host ban file path into the composition root.
- `CuoBootstrap` / `TestNode` — register the file store and service, with the
  test composition using an in-memory (null-path) store unless a test passes a
  real path.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1278 passed / 0 failed (full suite) |
| `dotnet format ... --verify-no-changes` | source files clean; the only reported file is the generated `obj/Debug/net48/MyPluginInfo.cs`, which is not part of the repository |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `DirectionTests.EveryNetMsg_IsExplicitlyClassified` | pass after adding `Banned` to the host→guest classification |
| Protocol | ProtocolVersion 40 |

## 4. L0 proof

- `HostBanTests.HostBan_SendsDedicatedMessageAndRemovesMember` — the host ban
  sends `Banned` with the reason, removes the guest from host presence, the
  guest's `SessionActive` becomes false, and the ban list contains the SteamID.
- `HostBanTests.HostBan_OnlyHostCanBanNonLocalKnownMember` — a guest cannot
  ban, a host cannot ban itself, an unknown id is rejected, and a second ban of
  an already-banned SteamID is a no-op.
- `HostBanTests.HostBan_RejectsRejoinAndUnbanAllowsRejoin` — a banned player
  re-entering the lobby is not added to host presence; after `Unban` the same
  rejoin flow is accepted.
- `HostBanTests.HostBan_PersistsAcrossHostNodeRestart` — a fresh host node
  constructed with the same ban file loads the persisted SteamID.
- `HostBanFileStoreTests` (4) cover round-trip/no-temp, missing-file empty,
  corrupt-file degradation, and disabled-store no-op.
- No manual dual-side acceptance (user rule 2026-08-16).

## 5. Structure review

- New state has an owner: the persisted ban list lives in `HostBanService` and
  the disk store; it is not a DI-visible mutable singleton state store.
- `SessionService` is untouched except by no new code in this slice — the ban
  path is a separate top-level collaborator, preserving the 600-line gate.
- No dead mechanism left behind: `HostKickService` remains the one-shot kick
  path, `HostBanService` is the persistent ban path, and both share the existing
  member-removal surface.
- The new wire message is fail-closed classified in `DirectionTests`; no
  manually maintained direction switch remains.
