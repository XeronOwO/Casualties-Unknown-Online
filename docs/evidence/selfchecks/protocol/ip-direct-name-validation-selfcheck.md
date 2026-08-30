# IP-direct display-name validation — self-check (2026-09-01)

Backlog §"IP-direct identity / player presentation" listed IP-direct name
validation as the first open row. This slice closes that row: the configured
display name is now a required join contract on both sides, not an optional
label.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Local configured name | `IpDirectConfigEditor` owns `[IpDirect] DisplayName`; `IpDirectActions` writes it into `IpDirectSteamService` before host/join. |
| Local refusal | `IpDirectSteamService.StartHost` / `Connect` validate the configured name before touching the TCP transport. |
| Host-side refusal | `HandshakeHandler` validates the inbound `HandshakeMsg.DisplayName` before creating a member, alongside the existing ban/mod/late-join gates. |
| Normalization | Display names are trimmed at every storage point (`HandshakeHandler`, `HandshakeAckHandler`, `PlayerJoinHandler`). |
| UI error surface | `IpDirectActions` maps an invalid local name to the localized `ip.display_name_required` message; the Home page's IP error row renders it. |

## 2. Changes

- `IpDisplayNamePolicy` — one shared validation contract: non-empty after
  trim, max 24 chars, no control characters.
- `IpDirectSteamService` — refuses `StartHost`/`Connect` when the local
  display name is invalid.
- `HandshakeHandler` — rejects an inbound peer with an empty or malformed
  display name (no member creation, no ack).
- `HandshakeAckHandler` / `PlayerJoinHandler` — normalize stored names.
- `IpDirectActions` — localized pre-check so the Online UI shows a clear
  error instead of a raw transport failure.
- `LocalizationCatalog` — `ip.display_name_required` (en/zh).

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1820 passed / 0 failed (full suite) |
| `IpDisplayNamePolicyTests` | empty/whitespace/length/control coverage |
| `IpDirectSteamServiceTests` | local host/join refusal with empty name |
| `HandshakeTests` | host rejects empty and over-long inbound display names |
| `IpDirectSessionIntegrationTests` | existing TCP handshake tests updated to use valid names |
| Protocol | no `NetMsg` / `ProtocolVersion` change |

## 4. L0 proof

- Policy tests lock the shared validation rules and canonical trimming.
- Adapter tests prove the local side refuses an empty configured name before
  any TCP listener/connection is created.
- Handshake tests drive the real session/handshake path through the fake
  network and prove the host does not add an invalid-name member.

## 5. Structure review

- `IpDisplayNamePolicy` is a small stateless static policy, one file/type.
- `IpDirectSteamService` only gains a guard at its two entry points; no new
  state.
- `HandshakeHandler` follows the existing reject-before-create pattern used
  by ban/mod/late-join gates; no new service or wire surface.
- No touched class approaches the 600-line architecture gate.
