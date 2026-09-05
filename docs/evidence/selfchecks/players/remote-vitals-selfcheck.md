# Remote vitals display — mechanism inventory and self-check

Owner cycle: backlog "Direct player interaction (view/take items, carry, view
vitals, heal)". Decision for this cycle: close the **view vitals** slice by
showing the remote players' compact vitals in the existing Online UI — the
member status list and the in-world nameplates. The remaining direct
interactions (view/take items from another player, carry, heal) stay open.

No protocol change: the 1 Hz character-data stream already carries the full
`CharacterHealthMsg` to every side (guest reports → host save/relay; host
broadcast → guests; cross-guest relay with `OwnerSteamId`), so the UI only
needs a read-only projection of data that is already arriving.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Character snapshots reach every side | `CharacterDataMsg.Health` (`src/.../Protocol/Messages/CharacterDataMsg.cs`); host saves guest reports, fires `CharacterDataReceived`, then relays them to the other guests (`CharacterDataHandler.cs:17-35`, `CharacterDataStore.RelayCharacterData`); the host's own snapshot arrives via `HostCharacterData` (`HostCharacterDataHandler`) |
| 2 | The health fields are already wire-complete | `CharacterHealthMsg` carries `BrainHealth`, `Hunger`, `Thirst`, `Stamina`, `Energy`, `Temperature`, `Alive`, `Conscious` — the same body save surface (`Body.cs:3942-3954`) |
| 3 | Existing Online UI has the remote-player projection point | `OnlineUiOverlay.DrawNameplate` / `DrawMemberStatus` already render every in-world remote player; only the vitals line is missing |
| 4 | UI must not reach GameAdapter/Unity internals | The new cache lives in the Runtime and is fed by the public character-data events; the Plugin only calls `TryGet` and draws a string |
| 5 | Session scope must not leak between runs | `SessionService.SessionEnded` and `RemoteSceneChanged` are already broadcast; the cache clears on both |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CharacterDataStore` | Unchanged — it remains the owner of saved/relayed character data |
| `CharacterDataHandler` | Unchanged — the existing report/relay/restore flow already delivers health data |
| `RemoteVitalsSnapshot` | New pure projection: `From(CharacterHealthMsg?)`, `ToShortString()` |
| `RemoteVitalsService` | New read-only cache, subscribed to `CharacterDataReceived`, `HostCharacterDataReceived`, `RemoteSceneChanged`, `SessionEnded` |
| `OnlineUiOverlay` | Draws the compact line in the member status list and the in-world nameplate |
| `Plugin` | Resolves `RemoteVitalsService` and passes it to the overlay |
| Protocol / patches | Unchanged — no wire bump, no Harmony patch surface |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host sees guest vitals | Cache `CharacterDataReceived(sender, data)` on host as sender | `RemoteVitalsService.OnCharacterDataReceived`; covered by `Host_CachesGuestReportBySender` |
| Guest sees host vitals | Cache `HostCharacterDataReceived(data)` as `HostSteamId` | `RemoteVitalsService.OnHostCharacterDataReceived`; covered by `Guest_CachesHostBroadcastByHostSteamId` |
| Guest sees another guest's vitals | Use `OwnerSteamId` from the host relay, never the transport sender | `RemoteVitalsService.OnCharacterDataReceived`; covered by `Guest_CachesCrossGuestRelayByOwnerSteamId` |
| Local restore is not shown as a remote | A guest-side `CharacterData` with `OwnerSteamId == 0` is ignored | `Guest_IgnoresOwnRestoreOwnerZero` |
| No stale vitals after leaving world | `RemoteSceneChanged(false)` removes the player's entry | `RemoteLeavingWorld_ClearsThatPlayersVitals` |
| No cross-session leak | `SessionEnded` clears the cache | `SessionEnd_ClearsTheCache` |
| Compact formatting | Pure `ToShortString()` rounds to integers for the 10 px nameplate | `Snapshot_ProjectsOnlyNonNullHealth` |

## 4. Verification design

- **L0 service tests:** `RemoteVitalsServiceTests` (7 tests) — host report,
  host broadcast, cross-guest relay, own-restore exclusion, world-leave clear,
  session-end clear, pure projection/formatting.
- **Full regression:** `dotnet test CasualtiesUnknownOnline.slnx` — the
  character-data flow, Online UI geometry and the existing 20 Hz/1 Hz domains
  stay untouched.
- **Static evidence:** the character-data stream already delivers
  `CharacterHealthMsg`; the UI only projects received data.
- **Runtime evidence:** development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Verification results (2026-08-21)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1010 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "<game-dir>"` | 26 files deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 6. Structure review

- `RemoteVitalsSnapshot` 66 lines, `RemoteVitalsService` ~110 lines, both under
  the 600-line gate.
- One top-level type per file; no new expression-state bools; the cache is
  state owned by `RemoteVitalsService` with a read-only `TryGet` surface.
- Dead mechanisms: none. The existing character-data events are the single
  source; the UI projection is a new consumer, not a duplicate path.
