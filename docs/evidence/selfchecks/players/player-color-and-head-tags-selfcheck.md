# Player-selectable colors and color-only head tags — self-check

Closes the "IP-direct identity / player presentation" backlog pair:
player-selectable colors and color-only, join/leave-safe head name tags. The
existing deterministic SteamId palette remains the automatic fallback; a local
palette picker now lets a player choose a shared presentation color, and that
choice travels through handshake, roster announcements and a live update.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Local color preference | `PlayerColorConfigEditor` owns the `[UI] PlayerColorIndex` BepInEx entry (`-1` = auto, `0..7` = palette). |
| Identity plumbing | `ISteamService.LocalPlayerColor` + `SetLocalPlayerColor`; `CuoNetworkRouter` writes both Steam and IP-direct adapters so a transport switch keeps the choice. |
| Join/roster wire | `HandshakeMsg`, `HandshakeAckMsg`, `PlayerJoinMsg` carry optional `HasColor` + `NetColorRgbaMsg`; `MemberPresenceTable.SelectedColor` stores it per member. |
| Live update | `PlayerColorUpdateMsg` (bidirectional) + `PlayerColorUpdateHandler`; guest → host stores/relays, host → guest broadcasts. |
| UI projection | `OnlineUiContext.PlayerColor` resolves local config → remote selected color → automatic palette; `OnlineUiMemberProjection` puts it on every row. |
| Head-name render | `OnlineUiOverlay.DrawNameplate` now draws only the colored player name; off-screen markers keep colored arrow + name + distance. |
| Preferences page | `OnlineUiPreferencesDrawer.DrawColor` exposes Auto + eight palette choices through the existing dropdown pattern. |

## 2. Changes

- **Selectable palette** — `PlayerColorResolver.PaletteValues` and `TryGet` expose
  the same eight high-contrast colors used by the automatic resolver; the
  picker persists the index and applies it to both identity adapters.
- **Wire sharing, not local-only derivation** — a selected color is carried on
  handshake/join so every peer and late joiner sees the owner's choice; `Auto`
  sends no color and all sides fall back to the deterministic SteamId palette.
- **Live mid-session update** — changing the color while a session is active
  reports it immediately (guest → host relay; host → guests broadcast) so
  existing name tags/rows update without a reconnect.
- **Head tags are color-only** — the on-screen nameplate no longer overlays
  vitals/status text; status remains available in the Players page.
- **Player-list and owner/role labels** use the effective per-member color in
  the Players page, room-owner labels, and local persona label.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1831 passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | passed (7 boxes checked) |
| `dotnet format CasualtiesUnknownOnline.slnx` | run |
| Manual acceptance | Not required by the developer-cycle rule; L0 + fake-network simulation, no manual acceptance. |

## 4. L0 / simulation proof

- `PlayerColorResolverTests` covers palette exposure and invalid-index rejection.
- `OnlineUiMemberProjectionTests` covers effective color propagation into rows.
- `NetPacketTests` covers color roundtrips for `HandshakeMsg`, `HandshakeAckMsg`,
  `PlayerJoinMsg`, and `PlayerColorUpdateMsg`.
- `SessionServiceTests.ReportLocalPlayerColor_*` drive the full fake-network
  host/guest stack and assert that guest and host changes reach the opposite
  roster presence.
- `DirectionTests` classifies the new live-update message as bidirectional and
  the fail-closed receiver contract remains green.

## 5. Structure review

- New runtime files are one type/file and stay small: `PlayerColorConfigEditor`,
  `PlayerColorUpdateMsg`, `PlayerColorUpdateHandler`.
- `MemberPresenceTable` only gains a nullable wire color field; no cross-domain
  service was introduced.
- `SessionService` gained only a small local report method; the class remains
  well below the 600-line gate.
- `OnlineUiOverlay` lost the vitals/status rendering from head tags, which
  removes UI-only state from the nameplate path.
