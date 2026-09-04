# Remote player medical/health panel is missing or not enabled

- Status: Review
- Priority: Medium
- Category: Remote player UI / medical inspection
- Source: User report (2026-09-04) — it is not possible to open another player's medical panel and view their body condition. This may be an unimplemented feature or an existing-but-not-enabled path. Record only; no code action taken yet.

## Landed

Implemented a CUO-side read-only medical panel fed by the existing 1 Hz
character-data stream. No native medical UI focus was attempted; the panel uses
the same already-synced data model and keeps all display-only invariants.

- `RemoteVitalsService` now caches a full `RemoteMedicalSnapshot` (all
  `CharacterHealthMsg` fields) plus `RemoteLimbSnapshot` entries alongside the
  existing compact vitals projection.
- New `OnlineUiMedicalPanel` renders general/nutrition/circulation/trauma/
  status/limb sections; it is reachable from the Players page, quick panel,
  and in-world right-click menu.
- The panel is read-only: no remote clone mutation, no authority message, no
  protocol change.

Selfcheck: `docs/evidence/selfchecks/players/remote-medical-panel-selfcheck.md`.

## Goal

Allow a player to open a medical/health panel for another player and view their detailed body condition, either by reusing the game's own medical UI with a remote focus (like the native remote backpack) or by adding a CUO-side panel fed by already-synced character data.

## Current behavior

- CUO currently exposes only a compact vitals line for remote players:
  - `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OnlineUiMemberRow.cs` / `OnlineUiMemberProjection.cs` — `VitalsText`.
  - `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/RemoteVitalsSnapshot.cs` — `ToShortString()` shows only HP, hunger, thirst and stamina.
- The underlying wire data is richer:
  - `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/CharacterHealthMsg.cs` — full physiological/status fields (blood, heart rate, pain, shock, sickness, radiation, temperature, consciousness, etc.).
  - `docs/evidence/selfchecks/players/remote-vitals-selfcheck.md` — the 1 Hz character-data stream already delivers `CharacterHealthMsg` to every side; the existing cache is intentionally a small display projection.
- There is currently no action in the right-click menu, Players page or quick panel to open a "medical/health panel" for a remote player, and no native adapter path analogous to `IGameAdapter.OpenRemoteBackpack`.

## Investigation needed

1. Determine what the game's "medical panel" is:
   - a native UI that can be opened for another body (if so, can it be pointed at a remote render clone like `RemoteBackpackCoordinator`/`RemoteBackpackView`?),
   - or a CUO-side panel that must be built from existing synced data.
2. Determine what data a useful medical panel needs:
   - full `CharacterHealthMsg`,
   - limb/wound state (broken/dismembered/bleeding/pain/infection),
   - carried status/equipment that affects diagnosis,
   - any data not currently retained by `RemoteVitalsService` (it currently stores only a small `RemoteVitalsSnapshot`, not the full health block).
3. Decide the UI entry point and surface:
   - right-click player context menu,
   - Players page row action,
   - quick panel action,
   - or a dedicated hotkey/panel.
4. Check whether all required data already rides the 1 Hz character stream or whether additional data/wire work is needed.

## Required design direction (for the implementation cycle)

- If the native medical panel can be safely focused on a remote clone, follow the established remote-backpack pattern: a thin adapter focus + patches + no authority mutation.
- If not, build a CUO IMGUI/detail panel using a read-only remote player state model, not the live game objects.
- Extend the remote character cache to retain the full needed health/limb projection (or add a separate read-only model) without leaking Unity/game types into Runtime.
- Ensure the panel updates from the existing 1 Hz data and does not create a new wire protocol unless required.
- Keep all display-only and read-only; no remote body mutation.

## Acceptance criteria (for the later implementation cycle)

- A player can open a medical/health panel for another player and see detailed body condition.
- The panel is reachable from the existing player-facing UI (right-click/Players/quick panel or a clear documented entry).
- The displayed data is current and correctly attributed to the selected remote player.
- No dead/duplicate presentation path is left behind.
- Existing tests and repo gates remain green.
- If a native remote medical view is implemented, it must be safe against remote clone proxies (no mutation, no display-proxy reporting bugs).

## Non-goals

- Not implementing in this cycle — this ticket is a backlog record only.
