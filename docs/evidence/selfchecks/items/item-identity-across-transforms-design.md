# Item identity across a transform — design record (2026-10-11)

Ticket: `docs/backlog/todo/item-identity-across-transforms.md`. Cycle scope: research only — no file
under `src/`, `tests/` or `tools/` is touched by this cycle. The record states what the native game
does, what CUO commits today, the three answers CUO could give, and what each costs. **Nothing here is
decided**: the ticket carries the decision draft and the user rules on it.

Citations: `reversing/` paths carry line numbers (that tree is never edited); our own `src/` and `docs/`
are cited by path plus quoted text.

## 1. The question

CUO stamps an instance id on every runtime item and publishes it to mods
(`IModInventoryEntry`: "The framework item-instance id (0 when not yet allocated)"). It never says what
that id means after the item stops being an item. A splint applied to a limb is the clearest case: the
game destroys the carried `Item` and adds a `SplintLimb` component to the `Limb`, and taking it off
instantiates a **new** `Item` that copies `condition` and nothing else. A mod that recorded the id has
no stated answer for "the splint I recorded is now on someone's leg", and neither does CUO's own
favourite-mark ledger, which is keyed by that id.

## 2. The native transform inventory

Every site below was read from the decompiled game. The family is read as "components an item's own
limb action attaches to a `Limb`": under that reading it has exactly four item→component branches, all
in `Item.cs`, and two component→item branches. (An exhaustive search for `AddComponent<` /
`GetOrAddComponent<` over the assembly finds four more attaches that are NOT limb components —
`Painkillers` at `Item.cs:457`, `:840` and `:3650`, `MindwipeScript` at `:1208` and `Liquids.cs:1162`,
`SleepingPills` at `Item.cs:5842` — and they target `limb.body` / `body`, so they are outside the
family and have no path back to an item.)

| # | Branch | Item → component | What the component reads from the item | Item destroyed? | Component → item | What the new item gets |
|---|---|---|---|---|---|---|
| 1 | tourniquet | `Item.cs:400-408` — `AddComponent<TourniquetScript>(limb).condition = item.condition` | `condition` (after `item.condition -= 0.125f`) | yes, in the same delegate (`Object.Destroy(item.gameObject)`) | `TourniquetScript.TakeOff` (`TourniquetScript.cs:25-35`) | `condition` only; the template is the hardcoded `"tourniquet"`; `timeApplied` is LOST |
| 2 | splint | `Item.cs:1479-1491` | `condition`, `item = "splint"`, `conditionLossMinute = 0.015f` | yes, same shape | `SplintLimb.TakeOff` (`SplintLimb.cs:19-26`) | `condition` only; the template comes from the component's own `item` field |
| 3 | carcass splint | `Item.cs:1505-1517` | `condition`, `item = "carcasssplint"`, `conditionLossMinute = 0.036f` | yes, same shape | same as #2 | `condition` only |
| 4 | ice pack | `Item.cs:1629-1639` — `GetOrAddComponent<ChilledLimb>` | **nothing** — the component is seeded with the constants `150f`/`150f` | **no** (`destroyAtZeroCondition = false`, `Item.cs:1626`) | none — `ChilledLimb` has no `TakeOff` and no `OnDismember` | — |

The guards are the item's own data, not a CUO table, and they are **not the same sentence**:
branch 1 is gated on `!limb.isVital && limb != limb.body.limbs[2] && !limb.GetComponent<TourniquetScript>()`
(`Item.cs:402`), branches 2 and 3 on `!limb.isHead && !limb.isVital && !limb.GetComponent<SplintLimb>()`
(`Item.cs:1481`, `:1507`), and branch 4 on `item.condition >= 0.5f` (`Item.cs:1631`). What the three
limb guards have in common is what matters here: a limb carries at most ONE component of each type, and
a refused apply spends nothing. (`limbs[2]`'s identity cannot be settled from the tree — `Body.cs:3745`
declares `public Limb[] limbs;` and the order is prefab serialization, assigned nowhere in the
assembly. `Body.cs:3192` shows the head is `limbs[0]`; a `PlayerCamera.cs:739-755` apply already
returns early on a dismembered limb.)

