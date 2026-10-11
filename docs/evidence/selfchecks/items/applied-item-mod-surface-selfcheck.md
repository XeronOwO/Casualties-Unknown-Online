# The mod surface for an applied item — stage 3 self-check (2026-10-14)

Ticket: `docs/backlog/review/item-identity-across-transforms.md`. Stage 1's page is
`docs/evidence/selfchecks/items/applied-item-location-selfcheck.md` and stage 2's is
`docs/evidence/selfchecks/items/applied-item-round-trip-selfcheck.md`; the design record the ruling
rests on is `docs/evidence/selfchecks/items/item-identity-across-transforms-design.md`. **Scope of this
cycle: stage 3 of the ruled option 1 — the mod-facing half.** The rule that stage 2 made true in a
running game ("an item applied to a limb keeps its instance id") is now written where a mod author
reads it, and the read the ruling promises — "a mod that recorded the id still finds it" — exists:
`IModGameState.TryGetItem`. No adapter, kernel or wire behaviour moves: this cycle only gives the fact
a documented reader.

Citations: `reversing/` paths carry line numbers (that tree is never edited); our own `src/` and
`docs/` are cited by path plus quoted text.

## 1. What landed

| Artifact | Change |
|---|---|
| `IModItemState` (new) | The item fact a mod reads by id: instance id, item id, `Location`, owner, limb, container, position, condition, the item's own favourite flag. `Experimental`. |
| `ModItemLocation` (new) | The five families the kernel's own `ItemLocationKind` names, one by one: `World`, `Carried`, `Contained`, `Applied`, `Terminal`. `Experimental`. |
| `IModGameState` | `TryGetItem(ulong instanceId, out IModItemState item)` — the by-id read, `Experimental` on a `Stable` interface, beside `TryGetPlayer` as stage 1's §2.3 settled. |
| `IKernelItemFacts` | Its doc now names its second consumer (the mod projection) instead of the admission seam alone; the interface itself is unchanged — one member, `FindItem`. |
| `ModGameStateAdapter` | The read, and the one-by-one mapping of the kernel's family onto the mod contract — a switch expression with a throwing default, NOT an ordinal cast. |
| `ModContext` / `ModLifecycle` / `ModService` | The read port threaded to the adapter through the composition root; no new DI registration (the port already existed). |
| `IModContext` | The `GameState` member's doc says what the surface now carries. |
| `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` | The read, and a new "What an item's id means across a transform" subsection: the four-row lifecycle table and its four rules. |
| `docs/en/how-to/read-game-state.md` + `docs/zh/how-to/read-game-state.md` | A "Follow one item by its id" section (the `Location` table, the two careful answers), the two reads' different sources, two new traps and the runtime check. |
| `docs/standard/terminology.txt` + both glossaries | Two words registered before their first human-facing use: `instance id` (实例编号) and `applied item` (已装物品). |
| `docs/contracts/abstractions-api-baseline.txt` | The reviewed addition: 18 added lines and no changed line, byte-identical to the gate's emitted candidate. |
| `docs/decisions/active.md` | Decision 259 carries the stage-3 half: what the mod surface answers, the two answers that stay apart, and why the read needs no wire member. |
| `tests/.../Mods/ModItemStateTests.cs` (new) | 8 cases, all through the production composition root. |
| `docs/backlog/` | The ticket moves `todo/` → `review/` with its index row. |

## 2. The four decisions this stage had to make

Stage 1's §2.3 settled *where* the read lives (`IModGameState`, the same permission gate and the same
point-in-time shape as `TryGetPlayer`) and that an applied item is NOT an inventory entry. Four things
were still open, and each is answered from the tree rather than from taste:

