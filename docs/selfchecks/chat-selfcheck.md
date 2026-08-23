# Text chat — simple co-op chat line (ProtocolVersion 36)

Owner cycle: backlog "Text chat — MEDIUM/HIGH. CUO currently has in-world Talker
bubbles only; a simple chat message + UI is the first clear communication
feature." Decision for this cycle: implement a host-relayed text-chat line with
a small bottom-right IMGUI panel. No voice, no per-channel rooms, no
distortion, no server announcements — those stay future work.

## Decision summary

- One `ChatMsg` (NetMsg 109, `ProtocolVersion 36`) carries the author SteamId +
  the final text. It is **bidirectional in wire shape** but star-relayed: a
  guest reports its own line to the host, the host validates and broadcasts to
  every other member, and a guest never sends directly to another guest.
- The host is the relay authority and the only anti-spoof gate: it drops a line
  whose claimed `SenderSteamId` does not match the transport sender, and it
  drops empty/whitespace/oversized text before surfacing it.
- The Runtime `ChatService` owns a bounded 50-line recent buffer and the send
  path. It is pure Runtime (no Unity object, no Steamworks), so the same path is
  covered by L0 fake-network tests.
- The Online UI panel is deliberately simple: last 7 lines, one text field, one
  Send button, persona names resolved from `SteamService`. It is visible while
  the session is active, including between the lobby handshake and world entry.
- Text limit: 200 characters, enforced by the shared `ChatPolicy` used by both
  the UI/send path and the host receive/relay path.
- Session end clears the local recent buffer, so a chat log never leaks into a
  new lobby.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Wire message | `ChatMsg` (NetMsg 109) — `SenderSteamId` + `Text`; one shape for guest→host report and host→guest relay |
| 2 | Direction | `PacketReceiver.IsValidDirection` default-true for bidirectional `Chat`; `DirectionTests.BidirectionalMessages` now explicitly classifies it (the completeness guard fails otherwise) |
| 3 | Channel | `ChatChannel` — `SendChat` (guest→host), `BroadcastChat` (host except author), `FireChatReceived` |
| 4 | Handler | `ChatHandler` — pure-text validation, host spoof check, local event, host relay |
| 5 | UI/service | `ChatService` + `IChatControl` — bounded recent buffer, `TrySend`, `MessageReceived`, session-end clear |
| 6 | Online UI | `OnlineUiOverlay.DrawChatPanel` — recent lines + input + Send button |
| 7 | Protocol | `ProtocolVersion.Current` 36 (new wire message) |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `NetMsg` / `ChatMsg` / `ProtocolVersion` | New `Chat = 109`, message type, v36 |
| `ChatChannel` | New wire channel in `Runtime.Session.World` |
| `IWorldControl` / `WorldService` / partial | Exposes `SendChat`, `BroadcastChat`, `ChatReceived` |
| `ChatHandler` | New `[PacketHandler(NetMsg.Chat)]`; host validates + relays |
| `ChatService` / `ChatPolicy` / `ChatLine` / `IChatControl` | New Runtime chat domain |
| `Plugin` | Resolves `IChatControl`, passes it to the overlay |
| `OnlineUiOverlay` | Draws the chat panel and sends through `IChatControl.TrySend` |
| `DirectionTests` | Adds `NetMsg.Chat` to the bidirectional classification |
| `ChatServiceTests` | New end-to-end fake-network tests |

No GameAdapter, game-method, entity-event, item or character-domain paths
changed. The chat domain is independent of the already-complete native-content
sync matrix.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Guest report reaches host | A guest's `TrySend` sends `ChatMsg` to the host and the host buffer receives it | `GuestChat_ReachesHostBuffer` |
| Host line reaches guest | Host `TrySend` broadcasts and the guest buffer receives it | `HostChat_ReachesGuestBuffer` |
| Host relays to other guests | A guest's line is broadcast to every other guest, not back to the author | `GuestChat_IsRelayedToTheOtherGuest` |
| Invalid/oversized refused locally | whitespace/empty/201-char line returns false, no wire send, no buffer entry | `InvalidOrOversizedLine_IsRefusedLocally` |
| Spoofed sender dropped | A guest transport frame claiming another author is dropped at the host | `SpoofedSender_IsDroppedAtHost` |
| Session-end clear | `SessionEnded` clears the recent buffer | `SessionEnd_ClearsChatBuffer` |
| Direction classification | `Chat` is explicitly bidirectional | `DirectionTests.EveryNetMsg_IsExplicitlyClassified` |
| Protocol bump | v35 peer has no chat handler; v36 required | `ProtocolVersion.Current` |

## 4. Verification design (development-period, no manual acceptance)

- **L0 fake-network tests**: `ChatServiceTests` — guest→host, host→guest,
  host→other-guest relay, invalid/oversized refusal, spoof drop, session-end
  clear.
- **Direction guard**: `DirectionTests` classifies the new message.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` —
  **1238 passed / 0 failed**.
- **Gates**: `dotnet build` 0 warnings / 0 errors; `dotnet format`; 
  `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` all pass.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-23)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1238 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | 36 (new NetMsg 109) |

## 7. Structure review

- `ChatService` ~95 lines, `ChatChannel` ~52, `ChatHandler` ~40,
  `ChatPolicy` ~20, `ChatLine` one record, `OnlineUiOverlay` still under the
  600-line gate.
- One top-level type per file; no new expression-state bools.
- The chat history is owned by `ChatService` with a read-only `Recent` surface;
  the overlay only projects display data.
- Dead mechanisms: none. The existing `SpeechMsg`/Talker path remains the
  in-world bubble channel; text chat is a separate communication surface.

## 8. Accepted boundaries

- No voice, no channel/room, no formatting, no server announcements.
- No chat history persistence across sessions; the buffer is session-scoped.
- The host is the relay authority; there is no per-peer direct chat in this
  slice.
- The UI is a minimal IMGUI panel (recent 7 lines + one input), matching the
  existing Online UI style.
- **Later note (2026-08-23):** this IMGUI chat panel was disabled/removed from
  the overlay because its input field captured Tab/WASD while playing. The
  Runtime chat channel/service and wire message remain; the UI will be redone
  as part of a Minecraft-style command input surface (see `docs/backlog.md`).
