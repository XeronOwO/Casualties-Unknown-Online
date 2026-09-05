# Remote medical panel — mechanism inventory and self-check

> **Status: Rejected in review (2026-09-05).** The CUO-side IMGUI panel was not
> accepted because it did not reuse the game's existing medical UI. This file is
> retained as historical evidence; any future implementation must follow the
> native-UI reuse direction in
> `docs/backlog/todo/remote-player-medical-panel.md`.

Owner cycle: backlog `remote-player-medical-panel.md`. Decision for this cycle:
build the CUO-side read-only medical panel, fed by the already-synced 1 Hz
character-data stream, rather than attempting to focus the game's own medical
UI on a remote clone. The panel is display-only: it never mutates remote clone
proxies and never sends an authority-changing message.

No protocol change: `CharacterHealthMsg` and `CharacterLimbMsg` already travel
inside `CharacterDataMsg` on the same snapshot path used by remote vitals and
remote inventory.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Character snapshots reach every side | `CharacterDataMsg.Health` / `CharacterDataMsg.Limbs`; host saves guest reports and the same store relays/restores them to peers |
| 2 | Existing vitals cache is a consumer of those events | `RemoteVitalsService` subscribes to `CharacterDataReceived`, `HostCharacterDataReceived`, `RemoteSceneChanged`, `SessionEnded` |
| 3 | The previous cache only kept a compact projection | `RemoteVitalsSnapshot` contains only HP/hunger/thirst/stamina/energy/temperature/consciousness; the full health/limb wire data was discarded |
| 4 | The Online UI already has a member list and context-menu action surface | `OnlineUiMemberListDrawer`, `OnlineUiPlayerContextMenu`, `OnlineUiQuickPanel` all render from `OnlineUiMemberRow` projection |
| 5 | UI must not reach GameAdapter/Unity internals | The new `RemoteMedicalSnapshot` and `RemoteLimbSnapshot` are Runtime-only immutable projections; the Plugin panel only calls `RemoteVitalsService.TryGetMedical` |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CharacterDataStore` / handlers / protocol | Unchanged — the existing report/relay/restore flow already delivers full health and limb data |
| `RemoteVitalsService` | Now stores one per-player cache entry containing both the compact vitals and the full medical projection; `TryGetMedical` added |
| `RemoteMedicalSnapshot` | New immutable full-health projection (all `CharacterHealthMsg` fields) |
| `RemoteLimbSnapshot` | New immutable per-limb projection |
| `OnlineUiMemberRow` / `OnlineUiMemberProjection` | New `CanViewMedical` flag for non-local in-world players with cached vitals |
| `OnlineUiMemberListDrawer` | "Medical" button on member cards |
| `OnlineUiPlayerContextMenu` | "Medical" action, available even without line-of-sight because it is display-only |
| `OnlineUiMedicalPanel` | New read-only IMGUI panel with general/nutrition/circulation/trauma/status/limb sections |
| `OnlineUiOverlay` | Draws the panel, adds pointer-over/scoped-block coverage |
| Localization | English + Chinese strings for all panel labels |
| Protocol / Harmony patches | Unchanged — no wire bump, no patch surface |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Full medical data is cached | Service stores `RemoteMedicalSnapshot` for every cached remote player | `Host_CachesFullMedicalSnapshotForGuestReport` |
| Full health/limbs project correctly | `RemoteMedicalSnapshot.From(CharacterDataMsg)` copies health fields and limbs | `MedicalSnapshot_ProjectsNullAndFullHealth` |
| Existing compact vitals still work | The same service still exposes `TryGet` | existing `RemoteVitalsServiceTests` remain green |
| Read-only panel is offered only for real remote players | `CanViewMedical` is false for local, out-of-world, and no-data rows | `InWorldRemoteWithVitals_CanViewMedical`, `RemoteWithoutVitalsOrNotInWorld_CannotViewMedical` |
| Medical display is not a physical action | It does not require line-of-sight | `CanViewMedical_DoesNotRequireLineOfSight` |
| No cache leak after leaving world | `RemoteSceneChanged(false)` removes the medical entry too | `RemoteLeavingWorld_ClearsThatPlayersVitals` now also checks `TryGetMedical` |
| No cross-session leak | `SessionEnded` clears the medical entry too | `SessionEnd_ClearsTheCache` now also checks `TryGetMedical` |

## 4. Verification design

- **L0 tests:** new/updated `RemoteVitalsServiceTests` and
  `OnlineUiMemberProjectionTests` cover caching, projection, row eligibility,
  and clear paths.
- **Full regression:** `dotnet test CasualtiesUnknownOnline.slnx` — all existing
  item/player/UI domains must stay green.
- **Static evidence:** full health/limb blocks were already in the wire
  `CharacterDataMsg`; the only new code is read-only presentation.
- **Runtime evidence:** development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; manual dual-client acceptance remains a
  user release action.

## 5. Verification results (2026-09-05)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` | 2224 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| Protocol | unchanged (no bump) |

## 6. Structure review

- `RemoteMedicalSnapshot` ~415 lines, `RemoteLimbSnapshot` ~93 lines,
  `OnlineUiMedicalPanel` ~231 lines — all under the 600-line gate.
- One top-level type per file; no new expression-state bool fields.
- The cache remains owned by `RemoteVitalsService`; the new panel is a pure
  consumer of the immutable snapshots.
- Dead mechanisms: none. The old compact `RemoteVitalsSnapshot` is still used
  by the member status line; the new full snapshot is an additional read-only
  projection, not a duplicate presentation path.
