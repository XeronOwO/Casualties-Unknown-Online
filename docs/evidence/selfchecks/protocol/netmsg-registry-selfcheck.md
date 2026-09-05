# NetMsg direction registry / fail-closed — self-check (2026-08-23)

Backlog §3.2 called this out: `PacketReceiver.IsValidDirection` was a
manually maintained, fail-open switch; unknown/new message ids defaulted to
valid, and the direction classification was duplicated between the receiver
switch and `DirectionTests`. This cycle replaces that with a single protocol
message registry consumed by both the receive and send sides.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `PacketReceiver.IsValidDirection` | Old fail-open `switch` in `src/.../Session/PacketReceiver.cs`; `_ => true` accepted any unclassified/unknown id. |
| `PacketHandlerAttribute` | Old attribute carried only `NetMsg`; direction lived only in the receiver switch. |
| `DirectionTests` | Already contained the full 3-way direction contract (`GuestToHost` / `HostToGuest` / `Bidirectional`, 80 ids). |
| Handler generic payload | Every handler derives `PacketHandlerBase<TPacket>`, so the registry can derive the wire payload type without a second manual table. |
| `PacketSender` | Send side had no guard: an unregistered/unknown id would be encoded and sent, then silently dropped by the receiver. |

## 2. Whole-family audit

The whole direction family was aligned in one pass:

- Every `[PacketHandler]` attribute in the Runtime assembly now carries an
  explicit `NetMessageDirection` (80 handler files).
- The receiver no longer has a per-message switch; it asks
  `NetMessageRegistry` and fails closed for unknown ids.
- The sender now refuses unregistered ids before encoding (`TrySend` and
  `SendToAll`).
- `PacketDispatcher` validates each handler's registered id against the
  registry at startup.
- `DirectionTests` remains the independent contract; it now exercises the
  registry-backed `PacketReceiver.IsValidDirection`, so a wrong direction in
  any handler attribute fails its theory row.
- No message id, payload shape, protocol version or handler behavior changed.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Direction table | One explicit `NetMessageDirection` per handler attribute + immutable registry | `NetMessageRegistry`, `NetMessageMetadata`, all `Session/Handlers/*.cs` |
| Receiver | Fail-closed `NetMessageRegistry.TryGet` + role check | `PacketReceiver.OnTransportMessage` / `IsValidDirection` |
| Sender | Reject unregistered ids before encode | `PacketSender.EnsureRegistered` |
| Dispatcher | Startup check: handler's id must exist in registry | `PacketDispatcher` ctor |
| Payload type | Derived from `PacketHandlerBase<TPacket>` generic, not handwritten | `NetMessageRegistry.FindPayloadType` |
| Unregistered ids | No default-true path; raw unknown ids are dropped/refused | `NetMessageRegistryTests` |
| Wire/protocol | Unchanged — same ids, same payload classes, same `ProtocolVersion` | Full suite + gates |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1248 passed / 0 failed (full suite) |
| `dotnet test ... --filter DirectionTests\|NetMessageRegistryTests` | 86+ passed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<game-dir>"` | deployed to real game dir |
| Protocol | unchanged |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Deployed to the real game directory only; no sandbox path.
- Static: the registry is the only direction source; no runtime dual-side
  manual acceptance is required for this protocol-metadata refactor
  (user rule 2026-08-16).

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 7. Structure review

- New files: one top-level type per file (`NetMessageDirection`,
  `NetMessageMetadata`, `NetMessageRegistry`).
- Touched runtime files remain well within the 600-line gate.
- No new expression-state bool fields.
- No mutable shared state: the registry is an immutable startup-only map; the
  message metadata is constant protocol data.
- No dead mechanism left behind: the old switch and `_ => true` default are
  removed, not co-existing with the registry.
