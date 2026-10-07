# Mod-defined wire packets — attribute inventory and self-check

Owner cycle: `review/mod-defined-wire-packets.md` (2026-10-07), the "custom replication domains" entry of
`todo/mod-content-ceiling.md` Part 3 C, promoted with its own protocol decision.

Question the cycle answers, in the user's words: a mod must be able to define its own behaviour packets and
its own handling chain, not only ride CUO's built-in messages.

Decision (recorded as decision 243): a mod-owned packet id rides the EXISTING mod frame beside the mod id
(`ModMessageMsg.PacketId`, empty = the packet-less anonymous tunnel form), and the declaration carries the
whole policy — who may send it, which copies run its chain, and the chain of `Validate` / `Apply` / `Observe`
handlers — while CUO owns the frame, the routing, the relay and the refusal story.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | What a mod could send before this cycle | `IModNetwork`'s three calls over one wire kind, `NetMsg.ModMessage` (75) carrying `ModMessageMsg { ModId, Payload }` — `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModChannel.cs`, the three `_sender.Send`/`_session.Broadcast` sites |
| 2 | The identity that traffic carried | the MOD id only (`ModMessageMsg.ModId`); no message-type identity on the wire, so a multi-message mod demultiplexed inside its own bytes and the framework could not route, log or refuse by message type |
| 3 | Where a mod-owned id had to be sliced in | `ModMessageMsg` itself (the packet id belongs beside the mod id, in the frame the router already decodes) — a second `NetMsg` id would duplicate the handler, the rate limiter and the payload cap for one wire kind |
| 4 | The handling chain before this cycle | one event, `IModNetwork.MessageReceived`, fired to every subscriber in subscription order by `ModLifecycle.OnModMessageReceived` |
| 5 | The framework's own consumer of the tunnel | `ModStatusTransport` publishes `ModStatusUpdate` frames over `IModNetwork.Broadcast` — the packet-less form is not legacy and does not go away |
| 6 | What the receive path already gated | per-sender token bucket (20/s, burst 40), the 64 KiB cap at both ends, the mod lookup, the `SendNetworkMessage` declaration — and NOT membership, unlike the host-command path (`ModCommandService` checks `member.Handshaken`) |
| 7 | Relay | none for mod traffic: "no auto-relay" meant every mod wrote the report-and-fan-out dance by hand, which is the one part of it a single callback cannot give |
| 8 | Null payload | `msg.Payload.Length` was dereferenced unguarded at two sites on the same frame (`ModLifecycle.OnModMessageReceived` and `ModMessageHandler.Handle`), and the tunnel's send side did the same in `ModChannel.CheckLength`; all three are guarded now. The premise is stated honestly below: an ABSENT payload member cannot come off the wire (`ModMessageMsg` is a `[ProtoContract]` without `SkipConstructor` and initializes `Payload = []`, so protobuf leaves the empty array), so these guards are the API boundary's own defence against a null handed in from a caller, not a peer shape |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModMessageMsg` | gains `PacketId` (protobuf member 3); the doc comment states both forms |
| `NetMsg.ModMessage` | its log comment restated: optional packet id, the anonymous form, and the declared-packet relay |
| `IModPackets`, `IModPacketContext`, `ModPacket`, `ModPacketHandler`, `ModPacketStage`, `ModPacketSender`, `ModPacketDelivery` | new, all `Experimental` (the promotion funnel's step for a surface with no third-party consumer yet) |
| `IModContext.Packets` | new member, marked `Experimental` because `IModContext` itself is `Stable` |
| `ModPacketsAdapter` | new: the per-mod registry, the send surface, the chain runner, the router verdict, and the re-entrancy guard (a packet this copy is already running is refused, so a chain that starts it again cannot recurse) |
| `ModPacketPolicy` | new: id grammar (the content-id path grammar), chain/count caps, the payload rail |
| `ModPacketRoute` | new: the router's verdict (unknown / refused / applied / relay) |
| `ModChannel` | four packet sends (host, peer, all, relay) that never fire the sender's own copy — a packet's local run is the declaration's decision and the adapter performs it; its own role/session/cap guards answer for the wire boundary, and `CheckLength` no longer throws on a null payload |
| `ModLifecycle` | packet routing, the relay, the packet-id grammar rail, the null-payload guard and the handshaken-member gate, which now covers BOTH mod-message forms (the family fix beside the new mechanism) |
| `ModMessageHandler` | its log line dereferenced `msg.Payload` unguarded on the same frame — now null-safe, so no path on this frame can throw into the dispatcher |
| `ModContext` | was 596 lines against the 600-line hard gate; its seven per-mod adapters moved into their own files with no behaviour change and the file is 151 lines now |
| Consumers | `ExampleMod` declares `example.echo` + `/exampleecho` beside its tunnel echo (a guest reports it, the host broadcasts it); `ExampleMachineMod` (`cuo.example.machine`) declares `machine.use` / `machine.state` + `/machineuse` |
| Docs | `mod-api.md` both blocks (context row, the *Declared packets* section with the two policy tables and the sender attribution, version discipline, verification), `protocol-messages.md` both blocks (row 75), the term registry (declared packet / packet chain / anonymous tunnel) and both glossaries, the sync-coverage N8 row + 2 anchors, decision 243 |
| Tests | `ModPacketsTests` (20), `ModPacketsFailureTests` (11), `ModPacketsRailsTests` (11) sharing `ModPacketsWorld`, plus `ModHandshakeProtocolTests` (+2) |
| Protocol version | unchanged — frozen pre-release (decision 241); the frame change is recorded by this commit and this ticket |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The packet id is the mod's own | it rides the frame beside the mod id and is what the receiver routes by | `ModMessageFrame_RoundTripsTheDeclaredPacketId`, `ModMessageFrame_WithoutAPacketId_DecodesToTheAnonymousTunnelForm` |
| The tunnel form still works | an empty id routes to the single callback as before | existing `ModMessageTests` (13 cases) green unchanged |
| Who may send | enforced at the sender AND at the host on a member's frame; a guest never re-judges | `HostOnlyPacket_AGuestCannotStartIt`, `HostOnlyPacket_AMembersFrameForItIsRefusedAtTheHost`, `GuestOnlyPacket_TheHostCannotStartIt` |
| Who runs the chain | EVERY documented delivery cell, the two host-side `EveryMember` cells included | `GuestReport_EveryOtherMember_…`, `GuestReport_EveryMember_RunsOnTheReporterFirst`, `HostBroadcast_EveryMember_…`, `HostBroadcast_EveryOtherMember_SkipsTheHostsOwnCopy`, `HostDirectedSend_ReachesOnlyThatMember_…`, `HostDirectedSend_EveryMember_RunsOnTheHostAndThatMember`, `HostLocalSend_HostOnlyPacket_…`, `HostLocalSend_EveryMember_RunsOnTheHostsOwnCopyOnly`, `EveryOtherMember_TheHostHasNoOtherCopyToSendTo`, `HostOnlyDelivery_DirectedSendAndBroadcastAreRefused`, `AGuestCannotBroadcastOrSendToAMember` |
| The relay | the host relays to every OTHER member and the reporter never runs it twice | `GuestReport_ReachesTheHostWithTheReportersIdentity_AndTheRelayCarriesTheHosts` |
| Chain order | stages run in the framework's order whatever the declaration order; order inside a stage is the declared one | `TheDeclaredChain_RunsStagesInFrameworkOrder_AndKeepsTheDeclaredOrderInsideOne` |
| The payload belongs to one delivery | a handler's in-place write reaches this chain's later handlers and neither the relayed frame nor the frame a local run's send carries | `AHandlersInPlaceWrite_StaysInsideItsOwnDelivery` (the first byte each copy saw is 1, the second is the rewrite) |
| A refusal stops the delivery | no later stage and no relay, on the receive path AND on a local run before the frame leaves | `ARefusingValidateStage_StopsTheChainAndTheRelay`, `ARelayedPacket_StillReachesTheOtherMemberWhenValidateAccepts`, `ALocalRefusal_StopsTheFrameBeforeItLeaves` |
| A throwing handler | isolated with mod/packet/stage in the log, the chain continues, the router survives | `AThrowingHandler_IsIsolatedAndTheRestOfTheChainStillRuns`, `AThrowingHandler_DoesNotWedgeTheNextPacket` |
| The receive path's refusals | an undeclared id, an illegal id, a peer that is no member, a member that never handshook (both message forms) and a retired declaration | `AnUndeclaredPacketId_IsDroppedAtTheReceivingCopy`, `APacketIdThatIsNotALegalId_IsDroppedByItsLengthAlone`, `AFrameFromAPeerThatIsNoMemberAtAll_ReachesNoChain`, `AFrameFromAMemberThatHasNotHandshaken_ReachesNoChain`, `AFrameFromAMemberThatHasNotHandshaken_ReachesNoTunnelCallbackEither`, `AChainThatRetiresItsOwnDeclaration_StillFinishes` |
| A chain that starts its own packet | the second entry is refused by the re-entrancy guard instead of recursing to a stack overflow (a local run never crosses the wire, so the rate limit cannot bound it) | `AChainThatSendsItsOwnPacket_IsRefusedInsteadOfRecursing` |
| The rails | permission, id grammar, chain and count caps, payload cap, session, role, and the channel's own boundary guards | `ADuplicateDeclaration_IsRefused`, `ADeclarationWithoutAHandler_IsRefused`, `ADeclarationWithAnInvalidId_IsRefused`, `TheDeclarationCap_IsEnforced`, `ANullOrOverCapPayload_IsRefused`, `ASendOutsideASession_IsRefused`, `AModWithoutSendNetworkMessage_CannotDeclareOrSend`, `AModThatDeclaresNoPackets_CannotSendOneItDoesNotOwn`, `DirectedSend_ToANonMember_IsRefused`, `UndeclaredSend_IsRefused`, `PacketIds_AreListedInRegistrationOrder`, `TheChannelItselfRefusesAPacketSendItCannotMake` |
| The rate limit covers packets | the same per-sender bucket as the tunnel form | `PacketFloodOverBurst_IsDroppedLikeTheTunnelForm` |
| Public surface recorded | 39 reviewed lines, all `Experimental` | `ApiSurfaceGateTests` (451/451 with the whole gate project) |
| Docs pair intact | both blocks edited, hashes re-recorded | `DocumentationTreeGateTests` green after `docs/standard/alignment.txt` was re-recorded from the LF-folded blob hashes |

## 4. Verification design

- The behaviour tests run the REAL three-node stack (`TestNode` + `FakeNetwork`: host + two guests over the
  production composition root, transport and Steam replaced by fakes), so routing, the relay and the rate
  limit are exercised through the same code a session uses — not through a unit-test double of the router.
- Every delivery claim is asserted from what actually RAN on each copy (`TestPacketMod.Runs`), including the
  negative half: "the reporter did not run it" is a fact of the run history, not an inference from the code.
  The fixture also records the payload's first byte per run, which is what pins the payload-copy contract.
- The cases are split by behaviour family across three classes (20 / 11 / 11) sharing `ModPacketsWorld`,
  because the per-class xUnit case limit is 40 and a single class would have crossed it.
- The three mutations below were run against the working tree and the file restored after each; every restore
  was verified by reading the restored expressions back, telling the build the file is newer (`LastWriteTime`)
  and re-running all three classes green.

## 5. Verification results

- `dotnet test --filter FullyQualifiedName~ModPackets` → 42/42 (20 routing and policy, 11 failure story, 11
  rails), first run after the split. One case was RED before its fix: the re-entrancy guard's, whose first
  version covered the local send path only, so a received frame's chain could still re-enter its own packet
  once (4 nested-stage rows instead of 2); the guard moved into `RunGuarded`, shared by both entry points.
- Mutation 1 — the relay verdict in `ModPacketsAdapter.Route` flipped to `== HostOnly`: exactly 6 of the 42
  red (`GuestReport_EveryOtherMember_…`, `GuestReport_ReachesTheHostWithTheReportersIdentity_…`,
  `GuestReport_EveryMember_RunsOnTheReporterFirst`, `ARelayedPacket_StillReachesTheOtherMember…`,
  `AHandlersInPlaceWrite_StaysInsideItsOwnDelivery`, `AChainThatSendsItsOwnPacket_IsRefusedInsteadOfRecursing`),
  36 green. Restored → 42/42.
- Mutation 2 — `IsSenderRoleAllowed` forced to `true`: exactly 3 red (`HostOnlyPacket_AGuestCannotStartIt`,
  `HostOnlyPacket_AMembersFrameForItIsRefusedAtTheHost`, `GuestOnlyPacket_TheHostCannotStartIt`), 39 green.
  Restored → 42/42.
- Mutation 3 — the payload snapshot removed from `Route` (the chain handed the frame's own array): exactly 1
  red, `AHandlersInPlaceWrite_StaysInsideItsOwnDelivery` (the other member's first byte became the host's
  rewrite), 41 green. Restored → 42/42.
- The re-entrancy guard is NOT mutation-tested: removing it turns the guard's own case into unbounded
  recursion, which ends the test host with a stack overflow instead of a red assertion — a crash is not a
  usable mutation signal, so the guard's evidence is the case's own red-before-fix above, which the
  independent review accepted as the substitute (recording that it is not reproducible from the frozen tree).
- `CasualtiesUnknownOnline.NormativeGates.Tests` → 451/451 (the API baseline re-recorded from the gate's own
  emitted candidate; the sync-coverage row and its 12 anchors accepted by the gate's own quote check).
- `dotnet test CasualtiesUnknownOnline.slnx` (with a build) → gates 451/451 and behaviour 4755/4755, 0
  failures; `dotnet build` clean and `dotnet format` exit 0 in the same final pass.

## 6. Independent review

An independent adversarial review ran in a fresh context against the FROZEN working tree (protocol + public
mod API + cross-module routing, the FULL tier), reported no blocker, 2 major, 6 minor and 8 nits, and
reproduced the cycle's numbers independently (ModPacketsTests 34/34 at the time, ModMessageTests 13/13,
ModHandshakeProtocolTests 7/7, gates 451/451, the full solution green, the API baseline +39/−0, the JSON
count 1044 with 12 anchors on N8, all 78 alignment hashes, `ModContext.cs` 596 → 151, and that the seven
extracted adapter bodies are textually identical to the HEAD nested classes). Its verdict on the mechanism
itself: every path it attacked — wire form, routing, relay, stage order, refusal story, rails, extraction —
matched its documentation.

Every finding was fixed in this same commit:

- **major 1** — two documented delivery cells (`SendToHost` (host) × `EveryMember`, `SendToPeer` (host) ×
  `EveryMember`) had no case, and the local-refusal path was unpinned: three cases added, and the two
  refusals that were deletable without a red are now red without them.
- **major 2** — `IModPacketContext.Payload` promised that a handler's write cannot change the relayed frame
  while the code relayed the same array: the code now snapshots the payload for the chain (both entry
  points), the promise is pinned by `AHandlersInPlaceWrite_StaysInsideItsOwnDelivery`, and mutation 3 above
  shows the case is load-bearing.
- **minor 3** — the self-check's `ModMessageTests` count was 16, measured 13.
- **minor 4** — the mutation counts described a 33-case suite: all three mutations were re-run against the
  42-case suite and their measured reds/greens are recorded above.
- **minor 5** — the null-payload fix was half: `ModMessageHandler`'s log line and `ModChannel.CheckLength`
  are null-safe now, and the "absent member" premise is corrected in §1 row 8 (the wire cannot produce a
  null payload; the guards are the API boundary's own defence).
- **minor 6** — the mod-facing contract never said a relayed delivery's `SenderSteamId` is the host:
  `IModPacketContext` and both `mod-api.md` blocks say it now, next to the example that reads it.
- **minor 7** — the packet id had no receive-side rail: an illegal id is dropped by the registration grammar
  before anything echoes it, and the log names its length (`APacketIdThatIsNotALegalId_IsDroppedByItsLengthAlone`).
- **minor 8** — the membership gate's case covered "no member at all", not "member without the handshake",
  and only the declared form: two cases added, one per form.
- **minor 9** — the local-refusal path: pinned by `ALocalRefusal_StopsTheFrameBeforeItLeaves`.
- **minor 10** — the self-check promised a full-suite line that did not exist (it is in §5 now) and asserted
  the review's outcome before the review existed (this section).
- **nits** — the two `HostOnly` axes are named as different axes in both blocks; `/exampleecho` works on the
  host too (it broadcasts, since the declaration excludes the sender); the tunnel-vs-packet `SendToHost`
  asymmetry and the local-run rate-limit budget are stated in the contract; the new glossary terms are
  linked at first use; `Unregister` logs its refusal like every other call; the version-discipline sentence
  no longer reads as "this mechanism changed no wire"; and the mis-named rails case is renamed
  (`AModThatDeclaresNoPackets_CannotSendOneItDoesNotOwn`).

## 7. Limits

1. **No real-client run.** The two-client rows need two clients in a session with the example mods deployed;
   the deploy scripts do not ship `CasualtiesUnknownOnline.ModExample.dll`, so the batch copies it by hand.
   Named in the ticket's *Limits* for the agent's next acceptance batch.
2. **Both consumers are in-tree and neither is executed by a test.** Two independent `[CuoMod]` declarations
   with different policies and chains, but one example assembly, no third-party mod, and the test project
   does not reference that assembly — so their correctness today is the compiler's plus the acceptance
   batch's. That is why the surface stays `Experimental`.
3. **A relayed frame's sender is the host.** A guest cannot tell a relayed report from a host-originated
   frame, so the sender policy is not re-judged there; a mod that needs the original sender puts it in its
   own payload. Now stated in the contract rather than only in this record.
4. **The rate limit stays per sender**, shared by every mod and both forms: a flooding mod can starve
   another mod's packets from the same peer. Unchanged from the tunnel form, and out of this cycle's scope.
5. **`Refuse` after `Validate` is a logged no-op** — by the time a later stage runs the packet is applied, and
   the contract says so rather than pretending the refusal can be undone.
6. **No per-packet rate limit or payload cap.** The framework's single bucket and one cap apply to every
   packet of every mod; a per-packet rail is not built because nothing asked for it yet.
7. **The re-entrancy guard is per packet id, per copy.** It refuses any delivery that re-enters an id this
   copy is already running, so every cycle — including A → B → A — is caught; a finite chain of distinct
   packets is left alone and bounded only by the 64-packet declaration cap.
