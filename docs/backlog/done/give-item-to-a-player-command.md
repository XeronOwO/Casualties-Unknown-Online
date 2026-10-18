# The `/give` command: create one item in a player's inventory

- Status: Done — all twelve acceptance rows pass. Batch `20261018-q` judged rows 1, 2 (the liquid give, the
  row that rejected the previous build), 3, 6, 7, 8, 10 and 12 on `0.1.0+30dbb252`; batch `20261018-r`
  judged rows 4, 5 and 11 on `0.1.0+510b9cd1`, the build that landed the destination ladder, and its
  continuation `20261018-r2` judged row 9. The records are
  `docs/evidence/acceptance/give-item-to-a-player-command-20261018-q.md` and
  `docs/evidence/acceptance/give-item-to-a-player-command-20261018-r.md`. Split out of
  `done/item-and-entity-data-commands.md`'s stage C, which this ticket completes; the umbrella keeps stage D
  (`/summon`, landed 2026-10-18 and recorded in `done/item-and-entity-data-commands.md`).
- Priority: Medium-High
- Category: Command surface / item and entity state / cheat verb
- Source: stage C of `done/item-and-entity-data-commands.md` (designed 2026-10-18 in that ticket) and the
  user's combined 2026-10-18 ruling recorded in `review/cheat-permission-and-op-commands.md` — `/give` ships
  for the host AND the members, with the two-sided cheat check. The implementation cycle's directions (the
  carrier, the narrow id port, the definition fact port, the property reuse) were frozen by the session
  handoff and are restated under *The design, as landed*.
- Related: `review/cheat-permission-and-op-commands.md` (the permission half of the same cycle),
  `review/give-item-to-another-player.md` (the gesture that shares the custody carrier and its open gap),
  `done/item-and-entity-data-commands.md` (the umbrella whose stage C this is; stage D landed there too),
  `done/item-data-write-commands.md` (the property grammar and the local-scene port this reuses),
  `review/item-data-read-commands.md` (the row the answer is read back through),
  `docs/architecture/member-report-validation.md` (the census row for the member's request),
  `docs/evidence/sync-coverage-matrix.md` row I11

## What was asked

Put ONE item into a player's inventory with its properties already set, from the console. The item is created
— nothing is taken from anyone — so the verb owns both the definition lookup and the delivery, and a member's
verb is a request because only the host owns the definition table, the prefab capture and the instance-id
space.

## The syntax decision (2026-10-17, decision 264)

The verb's syntax is **the item id with its state attached as SNBT** — the same language the data model uses,
the shape the game's own save carries, and the shape Minecraft's item argument has:

```text
/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}],Condition:0.8}
/give @p bandage
/give @p largebattery{Condition:0.5,Components:{BatteryItem:{charge:0.9,on:true}}}
```

The `condition` / `favourited` / `liquid` / `component` token vocabulary the verb shipped with is
**superseded** (decision 264) and must not grow: it could not express nesting, several liquids, contents or a
second field of the same component, and it forced the Runtime to know which properties exist. Under the new
syntax:

- **One language across the family**: `/data get` prints this same SNBT, `/data merge` and `/data modify` take
  it, so an operator reads an item back and edits what they read.
- **The Runtime** owns the SNBT parse, the path resolution and the sentence the operator reads. It judges
  SHAPE only: malformed SNBT, an unknown top-level key, a value that is not the kind the key takes.
- **The adapter** owns the SCHEMA and the legality: which keys this definition's state carries (the item's
  own fields, the component type names and their fields, whether it is a liquid container). A give is refused
  BY NAME before anything is created when the state names a key the definition does not carry — and no game
  rule (capacity, container rules) and no definition constant enters the Runtime or the wire.
- **Completion is part of the ticket**: the console's suggestion machinery completes INSIDE the SNBT — the
  top-level keys, the component type names of the item at hand, their fields, and the enumerated values — from
  the adapter's schema for the item or definition being edited, never from a hand-kept list in the console.
- **The parser, the writer, the path resolver and the completion are their own ticket**
  (`done/snbt-state-language.md`, landed by the implementing cycle); this ticket carries the give half: the
  item id, the attached state, the schema query it needs, the delivery and the answer.

The acceptance rows below are re-derived in the SNBT form by `done/snbt-state-language.md` (row 2 becomes
`/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}]}`, and an unknown key or a state the definition
does not carry is a named refusal).

## Entry mapping

The console verb has no native call site: the game has no give-to-player command, and this is CUO's own
surface by the user's backlog request (2026-10-07) and the combined ruling (2026-10-18, quoted in
`review/cheat-permission-and-op-commands.md`) — its scope is that request's, never an inherited gesture's.
Two native facts are reused rather than re-invented: the item is captured from the prefab exactly as the
game's own grant instantiates it (`Utils.Create`, the call the acceptance `item-provide` recipe reproduces),
and the delivery is the landed cross-player push, whose receiving client runs the game's own placement path.
The destination is an ordered ladder ruled by the user on 2026-10-17 (body slot → a carried container with
weight headroom → the contents of a worn container → a drop at the player's position), so the "where does it
go" answer is always the operator's and is always named in the answer; nothing is silently substituted and
no rung is skipped without being the answer's own sentence.

## The design, as landed

- **The carrier is the landed push, unchanged.** `PlayerInventoryTakeService.DeliverCreatedItem` is the tail of
  the cross-player transfer core with no source body: it clones the destination's character snapshot, appends the
  created fact, saves it, records the recipient's ledger entry (`AdoptTransferredItem`, or
  `CommitCarriedToHost` for the host itself) and publishes ONE `PlayerInventoryTransferMsg` whose receiving
  client materializes the item through `PlayerInteractionApply.OnPlayerInventoryTransfer`. The published
  transfer names NO source (`FromSteamId = 0`), so no client removes anything and only the recipient's
  projection fires.