**The two directions are not symmetric in state.** Item→component copies `condition`; component→item
copies `condition` back and nothing else. `timeApplied` (the tourniquet's elapsed clock, which after
540 s starts damaging muscle, `TourniquetScript.cs:50-64`) lives only on the component, and
`conditionLossMinute` is written by the apply from a constant rather than read from the item.

**Who drives the round trip back.**

- `SplintLimb.TakeOff` has four callers: `PlayerCamera.cs:781-786` (the wound view's "remove splint"
  action, `WoundView.cs:474-479`), `FirstAidCourse.cs:360-364` (tutorial cleanup),
  `SplintLimb.cs:35-44` (the component's own decay when `condition <= 0f`), and
  `SplintLimb.cs:29-32` `OnDismember`.
- `TourniquetScript.TakeOff` has two: `PlayerCamera.cs:772-776` and `TourniquetScript.cs:68-71`
  `OnDismember`.
- `OnDismember` is dispatched by Unity reflection, not by an interface: `Limb.cs:101-103` runs
  `limb.gameObject.SendMessage("OnDismember", null, 1)`. Grepping the whole assembly, **only
  `SplintLimb` and `TourniquetScript` declare it** — that pair is the set the game itself treats as one
  family, and `ChilledLimb` is not in it.
- `Limb.Dismember()` itself is called from `AmputationMinigame.cs:75`, `TraderScript.cs:587`,
  `SpiderHandlerTBE.cs:36` and the debug console.

**Where the native round trip loses the thing outright.**

| Point | Code | Outcome |
|---|---|---|
| splint/tourniquet decay to zero | `SplintLimb.cs:37-41` calls `TakeOff()` at `condition <= 0f` | the new item is born with `condition <= 0f` and `destroyAtZeroCondition = true` (`Item.cs:1477`, `:1503`, `:398`), so `Item.Update` (`Item.cs:157-176`) destroys it on its first update (the branch is additionally gated on the world existing with a recently-enabled chunk renderer, `Item.cs:147`) — **net loss** |
| the tourniquet applied below 0.125 | `Item.cs:405` subtracts with no floor | same net loss on the way back |
| `ChilledLimb` expiring | `ChilledLimb.cs:17-26` | the component is destroyed and no item is produced; the ice pack itself survives (branch 4 never destroys it) |
| the severed limb's wearables | `Limb.cs:104-107` drops the wearables of every limb in `TourniquetScript.GetLowerLimbs(this, null)`, and `TourniquetScript.cs:80-83` puts the limb handed in into that set — so the severed limb's own wearables are dropped too; `Limb.cs:135-138` then runs `Object.Destroy(item2.gameObject)` over whatever is still parented to the severed limb | the drop precedes the destroy, so whether the destroy still reaches anything is NOT established by reading: it depends on `DropWearable` unparenting (`Body.cs:1528`). What IS established is that the severed limb loses its wearables without an item being produced for each. This row is about wearables, not about the components this record is about, and is recorded as OUT of scope |
| death | no code destroys the components. An exhaustive search for `Destroy(this)` over the assembly returns exactly `SplintLimb.cs:25`, `TourniquetScript.cs:34` and `ChilledLimb.cs:24`, and none of the 31 reads of `.alive` (`Body.cs:203-209`) touches a limb component | an installed splint simply stays on the body |
| leaving the run | `PlayerCamera.cs:613-616` / `PreRunScript.cs:264-270` reload the scene | everything goes, and death writes no save |

**What the native save knows.** `SavedItem.cs:4-19` is the whole item record:
`public string id; public float condition; public int slot; public string wearSlot; public bool favourited;`
There is no per-instance field — `id` is the prefab/resource name, and the native tree assigns it in no
place. Limb components are stored in a per-limb array (`SaveInfo.cs:15-24` `limbComponents`,
`SaveSystem.cs:139-149` building it and `:411-425` rebuilding it by limb index), carrying the three
`[JsonProperty]` fields each `SplintLimb.cs:47-59` / `TourniquetScript.cs:95-100` declares. So the
native save **does** tell two splints apart — by limb index, never by identity.

## 3. What CUO commits today

| Entry point | Who reports | Kernel commit | `TerminalKind` | Where the limb component's id would be |
|---|---|---|---|---|
| cross-player apply (wound-view gesture on a remote body) | the AFFECTED client, one frame after the destroy lands (`NativeLimbToolApply` → `SendItemActionOutcome(itemInstanceId, 0f, consumed: true)`) | `PlayerItemActionOutcomeService` → `PlayerItemUseCommit` → `DestroyKernelCarriedItemIfPresent` → `TryDestroy(_session.LocalSteamId, itemId, TerminalKind.Consumed, …)` | `Consumed` | nowhere — `ComponentStateMsg` is `{TypeName, Fields}` |
| local apply (own or directly-local body) | the generic `Item.OnDestroy` hook (`ItemPatches`, which suppresses the craft origin and whatever `ShouldSuppressDestroy` claims — today the craft family and the heater-cook source) → `SendItemDestroyed` | `ItemProjection.ApplyDestroy` / the wire's `Destroyed` | **`Destroyed`** | nowhere |
| removal, cross-player | the host, and only on `Completed` (`OtherMedicalOperationSessionService`: a cancel or a timeout leaves the splint on) | `OtherMedicalRemovalApplier.GrantRemovedItemToOperator` → `TrySpawnCarried` / `AdoptTransferredItem` under a **minted** id | new id, never terminal | `condition` and the definition (the splint's own `item` field, or the hardcoded `"tourniquet"`); the tourniquet's `timeApplied` is read nowhere else in `src/` and is dropped |
| removal, local (the player's own body, no remote medical view open) | nobody reports it — the native `TakeOff` runs un-intercepted, because `RemoteMedicalPatches` blocks `PlayerCamera`'s wound-item path only while `RemoteMedicalView.IsOpen` | the object `TakeOff` instantiates goes through `Item.Start` → `ItemIdAllocator.EnsureId` and an ordinary spawn report | new id from the **plain** counter | `condition` and the template only |
| component removal on the target's client | `LimbComponentStateCodec.RemoveAbsent` | — | — | `DestroyImmediate`, deliberately not the native `TakeOff`, because "the design gives the removed item to the OPERATOR" |
| cook | the host (`ItemService`: a guest's cook is refused) | `CookItemCommand` → `ItemDomainModule.DecideCook` | `ReplacedBy` + a spawn | product gets a NEW id from `ItemIdAllocator`; no item field crosses |
| craft | the crafter, via `CraftReportMsg` | `CraftReportJudge.Classify` → `TryDestroy(sender, id, TerminalKind.Destroyed, …)` | `Destroyed` | product gets a NEW id from `_ids.EnsureId`; the wire carries no lineage |

Three facts fall out of that table.

1. **The same native transform is recorded as two different terminal kinds** depending on whether it
   crossed a player: `Consumed` when the affected client ran it against a standing item object, and
   `Destroyed` when the local body ran it natively. Nothing branches on the difference today
   (`TerminalKind` is `{Consumed, Destroyed, ReplacedBy}`, and the only kind-sensitive branch in the
   domain is the cook path), but it is why "one transform, one answer" is not yet true.
2. **The same transform also produces a new id through TWO different counters.** A cross-player removal
   mints from the synthesized namespace; a local removal lets the native `TakeOff` run and the plain
   allocator stamps the new object. They cannot collide (the high bit keeps the namespaces apart), but
   they are two mechanisms answering one question, and the local one is the path a solo player takes.
3. **The component carries no identity anywhere** — not on the scene object, not on the wire
   (`ComponentStateMsg` is `{TypeName, Fields}`), not in the archive. Any answer to the ticket's
   question has to supply that first; the options below differ only in what happens after.

The rest of the current circuit, for completeness:

- `TerminalKind` is a label on a terminal location. A terminal row can never be picked up, dropped,
  updated, transferred or re-spawned (`ItemDomainModule` rejects each by name), it is never removed from
  the state table ("a destroyed item is never resurrected, and removing its tombstone would let a stale
  spawn report re-create it"), and it rides the checkpoint.
- The carried registration is an ABSOLUTE set of full item states, and `ItemArbitration.RegisterCarried`
  is ADD-ONLY: an id already registered is skipped, and there is no removal pass, so a set that shrank
  says nothing. "This carried item is now mounted on a limb" is not expressible through it, and it does
  not need to be — the item's own commit is what states it.
- A peer's clone of a consumed carried item is not removed by an event; `KernelBatchItemProjection`
  emits carried facts only for spawn/relocate/data-update, so the clone converges from the owner's 1 Hz
  character snapshot and the difference is logged (`[CharSync] divergence … left the inventory without an
  event sync`).
- The mod-facing surface: `IModInventoryEntry` documents instance id, item id, slot index, condition,
  favourite flag and contents. No page states the id's lifecycle.

## 4. The boundary: which transforms are form-preserving

The ticket's scope rule needs a definition that comes from the data, not from taste. One question
separates the two halves: **does the game turn this component back into an item?**

| Transform | Verdict | Evidence |
|---|---|---|
| splint, carcass splint, tourniquet | **form-preserving** — the component IS the item's installed form | the game's own `TakeOff` instantiates the item from the component's template and copies the component's `condition` back; `OnDismember` is declared by exactly these two types |
| ice pack (`ChilledLimb`) | **not** — the item survives the apply and the component is an effect | branch 4 never destroys the item, the component reads no item field, and there is no path back |
| every other limb tool the family admits (`medicalsuture`'s bleed ramp and the rest of `LimbToolAdmission`'s set) | **not** — no component holds the item | the verdict is the absence of a path back, not the item's fate: `medicalsuture` (`Item.cs:368-386`) only spends `item.condition` and does not destroy the object, exactly like the ice pack |
| cooking | **not** — content changed | `HeaterCookSync` drives `CookItemCommand`; the source is `ReplacedBy` and the product is a newly spawned row |
| crafting | **not** — content changed | `CraftEntryDisposition.Destroyed` entries plus products captured from objects the game just created |

So the rule "the id survives" covers exactly **`SplintLimb` (two definitions) and `TourniquetScript`
(one)**. Everything else is a new item — and for those, an optional lineage field is the most that is
still on the table (§5, option 2).

## 5. The three answers CUO could give

### Option 1 — the applied item keeps its id and gains a location (recommended)

An item applied to a limb stops being carried and becomes **located on that limb**. Its id is unchanged,
so the mod that recorded it can still follow it, the favourite mark still resolves, and a removal is a
**transfer** rather than a mint.

- The kernel gains one location family: applied, naming the owner and the limb (and the component type,
  which the definition already implies).
- Owned state splits along the line the game already draws: the item's `condition` is the item's
  durability; `timeApplied` and `conditionLossMinute` are application facts and stay on the component.
  The component's `condition` field remains the scene's live copy — the game's own `Update` writes it
  every frame and its own `TakeOff` reads it — and the durable item row is filled from the SAME 1 Hz
  character capture that already carries the component state today, so no new traffic appears.
- Removal is a move: applied → carried(operator). The operator's existing "awarded item" materialization
  (`MedicalOperationApply.RestoreAwardedItem`) becomes an ordinary carried-item apply. `timeApplied` is
  still not carried, and that stays honest: it is a fact about the application, not about the item.
- Decay to zero becomes the item's own end (`Destroyed`), instead of the current mint-then-immediately-
  destroy.
- `OnDismember` becomes applied → carried(body owner), which is what the native flow already does with
  the object it creates.
- The cross-player apply and the local apply converge on ONE commit, which also removes the `Consumed`
  / `Destroyed` split of §3.

### Option 2 — a lineage field on the created item

The item still dies; the item the transform creates carries `derivedFrom = <the id it came from>`.

- It needs the SAME first step as option 1 (the component must name its item), because otherwise the
  removal has nothing to derive from. So the cheap-looking option is only cheap *after* the expensive
  first step.
- It answers a mod with "no, but here is its parent", and it needs a rule for chains (one hop or
  transitive?) and for the case where the parent is itself terminal (a splint that decays before anyone
  takes it off).
- It leaves the favourite mark dangling exactly as it does today, because the id the mark holds is
  still the dead one.
- Against option 1 it saves the kernel location family, the wire location kind and every consumer of
  "an item can be somewhere other than the inventory" — that is the whole of §7's cost table minus the
  capture/restore column, which it still owes.

### Option 3 — document the status quo

Write the rule down as it behaves: an id names one incarnation; a transform that ends the incarnation
ends the id; a transform that produces an item produces a new id; a terminal id is never reused.

- Cheapest by far: one page, no code.
- It is a real answer, and it is the one every other CUO mechanism already follows.
- What it costs is the question the user is actually asking: a mod (and CUO's own favourite ledger)
  cannot follow an item across the one transform a player performs most often on somebody else's body.

## 6. The ticket's eight questions

The answers below are read off the tree and written in the present tense of the RECOMMENDED model, not
of the code: they describe what option 1 would make true, and none of them is decided until the user
rules.

**1. State ownership.** Split by who writes the value. `condition` is the item's durability — the game
copies it in both directions and the player reads it as the splint's remaining life — so the item row
owns it. `conditionLossMinute` and `timeApplied` are properties of this application, so the component
owns them. The component's own `condition` field stays what it is today (the scene's live copy, written
by the game's own `Update`), and the ONE seam that fills the durable row is the 1 Hz character capture
that already reads that field. There is no second writer, so there is nothing to drift; and the value
used at removal is the affected side's live component, which is decision 184's rule applied unchanged.

**2. Ownership and taking.** The item is owned by the body's owner while applied — the same actor the
kernel already names — so "whose is it" needs no new concept. Taking it off is a transfer to the
OPERATOR, expressed as the existing rule already expresses it (`OtherMedicalRemovalApplier` hands it to
the operator's first empty slot); what changes is that the transfer moves the item instead of minting
one. A search/frisk cannot reach it, because it is not in the inventory and not a wearable — which is
also the native behaviour (the only native gesture that removes a splint is the wound view's own
action). Death does nothing to it, because nothing in the native tree touches limb components on death.
Amputation is a move to the body's inventory, the shape the native `TakeOff` already produces.

**3. Reporting and reconnect.** The carried registration does not need to express "this one is applied":
an applied item is not in the inventory, so its absence from the absolute set is correct and carries no
information, while `RegisterCarried`'s add-only shape means the host's existing row is never
resurrected as carried. The fact lives in the item's own commit, which is where every other item
transition already lives. On reconnect and on a restore, the applied row travels in `items.json` (it is
not world-rooted, so a layer-end cut keeps it) and the component travels in the character file's limb
row; the restore must bind the two, and a component with no row (or a row with no component) is a
divergence the restore names rather than guesses at — the same shape
`todo/id-carriers-can-disagree-about-placement.md` records for placement.

**4. The native entry/exit list.** TakeOff (wound view) → application; `OnDismember` → application;
death → nothing; decay to zero → the item's own `Destroyed` end; disconnect → the owner's row survives
in the archive, the scene is rebuilt from it; layer change → survives, because the native `Clear()`
(`WorldGeneration.cs:1077-1094`) destroys only unparented WORLD items and never touches a body. The two
points that destroy without a move are named: the native decay-to-zero path (which today mints an item
it immediately destroys — under option 1 it becomes an honest `Destroyed`) and `Limb.cs:135-138`, where
the severed limb's wearables are dropped first (`Limb.cs:104-107`) and then destroyed. The second one is
about wearables rather than components, and whether the destroy still reaches anything after the drop is
not established by reading; it is recorded here as OUT of this rule's scope, not as covered by it.

**5. Save and the two carriers.** `items.json` gains the new location kind on `WireItem` (which already
carries `Location` and `Data`); the layer-end trim keeps it automatically, because
`ItemLocationChain.IsWorldRooted` answers false for anything not world-rooted. The character file needs
no new field for the placement — the limb row already carries the component list positionally — only the
id association, which is the same addition the wire needs. `id-carriers-can-disagree-about-placement`
is the neighbouring question about the two carriers disagreeing about placement, and this answer makes
that worse before it makes it better unless the presence rule is stated with it: **the item row says
WHICH item is applied and the limb row says the component is there; a disagreement is a divergence the
restore names**, exactly as the existing ticket asks for placement.

**6. The boundary.** §4. `ChilledLimb` is not an item-derived component: the ice pack is never
destroyed and the component reads no item field. The gap between the two is exactly "the game turns it
back into an item".

**7. Conflict check.**

- `TerminalKind.Consumed` — **changes**. Under option 1 a form-preserving apply is no longer terminal,
  so the limb-tool family's consumed commit splits: the component-bearing tools relocate, and the tools
  that are genuinely spent (the transfusion, the suture, the ice pack's charge) keep `Consumed`. Both
  halves of §3's split disappear, and the LOCAL removal path is the second half: it must stop letting
  the native `TakeOff` mint through the plain counter and route through the same relocation.
  `Consumed` itself keeps its meaning and needs no enum change.
- The favourite mark's guard (decision 258) — **keeps**, and gets better: the guard is "id and
  definition", and under option 1 neither changes across the round trip, so a mark follows the splint
  onto the limb and back. Today the mark is left pointing at a dead id forever.
- "An id is issued once and never reused" — **keeps, and is the reason option 1 is expressible at all**:
  moving an id is only safe because nothing else can ever claim it.

**8. Cost and risk** — §7.

## 7. Cost and risk, per module

Counted from the tree, for option 1. "Wire" means the deployment ships both peers together (the
handshake is the compatibility boundary), so no compatibility layer is needed anywhere.

| Module | What changes | Size |
|---|---|---|
| Kernel location model (`GameState`) | one `ItemLocationKind` member, one field on `ItemLocation`, the per-command rules for the new kind, `AssertInvariants`, `ItemLocationChain.IsWorldRooted` | the model change is small; the rules are the work |
| Kernel commands / events | an apply and a remove decision; `ItemRelocatedEvent` is the natural carrier, so no new event type is needed if the location carries the limb | small |
| Wire | `WireItemLocationKind` member + `WireItemLocation` field, `KernelWireMapper` both directions, and the limb component's id association (`ComponentStateMsg`) | small, but it touches the batch path every peer reads |
| Save | rides `WireItem`, so the encoder needs nothing; the format page's rule for a new kind needs writing, and the restore's applied-row handling is new | small in code, needs a format-page rule |
| Restore / reconcile | the applied row must not be materialized as a world object (`RestoredWorldItemSet` filters on `World`, so this is already true) and must be turned into a limb component by the character-restore seam | medium — this is where a divergence would be born |
| Projections | `KernelBatchItemProjection` must carry the apply to peers' clones, `ItemApplication` must not treat it as a world item, `CharacterDataSync` must not read the disappearance as a divergence | medium |
| Adapter apply | `NativeLimbToolApply` reports a relocation instead of `consumed: true`; a local apply must reach the same commit (today it does not go through this file at all) | medium — and this is where the local/cross-player asymmetry is closed |
| Adapter removal | `OtherMedicalRemovalApplier` transfers instead of minting; `MedicalOperationApply.RestoreAwardedItem` becomes an ordinary carried apply; and the LOCAL removal path (`RemoteMedicalPatches` only intercepts while the remote medical view is open) has to reach the same commit instead of letting the native `TakeOff` allocate | medium, and it DELETES a mechanism rather than adding one |
| The synthesized namespace it deletes | `ItemIdCoordinator.NextAwardedItemId` has exactly one production consumer (`OtherMedicalRemovalApplier`), so the id, its counter, `ItemInstanceIds.AwardedHighBit`/`IsAwarded`/`AwardedCounterOf` and `ResumeAwardedIds` all go. The surface that goes with them is NOT free: `docs/architecture/save-archive-format.md` §6.1's synthesized-namespace paragraph, `docs/evidence/sync-coverage-matrix.md`'s row I8 and its `sync-coverage-evidence.json` quotes, and the `ItemIdCoordinatorTests` cases that pin the awarded resume | medium — a doc and test sweep on top of the code deletion |
| Adapter capture/restore | `LimbComponentStateCodec` (the id association), `CharacterDataCapture`, `CharacterRestoreApplier`, `CharacterDataSync`, `ItemStateCodec` (an applied item's local incarnation, which is the standing-item category today) | medium-large; this is the single biggest cluster |
| Arbitration / transfer table | `ItemArbitration` must hold an applied row without treating it as carried, and the "neither a world item nor carried by the sender" audit line it feeds (`KernelCommandGateway.MayReportDestroyed`, in `Application/Kernel`) needs the new family named | small |
| Mod API | whether `IModInventoryEntry` shows an applied item, and if so through which surface; a baseline change plus the human pages | a decision, then small |
| Tests | the removal/award family (`MedicalOperationOtherActionsSessionTests`, `MedicalOperationConcurrencyTests`, `ItemIdCoordinatorTests`, `MedicalTargetBodyValidatorTests`, `LimbComponentStateCodecTests`) plus new kernel and restore cases | the largest single block of work |

**Risk.** The mechanism has one genuine failure mode: an applied row and a limb component disagreeing
after a cut. It is the same failure mode the neighbouring ticket already records for placement, and it
is answered the same way — one carrier is named the authority for presence, and a disagreement is a
named divergence at the restore rather than a guess. The rest of the risk is breadth: option 1 touches
the kernel location model, which every item consumer reads, so the change is wide even where each edit
is small.

**Against option 2**, the honest comparison is not "small vs large": option 2 pays the component-id half
(medium-large, the capture/restore cluster) and saves the kernel/wire/restore half (the model, the
projection and the reconcile). It buys a weaker answer for it.

## 8. What this record does not settle

- **No runtime evidence exists for any of it.** Every claim above is source-level: the decompiled game
  for §2, our own tree for §3–§7. A cycle that implements option 1 must observe the round trip in a
  two-client run, and the ticket names no acceptance row because there is nothing to accept yet.
- **The ice pack's charge is not tracked.** Branch 4 spends `item.condition` with no floor check and the
  item survives; what a mod should see for a partially spent ice pack is out of this rule's scope.
- **The wielded/worn neighbourhood is deliberately untouched.** The negative wear slot
  (`-(limbIndex + 2)`, `ItemStateCodec.SlotOf`, `IWearSemantics`, `ItemData.SlotIndex`) is CUO's existing
  positional encoding for "an item on a body", and it already keeps the id. Whether the applied location
  should reuse that encoding or be a location family of its own is a design decision for the
  implementing cycle, not a fact this record can settle; what this record settles is that "an item on a
  body has a position and keeps its id" is not a new idea in this tree.
- **The two carriers' disagreement** is `todo/id-carriers-can-disagree-about-placement.md`'s question and
  stays there. This record only says the answer must not contradict it.
