# Mod-defined wire packets and custom handling chains

- Status: Review — the development half landed 2026-10-07 (see *What landed*); the runtime rows need two
  real clients and are named in *Limits* item 1. The attribute inventory this ticket's first job asked for
  is *The attribute inventory* below, written before the design was fixed.
- Priority: High
- Category: Mod platform / protocol extensibility
- Source: the user's 2026-10-07 backlog request — the protocol must be extensible by mods: a mod has to be
  able to define its own behaviour packets and its own handling chain, instead of only riding CUO's
  built-in messages. This promotes the "custom replication domains" entry of
  `todo/mod-content-ceiling.md` (Part 3, section C) out of the memo.
- Related: `todo/mod-content-ceiling.md` (the entry this promotes and the platform direction it sits in),
  `review/mod-crafting-quality-labels.md` (the sibling stage-1 cycle of the same promotion funnel),
  `future/wire-surface-baseline.md` (the wire surface is recorded beside the protocol version),
  `docs/en/reference/protocol-messages.md`, `docs/decisions/active.md` (decision 243 — this cycle's design
  record — and decision 241 for the protocol-number policy)

## What is asked

A mod ships a behaviour of its own (a machine, a vehicle, a minigame) and needs two things:

1. **A packet of its own** — a message type it defines, sent and received on the session's own transport,
   without CUO having to know the message. The requirement is a mod-owned identity for the message and a
   mod-owned payload, carried by the same reliability, ordering and peer set the built-in messages use.
2. **A handling chain of its own** — the mod decides who may send the message, who applies it, and in
   which order its handlers run relative to CUO's own, instead of a single global callback.

The user's words put the goal beyond "mods may piggyback on one generic blob": the mechanism must leave room
for open-ended custom behaviour, not only for the shapes CUO anticipated.

## The attribute inventory (2026-10-07, before any design)

Read: `IModNetwork`, `ModMessageMsg`, `ModChannel`, `ModLifecycle`, `ModMessageHandler`,
`NetMessageRegistry`, `ModRateLimitPolicy`, `ModStatusTransport`.

| # | Question | Answer |
|---|---|---|
| 1 | What can a mod send today? | Three calls — `IModNetwork.SendToHost(payload)` (guest → the host's copy of that mod), `SendToPeer(steamId, payload)` and `Broadcast(payload)` (host only; the broadcast also fires the host's own copy locally). All three ride ONE wire kind, `NetMsg.ModMessage` (75), whose payload is `ModMessageMsg { ModId, Payload }` |
| 2 | What identity does that traffic carry? | The MOD id only — the manifest id from `[CuoMod]`, carried as `ModMessageMsg.ModId`. The frame has no message-type identity at all: `Payload` is opaque to CUO, so a mod with several message types demultiplexes them inside its own bytes, and the framework can neither route nor log nor refuse by message type |
| 3 | Where would CUO slice a mod-owned id into the frame? | In `ModMessageMsg` itself: the frame already carries the mod id, so the packet id belongs beside it as one more member of the same message (`ModMessageMsg.PacketId`). Nothing else in the path needs to move — `ModMessageHandler` hands the decoded message to the mod domain, and the router already has both the mod id and the sender. A second `NetMsg` id for the same frame would duplicate the handler, the rate limiter and the cap for one wire kind |
| 4 | What is the handling chain today? | One event, `IModNetwork.MessageReceived` (`(senderSteamId, payload)`), fired to every subscriber in subscription order by `ModLifecycle.OnModMessageReceived`. There is no per-message handler, no declared order and no way for the mod to say who may send it |
| 5 | What is missing for the ticket's two asks? | The message identity (2), a declared policy the framework can enforce (who may send, who applies), an ordered chain per message, and a relay the framework owns — today "no auto-relay" means every mod writes the report-and-fan-out dance by hand, which is the one thing a single callback cannot give it |
| 6 | Which framework code already depends on the tunnel form? | `ModStatusTransport` publishes `ModStatusUpdate` frames through `IModNetwork.Broadcast` — a first-party consumer of the packet-less form that is not going away |
| 7 | What does the receive path already gate? | A per-sender token bucket (20/s, burst 40), the 64 KiB payload cap (checked at both ends), the mod lookup, and the `SendNetworkMessage` declaration. It does NOT gate membership: the host-command path checks `member.Handshaken` (`ModCommandService`) and the mod-message path never did |
| 8 | Failures on the current path | A throwing `MessageReceived` subscriber is isolated by the mod domain's own `SafeRun`; a null payload in `ModMessageMsg` reached `msg.Payload.Length` and threw inside the handler (fixed in the same cycle as item 3 of the failure story); a frame from a peer that never handshook reached a mod's callback |