**2.1 The read's SOURCE is the kernel, and that is what makes it work on every role.** The surface's
other half reads the 1 Hz character stream; an applied item is on no such snapshot, and the item row is
the authority for which item is installed (`LimbItemLocationRules`' own doc). The kernel is also the one
place every client holds: the host owns the rows, every committed batch is broadcast to every handshaken
guest (`KernelProtocolService.BroadcastCommittedBatch` → `SendToGuests`) and applied to the guest's
mirror (`HandleCommittedBatch` → `_batches.Apply`), and a joiner gets the whole table as a checkpoint. So
the read answers on the host, on the member that performed the apply, and on a third member that did
nothing — which is the case a mod watching somebody else actually needs, and
`AThirdMember_ReadsAnItemAnotherMemberApplied` pins both of the last two in one case (the bystander's
mirror and the applier's own kernel). It also means **no wire member was added**: the fact already
travels.

**2.2 The answer mirrors the kernel's ONE-SHAPE location, and the slot is deliberately absent.** The
kernel's `ItemLocation` documents the rule this contract follows — "A field a family does not use carries
its neutral value, so the record stays one shape for every family" — so `IModItemState` carries every
family's field and `Location` is what says which of them mean something. What is NOT carried is the
inventory slot: the kernel's rows keep `ItemData.SlotIndex` across a relocation (`LimbItemLocationRules`
commits `command.Data ?? item.Value.Data`), so an applied row still holds the slot it had while it was
carried — and reporting it would tell a mod exactly what the fifth family exists to deny, that the
player still carries the splint. A carried item's slot stays where it belongs, in
`IModPlayerInventory`. The mapping from the kernel's family to the contract's is a switch expression,
not an ordinal cast, so a sixth family is a compile-time gap that fails loudly at the read
(`ArgumentOutOfRangeException`) instead of silently reading as whichever member shares its number. One
exception to the neutral-value shape is inherited rather than chosen and is documented rather than
hidden: `ParentItemId` is meaningful for a `World` item too, because a world container's contents are
recorded as world items bound to their container (`ItemProjection.ApplyDrop` builds
`ItemLocation.World(pos.X, pos.Y, parentItemId)`), so the member is written as "the container this item
belongs to" and the pages say a non-zero parent is not the same fact as being contained.

**2.3 An ENDED item answers `true` with `Terminal`, not `false`.** This is the distinction the whole
ticket is about: a mod that recorded an id must be able to tell "that item ended" from "I never saw that
id", and a `false` for both would collapse exactly the two answers the ruling separated. The kernel
keeps the row — "Terminal records stay too: a destroyed item is never resurrected, and removing its
tombstone would let a stale spawn report re-create it" (`ItemDomainModule`) — and an id is never
re-issued, so a terminal answer is final. `AnItemThatEnded_ReadsAsTerminal_RatherThanAsUnknown` pins both
halves, and `0` (the "not yet allocated" value `IModInventoryEntry.InstanceId` documents) is a `false`.
A `false` has a third cause the contract states rather than leaves to be discovered: a client whose
kernel holds no item facts yet — a guest before the join checkpoint — answers `false` for every id, which
is the second reason a reading is never a promise.

**2.4 The new surface is `Experimental`, not `Stable`.** `docs/en/reference/modification-policy.md`:
"A new surface starts here; the promotion funnel below is how it becomes `Stable`." The shape here
follows a model that landed two cycles ago and has no real-session evidence yet (stage 2's §5), which is
the definition of a surface that is documented and usable and allowed to move. The permission is
unchanged (`ReadGameState`) and is re-checked inside the call, like every other read on this surface.

## 3. The lifecycle rule, and where it is written

The ticket's remaining promise was "a written lifecycle rule". It is now one subsection in
`docs/en/reference/mod-api.md` (and its Chinese twin), shaped as four rows and six rules:

| What happens to the item | What the id does |
|---|---|
| picked up, dropped, worn, moved into a container, handed to another player | unchanged; `Location` says where it went |
| applied to a limb as a splint, a carcass splint or a tourniquet | unchanged; `Location` is `Applied` on that player's limb, and it is not an inventory entry |
| taken back off that limb | unchanged; `Location` is `Carried` by whoever took it off |
| consumed, destroyed, cooked or crafted away | `Location` becomes `Terminal`, and the id is finished |

The rules are the ones the design record's §4/§6.6/§6.7 already established, now stated for a mod author:
only those three definitions keep the id across a transform because the game is what decides it (the
component that turns back into an item); the OTHER limb tools do not transform the item at all — the
ice pack, the bandage, the analgesic gauze and the suture only spend its condition, which is the design
record's own distinction between "no path back" and "the item's fate"; cooking and crafting are the
transforms that really end the source row and mint a new id; `Terminal` is not "unknown"; a `false` means
this client holds no such fact; and an applied item's condition is the framework's record, which the 1 Hz
capture keeps in step with the component and can therefore trail by at most a step. The how-to page
carries the same facts as a task ("Follow one item by its id"), and the two new words are registered in
`docs/standard/terminology.txt` — `applied item` before its first human-facing use, and `instance id`
registered now, its Chinese rendering brought to one spelling across both pages.

## 4. Mechanism inventory

Mechanisms ADDED, each with the reader that makes it not a guess:

| Mechanism | Its reader |
|---|---|
| `IModGameState.TryGetItem` | the mod author the ruling promised the read to; documented in `mod-api.md` and the how-to page, pinned by 8 cases |
| `IModItemState` / `ModItemLocation` | the same read's return shape; the baseline records them at `Experimental` |
| the kernel-family → contract-family mapping | the read itself; its default arm is the alarm for a family the contract has not been taught |

Mechanisms DELETED or changed: none. The one doc-level change is `IKernelItemFacts`' description, which
now names both of its readers instead of only the admission seam. No DI registration was added: the port
existed and `KernelReplicationComposition` already registers it.

## 5. What this cycle does NOT prove

- **No two-client run has read one of these.** The end-to-end halves run in the simulation harness
  through the production composition root — a host commit read back on the host, a guest's commit read on
  the applier itself and on a third member, the applied row read on the limb and again in the operator's
  hands — but no real game process has observed any of it. The ticket carries no acceptance row and moves
  to `review/`.
- **The client-side transform is still unproven, and this stage does not change that.** Stage 2's §5
  stands unaltered: the whole of `LimbItemTransform` and its three patch bodies take `Item`/`Limb`/
  `GameObject` and have no case that can run without the game. What is proven here is the other end —
  given an applied row, what a mod reads. The row itself is created by the machinery stage 2 exercised
  through the wire.
- **The read is only as fresh as the client's kernel.** On a guest that is the mirror each committed
  batch and the join checkpoint fill in, so between a commit and its arrival the guest answers with the
  previous location. That is the same ordering every other kernel read on that client has, and it is the
  read model's own rule: a reading is never a verdict.
- **The read does not restrict WHICH ids a mod may ask about.** The kernel holds every member's items,
  and `ReadGameState` is the only gate — the exposure the player read already has. Nothing here decides
  that a mod may not read an item it has no relationship with; if that is ever wanted it is a permission
  or ownership rule, and inventing one here would have been a second, undocumented gate.
- **Everything stage 2 left open is still open**: the applied row's condition step (0.01), the local
  take-off's one-round-trip race, the in-flight container report that can claim an applied item
  (`todo/id-carriers-can-disagree-about-placement.md`), the wire checkpoint's all-or-nothing failure for a
  limb-less applied row, and the peer clone that converges from the 1 Hz snapshot instead of an event.
  None of them is touched by a read.
- **"An applied item is not in anyone's `IModPlayerInventory`" is inferred, not directly pinned.** The 8
  cases read `TryGetItem`; none of them reads `TryGetPlayer(...).Inventory`, because the harness feeds
  that projection by injecting character snapshots and would only assert what the test itself injected.
  What stands behind the claim is one hop of production code and one hop of game behaviour: the
  projection is built from the wire character snapshots, and the native apply has already destroyed the
  carried `Item`, so there is nothing left for a capture to report. Stage 2 pins the neighbouring fact
  (`MedicalToolApplicationTests` asserts the owner's saved character has no items after the apply).
- **The doc rule has no gate.** The four rows are prose in two pages and two glossaries; no test fails if
  a future cycle changes the model without them. What is gated is the contract they describe: the
  baseline entry, and the 8 cases.
- **No protocol number moved**, deliberately: `ProtocolVersion.Current` is frozen at `UnreleasedBaseline`
  before the first release (decision 241), and this cycle adds no wire member at all.

## 6. Verification as run

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ModItemStateTests"` —
  8 passed.
- Full: `dotnet test CasualtiesUnknownOnline.slnx` green at gates 615/615 and behaviour 5028/5028 on the
  frozen tree (the cycle adds 8 cases to the 5020 it started from), plus `dotnet format` clean. One
  caveat so the figures reproduce at any point in the cycle: the 615th gate is
  `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, which is the checklist satisfying
  itself, so a run taken while its boxes are still open reports 614 of those 615 — that gate goes green
  when the checklist is complete, and the final run before the commit has it green.
- The one gate this cycle had to move with the code: `ApiSurfaceGateTests` — 18 added lines and no
  changed line, taken from the gate's own emitted candidate and verified byte-identical to it
  (`git diff --no-index docs/contracts/abstractions-api-baseline.txt
  artifacts/api-surface/abstractions-api-baseline.txt` reports no difference; the line count is
  `git diff --numstat`, which reads `18 0`). The two documentation gates that moved with the pages are
  `DocumentationTreeGateTests`' pair record (`docs/standard/alignment.txt`, three pairs re-hashed) and
  `SelfcheckManifestGateTests` (this page's manifest row).
- Measured after the last edit (`(Get-Content <path>).Count`): `ModGameStateAdapter` 182 lines,
  `ModLifecycle` 403, `ModContext` 155, `ModService` 127, `IModItemState` 64, `ModItemLocation` 51,
  `ModItemStateTests` 202 — all well under the 600-line architecture gate, and no new bool of state
  anywhere.
