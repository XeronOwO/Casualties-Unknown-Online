# Player-list peer-id disambiguation — self-check

Closes the remaining `player-list-polish` todo item. The IP-direct identity
decision allows duplicate cosmetic display names, so the roster needs a stable
identity fallback when names collide. This slice adds a read-only peer-id
suffix to colliding player-list rows, in both the Players page and the quick
panel, without touching the wire or the session domain.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Member row projection | `OnlineUiMemberProjection` already builds every member row from the lobby roster and display-name resolver. |
| 2 | Duplicate-name rule | `docs/backlog/resolved/ip-direct-duplicate-names.md`: duplicate names are allowed; identity is always the logical peer id / SteamID. |
| 3 | Member row data | `OnlineUiMemberRow` already carries `SteamId`; it was not rendered in the roster. |
| 4 | Roster rendering | `OnlineUiMemberListDrawer.BuildStatus` renders the per-member status line used by both the Players page and the quick panel. |
| 5 | Localization | `LocalizationCatalog` already provides en/zh key tables for the Online UI. |
| 6 | Wire/protocol | No NetMsg, packet format, or protocol version change. |

## 2. Changes

- `OnlineUiMemberRow` gains `PeerIdHex` (string?): the stable peer ID in hex,
  populated only when two or more members in the current roster share the same
  display name, compared case-insensitively.
- `OnlineUiMemberProjection.Build` computes the duplicate display-name set
  before emitting rows and fills `PeerIdHex` for every colliding member.
- `OnlineUiMemberListDrawer.BuildStatus` appends the localized peer-id suffix
  to the member status line when `PeerIdHex` is present.
- `LocalizationCatalog` adds `member.peer_id` (` [peer {0}]` / `（对端 {0}）`).

## 3. Verification

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1857 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds x 3 tables) |

## 4. L0 proof

- `OnlineUiMemberProjectionTests.DuplicateCaseInsensitiveDisplayNames_PopulatePeerIdOnEveryCollidingRow`
  locks the duplicate-name path and the full hex peer id.
- `OnlineUiMemberProjectionTests.UniqueDisplayNames_DoNotPopulatePeerId`
  locks the no-clutter path for non-colliding names.
- The suffix rendering is presentation-only over existing row state; the
  projection tests cover the data decision and the drawer only consumes it.

## 5. Structure review

- No new service, no new mutable state, no wire/session change.
- `OnlineUiMemberRow` gains one nullable string; the projection and drawer stay
  single-purpose.
- The feature is deliberately scoped to colliding rows so normal rosters do not
  show noisy ID text.
