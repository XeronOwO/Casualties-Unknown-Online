# Mixed CUO versions are refused by a named dimension — batch 20261107-w

- Date: 2026-11-07
- Artifact: the host and the guest ran the deployed `0.1.0+544bd87fdea8450fdc75aaa026936b391b08d6e4` (acceptance
  channel deployed). The old client is a build of commit `3005409c`, exported with `git archive` to a scratch tree,
  built there, and placed into the Steam2 box's own plugin folder: one install serves every client, so a second
  version can only live in a box's shadow.
- Lobby: `109775243776560327`.

## The fixture

The host and Steam1 launched from the deployed build; Steam2 launched from the old build's plugin set. The old build
predates the identity block, so its handshake carries protocol and mods but no game build, framework build or save
schema. The host runs the handshake policy that reads the identity block first and refuses on the first dimension
that disagrees or is absent (`src/CasualtiesUnknownOnline.Runtime/Session/HandshakeCompatibility.cs`).

## Reading 1 — an old client cannot join, and the host names the dimension

The launcher joined Steam2 to the lobby (`alt joined`), and the host logged, once per retry from 23:33:10:

```
[2026-11-07 23:33:10.929] [WRN] [CasualtiesUnknownOnline.Runtime.Session.Handlers.HandshakeHandler] Handshake from 76561199526807662 refused: GameBuild (7.0.1 vs ); .
```

The old client never entered the world: the launcher's own check printed `all in world: False` after four minutes,
while the host and Steam1 were in it. The refusal names the first absent identity field (`GameBuild`: host `7.0.1`
against an empty member value), because the old handshake carries no identity block at all.

## Reading 2 — the CUO-build dimension itself

The new guest sent a crafted handshake whose framework build differs (`identity-mismatch dimension=cuo
value=0.0.0-crafted` → `sent:cuo:0.0.0-crafted:true:host=76561198281246659`). The host refused it at 23:33:57.900:

```
[WRN] [HandshakeHandler] Handshake from 76561198863287957 refused: CuoBuild (0.1.0+544bd87fdea8450fdc75aaa026936b391b08d6e4 vs 0.0.0-crafted); .
```

The guest rendered the same refusal on its own status line at 23:33:57.928, in its own language:

```
[WRN] [HandshakeRefusedHandler] the host refused this client: CuoBuild (host 0.1.0+544bd87fdea8450fdc75aaa026936b391b08d6e4, member 0.0.0-crafted, detail ) — status line: the host runs CUO build 0.1.0+544bd87fdea8450fdc75aaa026936b391b08d6e4; this client is 0.0.0-crafted. Both sides must install the same build. (translated from the client's Chinese status line)
```

## What this settles

Stage-1's row 20 ("mixed CUO versions"): an old client cannot enter a world hosted by the current build. The host
refuses deterministically — one line per retry — and names the failing dimension; a member that understands the
refusal sees the same dimension and values rendered in its own language and its session ends instead of retrying.

## Limits

- The old build cannot render the refusal message (it predates it), so its half of the reading is the host's line and
  the missing world entry; that is the honest boundary of a build that predates the feature.
- The fixture put the old plugin set into the Steam2 box's shadow folder because the deploy model keeps one tree for
  every client. The folder was removed after the reading and the box reads through to the deployed build again.

## Cleanup

The shadow plugin folder was emptied file by file and removed; the host and Steam1 stayed up, Steam2 was quit for the
swap.