- **The instance id is the adapter's, through a narrow port.** `IItemGrantSource.TryCreate(definitionId, ...)`
  answers a fresh save-shaped fact with an id from the ONE `ItemIdAllocator` the rest of the adapter allocates
  through (`AllocateId`). A second counter would mint ids the watermark, the snapshots and the archive never
  saw; the port is the only way the Runtime can ask for "a fact for this definition, with an id this host owns".
- **The definition is validated where it exists.** The adapter's `ItemGrantSource` reads
  `Item.GlobalItems` and resolves the prefab (`ItemPrefabResolver`), refusing by name an id the game does not
  know or a prefab that cannot load — before the item is created, rather than after the receiver's failed
  instantiation.
- **The properties are the item's own state, applied before delivery — SUPERSEDED syntax, decision 264.**
  The shipped token grammar (`condition`, `favourited`, `liquid`, `component`) is replaced by the item id with
  its SNBT state attached; the mechanics stand: the host creates the fact from its OWN capture of the
  definition, applies the state, and only then delivers, so a state the definition does not carry refuses the
  whole give before anything exists (the merge's atomicity rule, one step earlier). See *The syntax decision*.
- **The destination is a LADDER, ruled by the user on 2026-10-17**: (1) a free slot on the player's own
  body; (2) a container the body carries that has enough remaining weight capacity; (3) the equipment
  inside a container the body WEARS; (4) a drop at the player's own position. A selector that names several
  bodies is still refused by name (the family's one-item rule), and a named slot stays the operator's
  preference: an occupied or out-of-range named slot falls back to the first free one with the substitution
  logged, and the answer names where the item really landed. **The ladder replaces the earlier "body slots
  only / no free slot is a refusal" design** (and the container destination is no longer deferred to the
  give gesture ticket): a give to a full body is answered by the next rung, and the refusal by name is kept
  for the cases that are genuinely impossible (an unknown definition, a selector that names no body, a
  destination the item cannot be placed into at all — for example a container whose own rules refuse the
  item).
- **A member's verb is a request, and its answer prints where it typed.** `PlayerInventoryGiveRequestMsg` (147)
  carries the destination, the definition, the slot and the item state as canonical SNBT; the host re-parses and re-binds
  them to its own capture, so a member cannot hand over a state the definition does not carry.
  `PlayerInventoryGiveResultMsg` (148) carries the host's own answer sentence, built from the same reason
  wording the host logs, and the member's console prints it (`CommandConsoleService` subscribes to
  `IPlayerInteractionControl.GiveAnswered`).
- **The permission is the landed two-sided check, not a second gate.** The acting client runs `CheatVerbGate`
  (no round trip on an announced closed switch or roster); the host judges its own switch and roster for every
  request that arrives (`IMemberCommandRules`, switch then roster, each refusal its own sentence). The host's
  own verb needs neither — its standing is the online session — exactly as `/data`'s write verbs. The verb does
  NOT additionally consult `AllowRemoteInventoryTake`: that rule is the cross-player GESTURE family's policy
  (the drag/take affordances), while a console cheat verb's permission is the host's switch and roster — the
  user's own ruling for the family. A host that wants nobody to hand items out keeps the switch shut or grants
  nobody.

## Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The creation port | `IItemGrantSource` (`Runtime/GameAdapter`), implemented by the adapter's `ItemGrantSource` as a STANDALONE adapter service (not a port on the `GameAdapter` singleton — the interaction service consumes it, and a port there is the constructor cycle *The startup cycle* below records); registered in `GameAdapterComposition` over the container's one `ItemIdAllocator`, and defaulted to `UnavailableItemGrantSource` in the base root |
| 2 | The id space | `ItemGrantSource` calls `ItemIdAllocator.AllocateId()` — the same allocator `TraderRecruitCoordinator` uses for its stock gifts, so host-side ids stay in one counter |
| 3 | The definition facts | `Item.GlobalItems` and `ItemPrefabResolver.Load` are read only in the adapter (the game-assembly boundary); a missing definition or prefab is a named refusal |
| 4 | The state language | `SnbtParser`/`SnbtWriter` for the text, `IItemStateLanguage` for the schema and the adapter's `ItemStateDocument.TryBind` for the legality: one language, one refusal wording, one bind (decision 264; `done/snbt-state-language.md`) |
| 5 | The delivery | `PlayerInventoryTakeService.DeliverCreatedItem` shares `PlayerCharacterAccess.CloneCharacter`, `ResolveDestinationSlot`, the ledger calls and `PublishTransfer` with `HandleTakeCore`; the receiver is the landed `PlayerInteractionApply.OnPlayerInventoryTransfer` |
| 6 | The request/answer wire | `NetMsg.PlayerInventoryGiveRequest` 147 (guest → host) and `NetMsg.PlayerInventoryGiveResult` 148 (host → requester), both reliable direct frames — declared in sync-coverage row I11 and in both protocol reference blocks |
| 7 | The front end | `CheatVerbGate` in `ItemGiveCommands`, before the target is resolved (the family's order) |
| 8 | The back end | `PlayerItemGiveService.HandleRequest`: `CheatVerbsOpenToMembers` then `AllowsCheatVerb`; each refusal is sent back as the answer |
| 9 | The console answer | the host's command returns the host's sentence; the member's answer arrives through `PlayerInventoryGiveResultHandler` → `FireGiveAnswered` → the console's subscription |
| 10 | No protocol bump (a fact, not a design input) | decision 241: pre-release the number is a frozen baseline; this change adds two `NetMsg` members and no existing member moves |

## Self-check table

| # | Claim | Evidence |
|---|---|---|
| 1 | The host creates, writes the recipient's snapshot and the ledger, and the row reaches the member's kernel | `CommandConsoleItemGiveTests.Host_GivesToAGuest_WritesTheSnapshotTheLedgerAndTheKernelRow` |
| 2 | The host's own body is a valid destination | `.Host_GivesToItself_WritesItsOwnSnapshot` |
| 3 | A named slot is used when free, and an occupied one falls back with the answer naming the real slot | `.Host_GivesInTheNamedSlot`, `.Host_GivesIntoAnOccupiedSlot_FallsBackAndSaysWhereItWent` |
| 4 | A full body answers the ladder's next rung, never a refusal: a carried container with room takes the item, else a worn one, else the world drop — and the answer names the rung | `.Host_GivesToAFullBodyWithACarriedContainer_PlacesItInsideAndSaysSo`, `.Host_GivesToAWornContainer_CarriedCandidatesAreOfferedFirst`, `.Host_GivesToATargetWithNoRoom_DropsAtItsPositionAndSaysSo` |
| 5 | The state is bound BEFORE delivery | `.Host_GivesWithAProperty_AppliesItBeforeDelivery`, `.Host_GivesWithALiquidAndAComponent_AppliesBoth` |
| 6 | A state the definition does not carry refuses the whole give | `.Host_GivesWithAStateTheDefinitionDoesNotCarry_IsRefusedByTheCreationPort` |
| 7 | An unknown definition is refused by name and nothing is created | `.Host_GivesADefinitionTheBuildDoesNotKnow_IsRefusedByName` |
| 8 | A target the host cannot place the item on is refused by name | `.Host_GivesToATargetOutsideTheWorld_IsRefusedByName` |
| 9 | Several bodies are refused, by name, with the narrowing advice | `.Host_GivesWhenTheSelectorNamesTwoBodies_IsRefusedByName` |
| 10 | The grammar refuses the item-target words, a missing definition and a bad slot, and quotes its own usage | `ItemGiveParserTests` (19 cases), `.Host_GivesWithAStateTheDefinitionDoesNotCarry_IsRefusedByTheCreationPort`, `.Member_GivesWithMalformedSnbt_IsRefusedLocallyWithoutARequest` |
| 11 | A member's verb lands, and the host's answer is what prints | `CommandConsoleMemberItemGiveTests.Member_GivesToTheHost_LandsAndTheHostsAnswerPrints`, `.Member_GivesToItself_LandsOnItsOwnSnapshot`, `.Member_GivesWithState_IsAppliedByTheHost` |
| 12 | The front end refuses an announced closed switch and an announced roster that does not name the member, before anything is sent | `.Member_Gives_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally`, `.Member_Gives_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster` |
| 13 | With nothing announced the host judges and answers, and the refusal prints | `.Member_Gives_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal` |
| 14 | A member's unknown definition is answered by the host, not created locally | `.Member_GivesAnUnknownDefinition_IsAnsweredByTheHost` |
| 15 | The two new frames are direction-classified and the member-authored one is in the report census | `GuestToHostDirectionTests`/`HostToGuestDirectionTests` rows, `docs/architecture/member-report-validation.md`, sync-coverage row I11 with 5 anchors |
| 16 | The ladder's rungs are ordered as ruled, and the container rungs are answered by the game's own capacity rule | `FakeItemContainerFit.Offered` pins the carried-then-worn offer order; `ItemContainerFit` mirrors `Container.CanHoldItem` (per-item limit, total limit, tags) plus `Awake`'s +0.001 and `LoadItem`'s container-stacking refusal |
| 17 | The drop rung is wired end to end: the host sends the destination, the receiving client creates the world object with the host's id and REPORTS the creation | `SourceShapeGateTests.GiveLadderDrop_IsWiredFromTheTransferToTheSpawnReport`, `.GiveLadderContainerRefusal_FallsToTheDropOnlyForACreatedItem`; the live half is acceptance row 11 |
| 18 | The destination survives the kernel wire and the transfer message | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesDropDestinationOnTransferEvent`, `.WireBatchRoundTrip_PreservesContainerMoveParentOnTransferEvent` |
| 19 | The container seam is standalone, like the creation seam, so it cannot reintroduce the startup cycle | `AdapterCapabilityPortShapeTests.GiveContainerFitSeam_IsStandalone` |
| 20 | Not covered here | the live two-client run (the rows below); the real prefab capture, the real capacity values and the receiving client's drop report are the adapter's, and a test host has no game assembly |

## Whole-family audit

The console's cheat family now reads: `/data`'s write verbs (gated, landed), `/data get` (`Anyone` by the read
half's own rule), `/give` (this ticket — gated on both sides), `/summon` (stage D, landed 2026-10-18), the host-admin
verbs (`HostOnly`), and `/heal` (excluded because it consumes the healer's own item). The give verb reuses the
write verbs' grammar and the same `CheatVerbGate` instance in the console, and it inherits the same two host
rulings — a new verb added no new flag, which is the family's own rule. The delivery family was checked too:
the take core, the remote-backpack hand-over and the give all go through the one custody core, so the three
cannot drift about who owns an item.

## What the acceptance batch owes

A host + member session on the deployed artifact:

**Batch `20261017-q` judged these rows on the deployed `bb2350e7`**: rows 1 and 3-10 pass, **row 2 fails** (a
liquid give throws in the prefab capture — `ItemStateCodec.CaptureLiquids` reads a null stack on the prefab
template); the record is `docs/evidence/acceptance/give-item-to-a-player-command-20261017-q.md`. The ticket
moved back to `todo/` with its status field carrying the rejection, and the fix cycle owns: the row-2 defect,
the final-pass review's findings, and the user's 2026-10-17 destination ladder.

**Batch `20261018-q` re-judged the re-derived SNBT rows on the deployed `30dbb252`** (record
`docs/evidence/acceptance/give-item-to-a-player-command-20261018-q.md`): rows 1, 2, 3, 6, 7, 8, 10 and 12
pass. Row 2's liquid give answers with `Liquids:[{LiquidId:"water",Amount:500f}]` and the member reads `500f`
back, and the receiving client's own scene tree carries the three given items with their instance ids — the
previous record's "no local-scene probe" limit is closed. The run also read the slot substitution (`slot 9`
→ `slot 2`, named in the log before the give line) and a container's condition following its fill ratio
(a state naming 400 ml on a 500 ml bottle answers `Condition:0.8f`, the adapter's own rule). Rows 4, 5 and 11
are the ladder's and stay unproven until it lands; row 9 was not re-run.

**Batches `20261018-r` and `20261018-r2` judged the remaining rows on the deployed `510b9cd1`** (record
`docs/evidence/acceptance/give-item-to-a-player-command-20261018-r.md`): rows 4, 5, 11 and 9 pass. Row 4's
answer named the carried `plasticbag` and the member's own scene tree held the item inside it; row 5's answer
printed on the member's console for its own verb; row 11 dropped the item and BOTH peers read the world row
with the same id and state, because the receiving client created AND reported it; row 9's stale-roster
request reached the host and its own refusal printed on the member's console.

1. Host: `/give @p bandage` — the answer names a new instance id and slot; `/data get id <id>` on BOTH peers
   reads the same document, owned by the member.
2. Host: `/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}]}` — the answer carries the state, and
   `/data get id <id> Liquids[0].Amount` reads `500f` on the member's side (a fresh empty container is
   filled by the state).
3. Host: `/give @p bandage slot 9` (beyond the six-slot body) — the answer names the slot it really used, not
   9; `/data get` agrees.
4. The member fills its own body through the game's own grants, then the host `/give`s — the ladder's next
   rung answers: a container the member carries with weight headroom takes the item, and the answer names
   that container; if none can, the item drops at the member's position and the answer says so.
5. A granted member whose own body is full runs `/give @s bandage` — the same ladder, driven from the
   member's own console: the answer prints on the member's console (the answer path, not only the host's
   log) and names where the item really went.
6. Member, switch open + grant: `/give @p bandage` — the answer prints on the member's console, and the host's
   `/data get` reads the new row.
7. Member, switch closed (announced): the same verb is refused on the member's own console, and the host's
   record is unchanged.
8. Member, switch open, no grant (announced): refused by the roster's sentence, not the switch's.
9. Member with the roster announcement swallowed: the request reaches the host, the host's roster refusal is
   the answer, printed on the member's console.
10. A definition the game does not know (`/give @p notanitem`): the named refusal, nothing created on either
    side.
11. The ladder's last rung: a body with no free slot, no carried container with headroom and no worn
    container — the item is DROPPED at the player's own position, the answer says so, and both peers read
    the item where it landed.
12. `/give @p bandage{Quality:5}` (an unknown top-level key) and
    `/give @p bandage{Liquids:[{LiquidId:"water",Amount:500}]}` on a definition with no liquid container —
    each is refused BY NAME by the adapter and nothing is created.

## Limits, recorded rather than hidden

- **The receiving client's own slot rules do not decide placement — but its LIVE container is the final
  judge.** The host picks the destination rung from the recipient's last snapshot and the receiving client
  applies it (a body slot, a container's contents, or the world drop the last rung names). A live change the
  snapshot cannot see is the window this leaves: if the container the host named refuses the item (its live
  contents grew, or its own rules refuse it), a GIVE answers with the ladder's drop rung there and reports
  that, instead of a silent different slot; the answer already said "inside" and the operator reads the truth
  back with `/data get`. A source transfer keeps its slot fallback. No rung is skipped silently.
- **The capacity prediction is the adapter's, from definition facts.** `Container.CanHoldItem`'s three
  clauses are mirrored plus `Awake`'s +0.001 adjustment and `LoadItem`'s container-stacking refusal; the
  current holding weight is summed from the snapshot's contents' definition weights. A container nested
  inside another is never a candidate (the game's own `LoadItem` refuses such a destination). The rule has
  no test host — the acceptance rows 4 and 5 are its runtime proof.
- **A dropped item is a world row, not a carried one.** The receiving client creates the object with the
  host's instance id and reports the creation through the item domain, so the host's kernel and both peers
  learn it; until that report arrives the operator's `/data get` answers from the old record. The drop
  lands at the recipient's own position and time — the host's answer says where, not when.
- **A named slot in a full body is not logged as a substitution.** The substitution line is written only when
  a free fallback slot exists; with none, the ladder's own answer carries the real destination.
- **The ladder is the ruled design, not a fallback.** A named slot is the operator's preference only: an
  occupied or out-of-range named slot falls back to the first free one and the answer names the real slot;
  the rungs below a full body are the user's 2026-10-17 ruling, and no rung is silent.
- **A prefab's captured contents are the definition's own business.** The created fact carries what the
  prefab's capture carries; a container definition whose prefab already holds children would materialize them
  on the receiver, and their ids are not minted here (the same boundary the trader stock gifts have).
- **A component field only exists if the prefab capture carries it.** A battery granted straight from its
  prefab carries no `BatteryItem` component line, so `/give largebattery component …` is refused until a
  capture carries that component — the same finding the write verbs' acceptance recorded. The ticket's
  earlier reason ("the item builds one when it is loaded") is wrong: the game's `Item.Awake` CACHES
  `GetComponent<BatteryItem>()` and nothing adds one, so a prefab that carried the component would be
  captured; the live prefabs' contents are the asset's own fact and a run reads them, not this paragraph.
- **No definition completion.** The verb declares one free-text argument; the console's resource-location
  catalog is not wired into it, exactly as the read half recorded.
- **A lost answer costs the confirmation, not the item.** The answer is one reliable directed frame with no
  re-send; the operator reads the result back with `/data get`, and the created row converges through the
  ordinary character snapshot and kernel checkpoint (sync-coverage row I11).
- **The verb needs a session.** A solo world has no cheat standing for either role: the host's permission is
  the online switch and a member's is a session grant (`CheatVerbGate`'s no-session refusal).
- **The same refusal prints with two different console kinds.** The host's own `/give` returns its sentence
  and the console prints every command return as a success line, while a member's answer arrives through
  the answer event and prints as an error line. The WORDS are identical; the kind is a console-wide return
  contract (the landed `/data` writes have the same shape), not this verb's to change.
- **The created item is a grant, not a native spawn.** No local object is instantiated on the giver's client
  and no item-domain report fires there; the fact is built from the prefab and the receiver materializes it
  through the ordinary transfer path (the trader gift's shape).

## Adversarial review of the frozen change

Independent, fresh context, read-only over the frozen tree, following `docs/development/review-prompt.md`
(FULL tier). The review was stopped by the user before its final pass, after an interim report over the
frozen tree: **0 blocker, 0 major, 7 minor/nit**, all fixed in the same commit except the one recorded
above. Its "could not check" list (the runtime-only facts and the two-client delivery) is the acceptance
batch's rows below.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | minor | The member did not parse the property words locally although `ItemGiveRequest`'s doc said it did, so a malformed line always cost a round trip | fixed: `ItemGiveCommands` runs `ItemGiveParser.TryParseProperties` before the gate/send; the member and host cases pin it (the member case asserts no request left the client) |
| 2 | minor | The self-check table said `ItemGiveParserTests` carries 12 cases; it carries 14 | fixed |
| 3 | minor | The same refusal showed with different console kinds (host success line vs member error line) | not fixed, recorded as a limit: the console prints every command return as success; changing the kind with the verdict is console-wide |
| 4 | nit | The slot-substitution line was logged under `[Take]` on the give path | fixed: the resolver takes the caller's tag (`[Give]` from the give path, `[Take]` unchanged) |
| 5 | nit | The ticket said the answer is "the same sentence it logs"; only the REASON wording is shared | fixed in the ticket and the message/service docs |
| 6 | nit | The ticket's landing date (2026-10-17) contradicted the inherited design date (2026-10-18) it quotes | fixed: the status carries no landing date |
| 7 | nit | The umbrella ticket lost its UTF-8 BOM and the rewritten lines fixed mojibake while 53 occurrences remain | BOM restored; the remaining mojibake is the file's repo-wide pre-existing encoding artifact, left to the encoding cleanup rather than half-rewritten here |

The review also noted, and this commit fixes, a live-doc drift it did not have to find: `current.md`, the
internals adapter page and `guards.md` still described the adapter composition as twelve ports (the tree
then had seventeen, one of them this change's; the give seam has since moved OFF the aggregate — see
*The startup cycle* below), and the guard page also stated the old pinned member count.

## The final-pass review (2026-10-17, after the interim report)

Independent, fresh context, read-only over the COMMITTED `7b10e6ca` (no build, no tests, no edits); the pass
the interim review had not reached. **1 blocker, 0 major, 5 minor, 2 nit.** The blocker is the startup cycle
recorded in the next section (found by the same run and fixed in `bb2350e7`). The rest, and their disposition
in the follow-up cycle this ticket now goes into:

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | blocker | `IItemGrantSource` registered from the adapter singleton while `PlayerInteractionService` consumes it: the container can never be built, so CUO never starts | **fixed** in `bb2350e7` (*The startup cycle* below) |
| 2 | minor | Two "fixed" claims in the interim review's table are false in the committed code: (a) the give path does not pass the `[Give]` tag to the slot resolver, (b) the message/service docs still say the answer is exactly the host's log sentence | to fix: pass `[Give]` on the give path; correct both docs to say the REASON wording is shared |
| 3 | minor | The give answer frame is honoured from ANY sender (the handler drops `sender`; `FireAnswered` checks only the local role), where the same cycle's `HostRulesHandler`/`CheatPermissionsHandler` refuse a non-host sender by name | to fix: the host-id check on the answer handler, and the family sweep (`InventoryIntentRefusedHandler` carries the same pre-existing gap) |
| 4 | minor | The delivery checklist's structure box was checked with evidence contradicting its own "<= 600 lines" criterion, and two owner counts do not reproduce (239/125/86/48/73 against the recorded 240/125/77/48/73) | the checklist was reset by the follow-up cycle and re-filled from measured numbers — see its own file |
| 5 | minor | The battery limit's REASON was wrong: the game's `Item.Awake` caches `GetComponent<BatteryItem>()` and nothing adds one, so a prefab-carried component WOULD be captured | the limit's reason is corrected in this ticket; the live prefab contents are the acceptance run's reading |
| 6 | minor | The "body filled after the host's snapshot" limit understates the loss: the operator is told `Gave …`, the guest refuses the placement, and the member's next 1 Hz report overwrites the host's row — the item is lost on both ends | superseded by the ladder (the later rungs exist exactly for that case); the limit now states the loss the old design allowed |
| 7 | nit | The new port used `AddSingleton` where the file's defaulted seams use `services.Replace(...)` | **fixed** in `bb2350e7` (`Replace` + the standalone seam) |
| 8 | nit | The Entry mapping claimed "nothing is supplied by a fallback" while the slot fallback exists | corrected with the ladder: the substitution is named in the answer and the ladder is the ruled order, not a hidden fallback |

The review's "could not check" list is the acceptance batch's rows (the live session, the real prefab
contents, and whether a co-member can address another guest directly at all) — the batch's own record carries
them.

## The defect fixes this ticket reopens for (2026-10-17, the rejected-row cycle)

The acceptance batch's row 2 and the final-pass review's findings are fixed in one cycle before the ladder:

1. **The row-2 defect: a liquid give threw.** `ItemStateCodec.CaptureLiquids` read `WaterContainerItem.stack`
   and called `.Select` on it; a PREFAB template's list is null until the game's own load fills it, and
   `/give` captures from the prefab, so every liquid-carrying definition threw
   (`System.ArgumentNullException: Value cannot be null. Parameter name: source`, batch evidence
   `d33-give-exception.log`). The capture now treats a null list as "no liquid yet", which is the honest
   reading of an uninstantiated template; a bandage never hit it because it carries no
   `WaterContainerItem` at all.
2. **The `[Give]` tag (review #2a).** `DeliverCreatedItem` now passes `"[Give]"` to the slot resolver, so the
   substitution line the give path writes is no longer logged under `[Take]`.
3. **The answer docs (review #2b).** `PlayerInventoryGiveResultMsg` and `SendAnswer` now say what is true:
   the answer shares the host's REASON wording, and it is not the log sentence (the log carries the actor and
   the verb prefix).
4. **The answer's sender anchor (review #3).** `PlayerInventoryGiveResultHandler` refuses a frame whose sender
   is not this session's host, by name, before it reaches the console — and the family sweep added the same
   check to `InventoryIntentRefusedHandler`, which carried the pre-existing gap. The red is pinned:
   `CommandConsoleMemberItemGiveTests.AnAnswerFromANonHostSender_IsDropped` failed with the check
   neutralised (the forged answer printed) and passes with it (`.acceptance/20261017-q/red-sender-check.log`
   and `green-sender-check.log`).

What this cycle does NOT do: the destination ladder. The ladder is the next unit — it changes the delivery
path (rungs 2-4), the answer wording, the rows 4/5/11 and the slot resolver's contract, and it deserves its
own cycle rather than half of this one.

## Several liquids in one container (the user's question, 2026-10-17)

The question: does the property design consider a container holding more than one liquid? Read against the
game's own container and our write path, the honest answer is **read yes, write no**:

- **The game's model is multi-liquid with a capacity.** `WaterContainerItem.stack` is a
  `List<LiquidStack>`; `AddLiquid` appends a NEW stack for a liquid id it does not hold
  (`this.stack.Add(new LiquidStack(liquidId, num))`), tops up an existing one, clamps to
  `SpaceLeft = Capacity - CurrentTotal`, and `Capacity` is `LiquidItemInfo.capacity`. `Drain` drops stacks
  under 0.5 ml. So the game itself allows water + another liquid in one container, up to one shared capacity.
- **Our READ is multi-liquid already**: `CharacterItemMsg.Liquids` carries every stack, the codec captures
  the list, and `/data get` prints one `liquid <id> <ml>` line per stack — so a multi-liquid container reads
  correctly and round-trips through the snapshot/archive.
- **Our WRITE is not**: `ItemDataWriteValues.ApplyLiquid` looks up ONE existing stack by liquid id and sets
  its amount; it cannot append a stack, cannot fill a container that captured none (which is why the batch's
  row 2 refused — the prefab capture has no stacks at all), and it does not consult the capacity, so a set
  can exceed what the container holds. One `liquid` token edits one existing stack; a second liquid cannot be
  introduced by `/give` or `/data set`.
- **Why a prefab capture is empty even for a container the game fills**: `LiquidItemInfo.autoFill` (default
  true) is the game's own "fill it for me" flag, applied when the item is loaded/instantiated — and `/give`
  captures the PREFAB, which is not an instantiated item, so the capture legitimately has no stacks.

**The design (the user's follow-ups, 2026-10-17 — implemented by `done/snbt-state-language.md`: the state language writes the stack list, so an empty container can be filled and a second liquid added).** A liquid
write means "the amount of liquid X in this container": a liquid the item does not hold is APPENDED (an empty
container can be filled, a second liquid can sit beside the first), 0 drops the stack, and the container's own
capacity decides what fits — over capacity refused by name. `/give` and `/data set` share it.

The first two sketches of this were rejected, and the reason each was wrong is the part to remember:

1. "Carry the capacity on the item fact" put a DEFINITION constant into instance state — a second source of
   truth on every wire item. Rejected on sight ("什么傻逼设计").
2. "Ask the adapter for the capacity, judge in the Runtime" moved the number to the right owner but left the
   RULE in the wrong layer: it would still be the Runtime enforcing a game rule, while the adapter's
   `RestoreLiquids` rebuilds the stack verbatim (`water.stack = [..]`, deliberately — the game's own SAVE
   restores a container's stacks the same way), so a game rule enforced on one side and ignored on the other
   is worse than not enforcing it. ("你是一点不懂设计啊?")

The layer-correct shape, which is what this ticket now proposes: **the Runtime carries the INTENT and owns
only shape/word errors (a negative amount, a malformed token); whether the item can hold liquids at all, and
how much fits, is the ADAPTER's answer, because the adapter is the only layer with the live item or the
prefab in hand** — `WaterContainerItem`'s presence is the container fact, its `Capacity` is the limit, and
`AddLiquid`/`Drain` are the game's own operations. Concretely: for `/data set` the local write seam answers
the liquid part (it has the live item); for `/give` the creation seam answers it (it has the prefab), before
the fact exists, so a refusal is answered before anything is delivered. No wire change, no capacity field, no
new Runtime knowledge of a game rule. That also makes multi-liquid fall out of the game's own model instead
of a second implementation of it.

**The re-run's finding (2026-10-17 22:04, artifact `0.1.0+02bc1f6a`) — an OPEN QUESTION for the user.** The
exception is gone and the give answers, but row 2's 500 ml is still not reached: `/give @p waterbottle liquid
water 500` creates the waterbottle and then refuses with `carries no 'water' — its liquids are: none (an item
with no liquid container reads the same way)`. That is the SHARED grammar's own rule
(`ItemDataWriteValues.ApplyLiquid` sets an EXISTING stack's amount; the capture cannot tell "no container"
from "empty container"), not a new give defect: a fresh empty container cannot be filled by the property
vocabulary. Whether it should be able to — a container flag in the save shape plus an add-a-stack semantic,
shared with `/data set` — is a user ruling this ticket does not take on its own; the row's expectation is
re-derived once that ruling is in.

## The destination ladder (the user's ruling, 2026-10-17)

The user's direction, in its own words: "give 命令的设计,应当是优先玩家身上的空位,其次是身上有足够重量余量的
容器,然后是身上穿的容器里装备,最后是掉落在用户所在坐标". It supersedes this ticket's "the destination is a
body slot only" design AND the give gesture ticket's ownership of the container destination:

1. a free slot on the player's own body;
2. a container the body carries that has enough remaining weight capacity;
3. the equipment inside a container the body wears;
4. a drop at the player's own position.

The verb's answer names the rung that settled it, so the operator always reads where the item really went.
The ladder is the acceptance rows 4, 5 and 11's expectation (the old "no free slot is a named refusal" is
gone — a full body is simply rung 2's question).

## The ladder, as landed (2026-10-18)

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The rung order | `PlayerInventoryTakeService.DeliverCreatedItem`: the named-or-first free slot, then ONE `IItemContainerFit` call over the carried candidates followed by the worn ones, then `Drop` |
| 2 | The capacity answer | `IItemContainerFit` (Runtime port) / `ItemContainerFit` (adapter): `Container.CanHoldItem`'s per-item, total and tag clauses, `Awake`'s +0.001 adjustment, `LoadItem`'s container-stacking refusal, and the item table's own weights |
| 3 | The destination's wire | `PlayerInventoryDestination` on the kernel event and command, `WirePlayerInteraction` (ProtoMember 26) and `PlayerInventoryTransferMsg`; the receive side reads only a defined value |
| 4 | The container application | the landed `TargetParentItemId` branch; a give whose LIVE container refuses falls to the drop instead of a silent slot swap (`PlayerInventoryPlacement.AddCarriedItemToLocalContainer`) |
| 5 | The drop | `PlayerInventoryPlacement.DropCreatedItemAtLocalBody`: create at the body, stamp the host's id, apply the state, then `IItemControl.SendItemSpawned` — the report IS the row |
| 6 | The answer | `PlayerItemGiveService.Where`: the slot, the carried-or-worn container (definition and id), or the drop; the same helper feeds the log |
| 7 | The receiving client's load check | `ItemStateCodec.RestoreContent` verifies `Container.LoadItem` landed (`transform.parent`) instead of returning a child the container refused |
| 8 | No protocol bump (a fact, not a design input) | one field on an existing event; no `NetMsg` member added |

**What this section does not do.** The ladder is implemented and unit-pinned; rows 4, 5 and 11 of *What the
acceptance batch owes* are its runtime proof and are the next batch's job.

## The startup cycle the batch found (2026-10-17)

The first acceptance run against this commit never reached its first row: `7b10e6ca` deploys and then the
plugin dies in `Awake`. The client's own log (`BepInEx/LogOutput.log`, 2026-10-17 21:24) carries it:

```text
[Error  :Casualties Unknown: Online] CUO composition root validation failed: System.InvalidOperationException: A circular dependency was detected during CUO service resolution: CasualtiesUnknownOnline.Runtime.GameAdapter.IGameIntegrationLifecycle -> CasualtiesUnknownOnline.GameAdapter.GameAdapter -> CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl -> CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.PlayerInteractionService -> CasualtiesUnknownOnline.Runtime.GameAdapter.IItemGrantSource -> CasualtiesUnknownOnline.GameAdapter.GameAdapter
[Error  :Casualties Unknown: Online] CUO startup failed: System.InvalidOperationException: A circular dependency was detected during CUO service resolution: <the same chain, verbatim>
```

Both lines are quoted from `.acceptance/20261017-q/run1-startup-failure.log`, copied out of the client's
`LogOutput.log`; the second repeats the first line's chain exactly and is abbreviated here only in that
repeated part.

**The defect.** This change registered the new port the way every other port is registered —
`AddSingleton<IItemGrantSource>(p => p.GetRequiredService<GameAdapter>())` — but `IItemGrantSource` is
consumed by `PlayerInteractionService`, and `GameAdapter` itself depends on `IPlayerInteractionControl`
(it subscribes to the transfer/carry/heal/use events). Two edges, one cycle: resolving the lifecycle port
builds the adapter, which builds the interaction service, which asks for the give port, which asks for the
adapter that is still being built. The composition-root validation refuses that at startup and CUO never
runs — every unit and integration test still passed, because the tests compose the Runtime root with a
fake give source and never build the real adapter.

**The fix.** The give seam never needed the aggregate: it reads the game's own item table and prefab
resolver (statics) and allocates from the ONE id counter. So `ItemGrantSource` is registered as a
STANDALONE adapter service over the container's `ItemIdAllocator` (the allocator moved out of the
adapter's domain graph into the composition, so both sides share the one counter rather than minting a
second), `IItemGrantSource` left the `IGameAdapter` composition (sixteen ports now), and the shape gate
pins both facts — the standalone registration, the `Replace` that wires it over the Runtime's default, and
the one allocator — with the cycle named in the pin, so a future change that routes it back through the
singleton fails a test instead of the startup. The adapter's own `IPlayerInteractionVisibility` forwarder,
dead for the same reason (the container resolves the standalone oracle), was deleted with it.

**What the batch records about it.** The batch's own prelude gates on the client's `ping`, so the failure
stopped it before the first row: no row of this ticket was judged against `7b10e6ca`. The batch re-runs
against the fix's commit once it lands, and that run's record — plus the lesson it folds into
`docs/acceptance/lessons.md` (a green suite is not a start, because the acceptance prelude is the only thing
that composes the real adapter) — is what closes this paragraph. Until then this states the plan, not a
completed run.

## Adversarial review of the ladder (2026-10-18, before the commit)

Independent, fresh context, read-only over the frozen working tree, following `docs/development/review-prompt.md`
(FULL tier), with the game's own code and the landed take path as ground truth. **Verdict: not approvable as
frozen — 3 blocker, 4 major, 5 minor, 5 nit.** Every finding and its disposition is below; all are fixed in
the same commit unless the disposition says otherwise.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | blocker | The drop rung created the world item with the host's id and never reported it: `ItemWorldSync.OnItemInstantiated` skips an item that already carries `ItemInstanceId` (`"Skipped", "AlreadySynced"`), and the host's kernel therefore never got a row — a phantom the world-item keyframe kills (or, with an empty table, an object nobody knows). The test pinned the defect. | fixed: the placement calls `IItemControl.SendItemSpawned` with the created item's capture after stamping the id; `SourceShapeGateTests.GiveLadderDrop_IsWiredFromTheTransferToTheSpawnReport` pins the wiring, and the drop row's kernel row is now the receiving client's report (acceptance row 11 is its runtime proof) |
| F2 | blocker | `FullyQualifiedNameGateTests` failed on `System.StringComparison.Ordinal` in the new adapter file. | fixed: `using System;` and the plain name |
| F3 | blocker | `SourceShapeGateTests` failed: `PlayerInteractionApply` grew to 645 aggregate lines. | fixed: the placement half (`RemoveCarriedItemFromLocalBody`, the container/body placement, the same-owner re-home, the drop) moved to `PlayerInventoryPlacement`; the file is 484 lines and the collaborator 243 |
| F4 | major | When the LIVE container refused, the receiving client silently fell back to a body slot (or discarded the item), while the answer and two message docs claimed the placement agreed; `RestoreContent` returned success even when `LoadItem` refused. | fixed: a give answers that refusal with the drop rung and reports it (`AddCarriedItemToLocalContainer`'s `dropWhenRefused`), `RestoreContent` verifies the load landed and destroys the refused child, both docs and the ticket's Limits state the live-change window |
| F5 | major | The new standalone seam had no shape pin, so a future change could route it through the aggregate and reintroduce the startup cycle the give seam already tripped. | fixed: `AdapterCapabilityPortShapeTests.GiveContainerFitSeam_IsStandalone` pins zero `AddSingleton<IItemContainerFit>`, one `Replace`, and the standalone registration |
| F6 | major | The contract records the change falsified were not updated: sync-coverage P7 and I11, the evidence quote, and the ticket's self-check row that named a deleted test. | fixed: P7 and I11 updated, the I11 evidence quote re-pointed, the self-check table rebuilt with the ladder rows and the index line updated |
| F7 | major | The tests pin the Runtime's order and the answer, not the adapter's rule or the receiving apply; two mutations (drop the per-item guard, drop the apply call) survived the suite. | partly fixed: the wiring is pinned by the two source-shape cases above; the capacity arithmetic stays acceptance-proven (rows 4/5) because the behaviour project cannot host the game assembly — recorded in the Limits |
| F8 | minor | Rungs 2 and 3 shared one enum value and one sentence, so the answer never said which rung settled it. | fixed: the answer and the log name "the carried container" or "the worn container" (the container's own slot sign decides) |
| F9 | minor | The drop sentence asserted "no free slot and no container with room" as a live fact, false in a build with the unavailable default. | fixed: it says "dropped at its position (the ladder's last rung)"; the fall-through detail is in the log |
| F10 | minor | The prediction mirrored `CanHoldItem` but not `LoadItem`'s container-stacking refusal, so an item that is itself a loaded container was predicted to fit. | fixed: a fact carrying children is never predicted into a container, with the game's own refusal cited |
| F11 | minor | `PlayerInventoryGiveResultMsg.SlotIndex`'s doc was wrong for both new rungs. | fixed: it now states the container's own slot and the drop's -1 |
| F12 | minor | The world-creation path duplicated the materialization and sat outside the item domain's carrier set. | fixed with F1: one materialization site (`PlayerInventoryPlacement`), and the report goes through the item domain's own spawn path |
| F13 | nit | `Where` and `Describe` duplicated the same clauses. | fixed: one `Where` helper feeds both |
| F14 | nit | The wire field's cast was unchecked. | fixed: an undefined value reads as the body-slot default, documented on the field |
| F15 | nit | A defensive `throw` in a console/network path. | fixed: a missing container in the clone answers the drop rung with a warning |
| F16 | nit | The pages listing the adapter's standalone seams did not name the new one. | fixed: `docs/architecture/current.md` and both language blocks' `internals/adapter-and-updates.md` |
| F17 | nit | The delivery doc claimed the slot substitution is always logged. | fixed: logged when a free fallback exists; the answer carries the real destination |

The review's "could not check" list — the live container's prefab values, `Item.Start` frame ordering, the
keyframe actually killing a drop, the two-client rows, the custom-template `Awake` question, `ItemInfo.SetTags`
population and the live wire delivery — is exactly the next acceptance batch's scope.
