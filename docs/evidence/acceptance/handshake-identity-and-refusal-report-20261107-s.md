# Handshake identity and structured refusal — batch 20261107-s

Rows of `docs/backlog/done/handshake-identity-and-refusal-report.md`: the identity block is compared per
dimension and a refusal names the one that failed; the guest renders the line from its own catalogue and ends
its session instead of retrying; an ordinary join still completes.

- Artifact: `0.1.0+e36fc0469da7ff7c2514927cbe40122dbccc8b9a` (`tools/verify-deploy.ps1` exit 0, production package).
- Clients: host `ws://127.0.0.1:18590` (76561198281246659), Steam1 `ws://127.0.0.1:18591`
  (76561198863287957), Steam2 `ws://127.0.0.1:18592` (76561199526807662). Lobby `109775243757895681`;
  all three `inWorld=true` at 20:58:09 — the ordinary join completed with the identity block present on
  every handshake (no refusal).

## The crafted handshake

Steam1 declared `identity-mismatch` (`tools/acceptance/driver/eval-declarations/identity-mismatch.cs`) —
`"type":"AcceptanceIdentityMismatch"`, `"sent":true` — and ran the recipe with the game dimension crafted:

```
-Action recipe -Recipe identity-mismatch -RecipeArg @('dimension=game','value=0.0.0-crafted')
→ "result": "sent:game:0.0.0-crafted:true:host=76561198281246659"
```

The declaration copies this client's own Cuo build and save schema, sets `Protocol = 1` and only the game
build differs, so exactly one dimension can fail.

## Host side

```
[Warning ... Session.Handlers.HandshakeHandler] Handshake from 76561198863287957 refused: GameBuild (7.0.1 vs 0.0.0-crafted); .
```

It names the dimension and both values. The refusal is one frame (`NetMsg.HandshakeRefused = 166`).

## Guest side (Steam1, its own catalogue, language `zh`)

```
[WRN] [CasualtiesUnknownOnline.Runtime.Session.Handlers.HandshakeRefusedHandler] [Handshake] the host refused this client:
GameBuild (host 7.0.1, member 0.0.0-crafted, detail ) — status line:
(translated from the guest's zh catalogue) the host runs game version 7.0.1; this client runs 0.0.0-crafted. Both sides must run the same game version.
[INF] [CasualtiesUnknownOnline.OnlineUiHost] [OnlineUi] status line: (the same translated line)
```

The rendered line is the guest's own catalogue (`handshake.refused.game_build`), and the Online UI printed
it on its status line. The session ended one frame later:

```
20:58:52.228 ItemArbitration: Session ended — cleared the transfer table (0 entries).
20:58:52.232 AnnouncedCheatPermissions: the session ended — the announced cheat-permission list is dropped…
20:58:52.233 [Mod:cuo.example] [Example] session ended.
```

The guest's state read `inWorld=false`; its log has five `Retrying handshake` lines (20:57:53–20:57:57, the
connection's own retries before the refusal) and none after 20:58:52.

## Bystander

Steam2 stayed `inWorld=true` throughout; the refusal is between the host and the peer whose handshake failed.

## Limits

- The frame rides Steam1's already-established transport, not a fresh connection: the identity comparison in
  the handshake body is what is proven, not a transport-level refusal at connection setup.
- The host's member row for an already-handshaken peer is not removed by this refusal (the peer is still in
  the lobby); a real stale-build join is refused before the member exists. Recorded as a named residual.
- The Cuo-build and save-schema dimensions share the same comparison (unit-tested per dimension) and were not
  driven separately this batch.
- The client-only mod row is covered by the policy facts (12) and the live join without mods.
