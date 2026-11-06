# Remote medical Stage 1 — row 20 (mixed CUO versions), batch 20261106-o

Row 20 of `docs/backlog/done/remote-medical-stage-1-injection-session.md`: "Mixed CUO versions — strict
handshake rejection after protocol-version bump; no old/new compatibility shim is provided."

- Artifact: `0.1.0+e0727f8f` (acceptance build, `tools/verify-deploy.ps1` exit 0).
- Clients: host `ws://127.0.0.1:18590` (76561198281246659) and guest `ws://127.0.0.1:18591`
  (76561198863287957) in one world, lobby `109775243644362155`, both `inWorld=true`, `gateWaiting=false`.

## Instrument

The protocol number is frozen at `1` since `c2eaa962` (pre-release: no wire change moves it), so an old client would
announce a retired number — `c2eaa962^` declared `public const int Current = 45;`. Instead of building that old tree,
`tools/acceptance/driver/eval-declarations/version-mismatch.cs` builds the exact `HandshakeMsg` an old client sends
(`Protocol = 45`, empty `SceneStateMsg`) and hands it to the live session's own `PacketSender`, addressed to the host.

- Declaration on the guest: `-Action declare -Declare version-mismatch` → `"type":"AcceptanceVersionMismatch",
  "assembly":"eval-41"`, `sent:true`.
- Recipe: `-Action recipe -Recipe version-mismatch -RecipeArg @('protocol=45')` →
  `"result":"sent:45:true:host=76561198281246659"` (the send primitive accepted the frame).

## Reading

Host log, 2026-11-06 22:55:20.486:

```
[WRN] [CasualtiesUnknownOnline.Runtime.Session.Handlers.HandshakeHandler] Peer 76561198863287957 speaks protocol 45; we speak 1. Rejecting.
```

The handler returns before touching any state: no ack, no world join, no shim path (the only protocol branch in
`HandshakeHandler.Handle` is this equality check). Both clients were re-read after the rejection and still show
`inWorld=true`, same lobby, `gateWaiting=false` — a rejected handshake from an already-connected peer does not disturb
the running session.

## Limits

- The frame travels over the guest's already-established session transport, not a fresh connection: this proves the
  version gate where it lives (the handshake body), not a transport-level refusal at connection setup. A real old build
  would fail at exactly this check on its first handshake, so the world entry is what it loses.
- Only the host→peer direction was driven; the guest-side mirror (`HandshakeAckHandler`) carries the same equality
  check and was not exercised separately.
