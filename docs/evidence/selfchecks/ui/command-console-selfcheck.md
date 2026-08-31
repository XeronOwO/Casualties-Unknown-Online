# In-game command console — self-check

Owner cycle: backlog "Minecraft-style in-game command console". Decision: implement
a local command/chat console as a new Online UI page. The existing Runtime
`ChatService` remains the text-chat send path; slash-prefixed lines go through a
new local command chain. No new wire message, no protocol/version bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Command chain | `CommandConsoleService` owns registration, slash parsing, role permission, execution and output buffering. |
| 2 | Chat forwarding | Non-command input is forwarded to `IChatControl.TrySend`; received/sent chat lines are mirrored into the console output buffer. |
| 3 | Host admin commands | `/kick`, `/ban`, `/unban` use the existing `ISessionControl.KickMember` and `IHostBanService` host paths. |
| 4 | UI surface | New `Console` tab in the modal Online UI; input is deliberately inside the modal so the old chat-panel key-capture issue does not return. |
| 5 | Registration | `CommandConsoleService` + `ICommandControl` registered in `CuoBootstrap`. |
| 6 | No wire change | No `NetMsg`, no packet handler, no `ProtocolVersion` change. |

## 2. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Help lists commands | `/help` returns command names/descriptions | `CommandConsoleServiceTests.Help_ReturnsAvailableCommands` |
| Unknown command handled | Unknown slash command adds an error line | `CommandConsoleServiceTests.UnknownCommand_AddsErrorLine` |
| Chat send through console | Plain text goes through `IChatControl` and echoes in console output | `CommandConsoleServiceTests.PlainText_SendsChatAndEchoesInConsole` |
| Host-only permission | A guest running `/kick` gets a host-only refusal | `CommandConsoleServiceTests.HostOnlyCommand_IsRefusedForGuest` |
| Kick command | Host `/kick` removes the member through the real session path | `CommandConsoleServiceTests.HostKick_RemovesMember` |
| Ban/unban commands | Host `/ban`/`/unban` round-trip through `IHostBanService` | `CommandConsoleServiceTests.HostBan_And_Unban_RoundTrip` |
| Clear | `Clear()` empties the output buffer | `CommandConsoleServiceTests.Clear_EmptiesOutput` |
| Output bounded | Buffer caps at `MaxLines` | code review: `AddLine` removes oldest |
| No wire/protocol regression | No `NetMsg`/handler added | full suite + direction guard untouched |

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1864 passed / 0 failed |
| New console tests | 7 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
| Protocol | unchanged |

## 4. Structure review

- `CommandConsoleService` ~350 lines, one top-level type per file, no new
  expression-state booleans.
- The UI drawer is presentation-only; policy and output state stay in Runtime.
- The command registry is a small list with case-insensitive lookup; this is a
  human-facing console, not a hot-path registry.
- The existing `ChatService` and `ChatChannel` remain untouched except that the
  console consumes the chat send path.
