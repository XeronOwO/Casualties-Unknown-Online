# Handshake identity and structured refusal reasons

- Status: In progress
- Priority: Medium
- Category: Protocol / mod compatibility
- Source: Loomi architecture review (2026-09-20), item 10
- Related: `docs/en/reference/mod-api.md` (Handshake consistency, Versioning discipline),
  `docs/en/reference/modification-policy.md`, `done/mod-api-contract-governance.md`,
  `review/adapter-capability-catalog.md` (owns the capability degradation half)

## What it is

The handshake carries the guest's mod list and `ProtocolVersion.Current`, and a mismatch is refused
by strict equality on both sides. That policy is right and stays: the CUO wire protocol is not made
backward compatible (decisions 137/188 — the compatibility boundary is the handshake check).

What this ticket adds is an ACCURATE REFUSAL. Today one integer and one mod list answer every
question, so a client-only cosmetic mod that differs looks the same as a session-breaking one, and
the player is told "incompatible" without being told which thing was incompatible — in fact the host
only writes a log line and the guest retries its handshake forever with no message at all.

Goal: the handshake distinguishes its dimensions —

- the CUO wire protocol,
- the game build identity,
- the adapter identity/version,
- the kernel/save schema,
- the loaded synchronized mod set,
- each mod's own network/schema version,
- and the negotiated optional capabilities.

The wire protocol stays strictly equal; the other dimensions decide whether a peer is admitted, and
a refusal names the dimension that failed.

## The player's frame

Host and guest deploy the plugin, the guest joins a lobby and handshakes. If one dimension differs
(a stale CUO build, a different game build, a different save schema, a mod policy difference, a
protocol difference), today the host logs a warning and the guest keeps retrying — the guest's status
line never says why, and the lobby looks "joined but broken". After this change the guest's status
line says which thing differs and both values, the session ends instead of retrying, and the host's
log names the same dimension. A client-only mod difference never refuses (the existing policy — the
rejection list is unchanged, only its reporting becomes structured).

## Identity table

| dimension | value on the wire | source | verdict on a mismatch |
|---|---|---|---|
| protocol | `int` | `ProtocolVersion.Current` | refuse |
| game build | `string` | `UnityApplication.version` (the game) | refuse |
| CUO build | `string` | `BuildIdentity.Of` the plugin assembly (`<version>+<commit>[-dirty]`) | refuse |
| save schema | `int` | `SaveManifest.CurrentSchemaVersion` | refuse |
| mod set | the existing `ModInfoMsg` list | `IModListProvider` | refuse per the existing policy, now naming the mod and the reason |
| capabilities | — | — | NOT in this change: the negotiation's degradation semantics belong to `review/adapter-capability-catalog.md` stages 2-3, which wait on the game update. Recorded there; the mod-api table states the additive rule the future member must obey |

## Entry map

| entrance | path | what it carries |
|---|---|---|
| guest sends the handshake | `SessionPeerMaintenance.CreateHandshakeMsg` → `PacketSender.Send(HostSteamId, NetMsg.Handshake, …)`, retried every 1 s until acked | the identity block from the local provider |
| host receives it | `HandshakeHandler.Handle` — protocol, lobby membership, ban, display name, mod list, late join | one refusal per identity mismatch; the ban/late-join refusals are policy, not identity, and stay log-only |
| host acks | `HandshakeHandler` → `HandshakeAckMsg` | the host's copies of the identity block |
| guest receives the ack | `HandshakeAckHandler` — protocol check | unchanged besides carrying the host's values |
| host refuses | `HandshakeHandler` → new `NetMsg.HandshakeRefused` | dimension + detail + both values |
| guest receives the refusal | new `HandshakeRefusedHandler` | renders the line from its own catalogue, publishes on `ISessionNotices`, ends the session |
| third leg | `HandshakeAckAckHandler` | unchanged |

## Wire

`HandshakeMsg` gains `7 GameBuild`, `8 CuoBuild`, `9 SaveSchema`. `HandshakeAckMsg` carries the
host's copies of the same three. A new `HandshakeRefusedMsg` (`Host → guest`) carries `1 Dimension`
(`HandshakeDimension` enum: `Protocol=1, GameBuild=2, CuoBuild=3, SaveSchema=4, ModSet=5,
ModVersion=6`), `2 Detail` (the mod id and reason, empty for the identity dimensions), `3 HostValue`,
`4 GuestValue`.

The protocol number does not move: `ProtocolVersion.Current` is the frozen pre-release baseline
(decision 241). The change is recorded by its commit and this ticket; the wire-surface baseline is
regenerated in the same commit.

## Policy

A Runtime `HandshakeCompatibility` compares the identity block and the mod list and returns either
admit or one refusal (dimension + detail + both values). Equality on protocol, game build, CUO build
and save schema; the mod rules are the existing ones (state-bearing modes: missing, SemVer-precedence
unequal, permission unequal, mode disagreement → ModSet/ModVersion; a malformed list → ModSet;
`ClientOnly`/`Cosmetic` differences and host-only mods pass). An absent identity field (a peer that
does not send the block) is treated as "unknown" and refused by the dimension it belongs to — never
silently admitted (the pre-release rule: every client comes from this tree, decision 241; a peer
without the block is speaking a wire this build does not have).

## The guest's side

`HandshakeRefusedHandler` renders the refusal from the guest's own catalogue
(`handshake.refused.<dimension>` with both values, or the detail for the mod dimensions), publishes
it on `ISessionNotices` (the Online UI's status line) and ends the session — no more retrying. The
host's log line names the same dimension and values.

## Acceptance

- A refusal names the failed dimension: the host's log and the guest's status line both name it, and
  the guest stops retrying; driven with a crafted handshake carrying a wrong game build.
- A client-only mod difference does NOT refuse the join (policy unit test; the live join without
  mods is the baseline).
- The sync-coverage matrix and its evidence file carry `HandshakeRefusedMsg`; the wire-surface
  baseline is regenerated and the protocol number stays the frozen baseline (decision 241).
- The capability negotiation is recorded as deferred on `review/adapter-capability-catalog.md` (its
  stages 2-3), and the mod-api table states the additive rule the future member must obey.

## Run sheet

- [x] 2. Design — this section, from `docs/en/reference/mod-api.md` and the handshake code.
- [x] 3. Entry map — every place the handshake is sent/received and every refusal path.
- [x] 4. Family — every dimension's source and every refusal site.
- [x] 6. Red — the policy and handler tests fail before the code exists (12 admission facts red; the identity facts and the sender wiring followed the red run).
- [x] 7. Implement — messages, policy, handlers, composition.
- [ ] 8. Verify — build, gates, suite, format; the live handshake still completes.
- [ ] 11. Commit.
- [ ] 12. Close — status with the readings, README, census, matrix, checklist.
- [ ] 13. Acceptance — the crafted wrong-build refusal and a two-client join.
