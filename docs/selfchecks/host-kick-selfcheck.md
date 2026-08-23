# Host kick — first admin slice — self-check (2026-08-23)

Backlog §2.7 listed admin/kick/ban/vote as a lower-priority KrokMP candidate.
This cycle closes the **kick** slice: the host can remove a guest from the
session with a dedicated wire message, the guest tears its session down
immediately, and the remaining members are updated through the existing member
removal path. As a small adjacent polish item, the Online UI member list now
also shows each member's own RTT instead of only the global last-RTT line.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Session control plane | `SessionService` owns the presence table and the host member-removal path. |
| Wire registry | Every new message must be explicitly classified in `DirectionTests` (NetMsg direction fail-closed). |
| Host UI | `OnlineUiOverlay` member list already renders host/guest, handshake, in-world, vitals, inventory and per-member action buttons. |
| Guest teardown | `ISessionControl.EndSession()` is the existing no-host-migration teardown. |

## 2. Changes

- `KickedMsg` (NetMsg 111, ProtocolVersion 39) — host → guest: the host removed
  this member from the session. Carries a short human-readable `Reason`.
- `KickedHandler` — guest-side handler: logs the reason and calls
  `ISessionControl.EndSession()`.
- `HostKickService` — host-only kick collaborator: rejects non-host calls,
  self-kicks and unknown members; sends the dedicated `Kicked` message to the
  target first, then uses the existing `ISessionControl.RemoveGuestMember`
  member-removal path so the entity domain broadcasts `PlayerLeave` to the
  remaining members.
- `SessionService.KickMember(ulong steamId, string reason)` — thin facade over
  `HostKickService`; extracted as a separate top-level type so `SessionService`
  stays under the 600-line architecture gate.
- `OnlineUiOverlay` — host-only `Kick` button for each non-local lobby member;
  member status line now appends that member's `RttMs`.
- `Plugin` — wires the UI Kick button through `SessionService.KickMember`.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1269 passed / 0 failed (full suite) |
| `dotnet format ... --verify-no-changes` | our source files clean (the only reported file is the generated `obj/Debug/net48/MyPluginInfo.cs`, which is not part of the repository) |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `DirectionTests.EveryNetMsg_IsExplicitlyClassified` | pass after adding `Kicked` to the host→guest classification |
| Protocol | ProtocolVersion 39 |

## 4. L0 proof

- `KickedTests.HostKick_SendsDedicatedMessageAndRemovesMember` — the host kick
  sends `Kicked` with the reason, removes the guest from host presence, and the
  guest's `SessionActive` becomes false.
- `KickedTests.HostKick_OnlyHostCanKickNonLocalKnownMember` — a guest cannot
  kick, a host cannot kick itself, an unknown id is rejected, and the real
  host→guest kick still succeeds after those rejections.
- No manual dual-side acceptance (user rule 2026-08-16).

## 5. Structure review

- No new tracked state without an owner: the kick is a one-shot host action over
  the existing presence table; no new mutable state, no new DI service, no new
  controller object.
- `SessionService` remains under the line-count gate (the new method is small).
- No dead mechanism left behind: the existing `RemoveMember` path is reused,
  not duplicated.