The inventory's two consequences for the design: the packet id is a member of the EXISTING frame (item 3)
rather than a new wire kind, and the packet-less form stays (item 6) as the anonymous tunnel.

## Design (frozen before implementation)

Recorded as decision 243 (`docs/decisions/active.md`); the summary:

1. **Identity** — a mod-owned packet id rides `ModMessageMsg.PacketId` beside the mod id. Empty means the
   anonymous tunnel form (the framework's own status transport and the example echo use it); a non-empty id
   the receiving copy has not declared is dropped with a log, never guessed at.
2. **Declaration** — `ModPacket(id, sender, delivery, handlers…)`: who may send it
   (`ModPacketSender.AnyMember` / `GuestOnly` / `HostOnly`), which copies run its chain
   (`ModPacketDelivery.HostOnly` / `EveryOtherMember` / `EveryMember`) and the chain itself.
3. **Chain** — handlers are declared in stages and run in the framework's fixed stage order
   (`Validate` → `Apply` → `Observe`), keeping the declared order inside one stage. `Validate` runs before
   the framework relays or applies and is the only stage that may refuse; the first refusal ends the
   delivery, so the host relays nothing.
4. **Routing and relay** — CUO owns the fan-out the ticket's non-goal puts on its side: the host runs its
   own chain and then relays a non-`HostOnly` packet to every OTHER member (`BroadcastExcept`, the reporter
   excluded). `EveryMember` additionally runs the sender's own copy first, and a local refusal stops the
   frame before it leaves.
5. **Who may send, and where it is judged** — at the sender before the frame leaves, and at the host when a
   member's frame arrives. A guest never re-judges an inbound frame: every frame it hears comes from the
   host, which judged the report before relaying it.
6. **Failure story** — a handler that throws is isolated and logged with the mod id, the packet id and the
   stage, and the chain continues; an undeclared packet id is dropped with a log; a frame from a peer with
   no completed handshake reaches no chain (the gate the host-command path already applied, now applied to
   both mod-message forms); a chain that retires its own declaration mid-run still finishes it; and a
   delivery this copy is already running is refused, because a chain that starts its own packet again would
   recurse until the stack ends the process — the one failure the per-sender rate limit cannot bound, since
   a local run never crosses the wire.
7. **Transient by contract** — CUO buffers, replays and persists no packet; the mod owns late-join
   re-sending (`PlayerJoined`) or `IModState` / `IModData`.
8. **Permission and stability** — `SendNetworkMessage` gates declaring, sending and receiving (a declared
   packet IS the network surface, so a new bit would add a handshake field without adding a gate); the
   surface is `Experimental`, the promotion funnel's step for a new surface.
9. **Version** — the frame change is a wire change and takes no number: pre-release the version is the
   frozen baseline (decision 241), so it is recorded by its commit and its ticket.
10. **The payload belongs to one delivery** — the chain works on its own copy of the payload, so a handler's
   in-place write reaches that chain's later handlers and neither the frame the host relays nor the frame a
   local run's send carries. Tightened in the review round: the first cut relayed the very array the
   handlers were given, which contradicted what `IModPacketContext.Payload` promised.

## Required work

1. Attribute first — done, see *The attribute inventory*.
2. Decide the identity and the chain shape with the protocol-number policy in hand — done, see *Design*
   and decision 243.
3. Give the mechanism a failure story — done: undeclared id, throwing handler, retired declaration, a
   self-starting chain and a non-member sender each have a row in *Verification*, and none of them can wedge
   the receive path.
4. Verify with two real mods, not one — two in-tree consumers (`cuo.example`'s declared echo and
   `cuo.example.machine`'s host-authoritative machine) declare different ids, policies and chains. Their
   independence is what *Limits* item 2 qualifies.

## Non-goals

- Not a general serialization framework: a mod owns its payload bytes; CUO owns the envelope, the routing
  and the refusal story.
- Not a compatibility promise to KrokMP's wire format — that is
  `future/krokmp-compatibility-adapter.md`, and compatibility is not a design input here.
- Not a second protocol version or a second frame kind: the packet id is a member of the existing mod frame,
  and the mod's own version is what identifies a packet's shape (as it already does for every mod
  behaviour).

## What landed (2026-10-07 cycle)

| Surface | Change |
|---|---|
| Wire | `ModMessageMsg.PacketId` (protobuf member 3, empty = the anonymous tunnel form); `NetMsg.ModMessage`'s log comment restated |
| Public mod API | `IModPackets`, `IModPacketContext`, `ModPacket`, `ModPacketHandler`, `ModPacketStage`, `ModPacketSender`, `ModPacketDelivery`, and `IModContext.Packets` — all `Experimental`, 39 reviewed lines in `docs/contracts/abstractions-api-baseline.txt` |
| Runtime | `ModPacketsAdapter` (per-mod registry, send surface, chain runner, the payload snapshot a delivery's chain works on, the re-entrancy guard), `ModPacketPolicy` (id grammar, chain and count caps, payload rail), `ModPacketRoute` (the router's verdict), `ModChannel`'s four packet sends and its null-safe length rail, `ModLifecycle`'s packet routing + relay + packet-id rail + the membership gate, `ModMessageHandler`'s null-safe log line |
| Extraction | `ModContext.cs` was 596 lines against the 600-line hard gate, so its per-mod adapters moved out into their own files (`ModNetworkAdapter`, `ModContentAdapter`, `ModUiAdapter`, `ModEntitySpawnAdapter`, `ModItemSpawnAdapter`, `ModGameStateAdapter`, `ModNativeApiAdapter`) with no behaviour change |
| Consumers | `ExampleMod` declares `example.echo` (`AnyMember` + `EveryOtherMember`) beside its tunnel echo and registers `/exampleecho`; `ExampleMachineMod` (`cuo.example.machine`) declares `machine.use` (`GuestOnly` + `HostOnly`) and `machine.state` (`HostOnly` + `EveryMember`) and registers `/machineuse` |
| Docs | `mod-api.md` both blocks (the context row, the *Declared packets* section, the version-discipline and verification lines), `protocol-messages.md` both blocks (row 75), the term registry (declared packet, packet chain, anonymous tunnel) and both glossaries, the sync-coverage N8 row and its two new anchors, decision 243 |

## Verification (machine, this cycle)

- Three classes over the real three-node star (host + two guests, the production stack over `FakeNetwork`),
  sharing one world helper because the per-class xUnit case limit is 40: `ModPacketsTests` (20 — the delivery
  matrix cell by cell, the sender policy at both ends, the relay and its reporter exclusion, stage grouping
  and within-stage order, the payload-copy contract, a local refusal stopping the frame, the flood drop),
  `ModPacketsFailureTests` (11 — a throwing handler, an undeclared id, an illegal id, a peer that is no
  member, a member that never handshook on both message forms, a retired declaration, a chain that starts its
  own packet) and `ModPacketsRailsTests` (11 — the declaration and send rails plus the channel's own
  boundary guards).
- `ModHandshakeProtocolTests` — 2 new cases: the packet id round-trips, and a frame without one decodes to
  the empty tunnel form.
- Mutation checks, each restored afterwards (measured against the 42-case suite): flipping the relay
  condition in `Route` reddens exactly 6; making `IsSenderRoleAllowed` always true reddens exactly 3;
  removing the payload snapshot reddens exactly 1 (`AHandlersInPlaceWrite_StaysInsideItsOwnDelivery`).
- `CasualtiesUnknownOnline.NormativeGates.Tests` — 451/451 green, the API baseline re-recorded from the
  gate's own emitted candidate.
- The independent adversarial review (fresh context, frozen tree, FULL tier) reported no blocker, 2 major, 6
  minor and 8 nits and reproduced the numbers; every finding was fixed in this commit (self-check §6).

## Limits

1. **No real-client run yet.** The two-client rows (a guest's `/machineuse` reaching the host, the host's
   `machine.state` reaching every member, `/exampleecho` on both sides) need two clients in a session with
   the example mods deployed — the deploy scripts do not ship `CasualtiesUnknownOnline.ModExample.dll`, so
   the batch copies it by hand. Named for the agent's next acceptance batch, not for the user.
2. **The two consumers are in-tree and neither is run by a test.** They are two independent `[CuoMod]`
   declarations with different policies, chains and lifecycles, but they ship in ONE example assembly,
   neither is a third-party mod, and the test project does not reference that assembly — so their
   correctness today is the compiler's plus the acceptance batch's. A third-party consumer is what would
   prove the platform beyond this tree; until then the surface stays `Experimental`.
3. **A relayed frame carries the host as its sender.** The other members cannot tell a relayed report from
   a host-originated frame, which is why a guest never re-judges the sender policy. A mod that needs the
   original sender on a relayed packet puts it in its own payload (the `EveryMember` reporter's own run is
   the only place the reporter's id is visible to its own chain). The contract states this now, in
   `IModPacketContext.SenderSteamId` and both `mod-api.md` blocks.
4. **The rate limit stays per sender**, shared by every mod and both message forms: a flooding mod can still
   starve another mod's packets from the same peer. Unchanged from the tunnel form and out of this ticket's
   scope.
5. **An out-of-stage refusal is a logged no-op.** `IModPacketContext.Refuse` called after `Validate` cannot
   undo the application; the contract says so and the log says so, but a mod that refuses too late gets no
   second chance.
