# A craft report never retires a carried material

- Status: Done (batch `20261025-d`, 2026-10-20: the member crafted the `slingbag` from four carried
  destroyable materials and again with the wear slot occupied; every viewer applied the report's four
  `[CarriedSync] removed …` lines, the host apply line and the third peer's relay line sit in the same
  window, and **zero** `[CharSync] divergence` appears in any of the three 6 s windows. Rows 1–2 pass;
  record `docs/evidence/acceptance/craft-carried-fact-retirement-20261025-d.md`)
- Priority: Medium
- Category: Crafting / clone fact table (carried-fact event coverage and report ordering)
- Source: independent adversarial review of `done/snapshot-overtakes-item-reports.md` (2026-10-20) —
  the review of that fix read the craft branch and found the inversion it did not close. Code reading
  only; no craft had been driven since the finding.
- Related: `done/snapshot-overtakes-item-reports.md` (the three-gesture fix this is the sibling of),
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/CraftingPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Items/CraftingSync.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterSessionBinding.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftCarriedRowRetirement.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/IItemControl.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs`

## Mechanism (code reading)

- `Recipe.TryMake` consumes the materials first, then spawns the product
  (`reversing/Assembly-CSharp/Assembly-CSharp/Recipe.cs:179`: `this.items[i].UseItem(itemsForRecipe[i])`
  in a loop; `:181`: `this.result.SpawnResult(this.INT)`). A destroyable non-liquid material's `UseItem`
  does `PlayerCamera.main.body.DropItem(item)` then `Object.Destroy(item.gameObject)`
  (`RecipeItem.cs:182-183`), and `DropItem` unparents immediately (`Body.cs:1448`:
  `item.transform.parent = null;`).
- `RecipeResult.SpawnResult` creates the product and lands it through `Body.AutoPickUpItem`
  (`RecipeResult.cs`: `Utils.Create(...)` then `PlayerCamera.main.body.AutoPickUpItem(component2)`). A
  wearable product is WORN there, through `Body.WearWearable`.
- **Correction (2026-10-20, native re-read this cycle): a wearable in the product's slot is NOT
  displaced.** `AutoPickUpItem` calls `this.DropItem(wearableBySlotID)` (`Body.cs:522-527`), but
  `DropItem(Item)` only acts when `HoldingItem(item)` (`Body.cs:1443-1452`), and a worn item is a child
  of a `Limb`, not of a slot — so the call is a no-op. `WearWearable` then finds the incumbent through
  `GetWearableBySlotID` and REFUSES the product (`Body.cs:1482-1489`): the incumbent keeps its worn row,
  and the product stays a standalone world item (reported by the item domain's own instantiation path,
  outside the craft scope). So no displacement drop fact exists, and the drop-report half this ticket's
  first draft assumed is not part of the fix.
- `WearWearablePatch.Postfix` still called `PatchBridge.Impl?.OnInventoryChanged()` when the product was
  worn inside the craft: that snapshot is captured after the materials were consumed and BEFORE
  `CraftingPatches.RecipeTryMakePatch.Postfix` commits the craft report (`OnCraftEnd` runs first there).
  It was the character snapshot — not the report — that first told the peers the inventory changed.
- The product is id-less in that snapshot, so the divergence monitor skips it as unbound. The consumed
  materials are NOT: they were carried with ids (the 1 Hz snapshots carried them;
  `CraftingSync.OnCraftBegin` stamps every material's id before the native call), so
  `CloneFactTable.WarnOnDivergence`'s missing-entry loop logs
  `left the inventory without an event sync` for each of them.
- The craft report did not cure the fact table either: a `Destroyed` entry retired a row only when it
  was a world row (`CraftSyncService.ApplyGuestReport` → `CraftVerdict.WorldDestroy` →
  `RemoveWorldItemLocal`; the relay half said so in its own comment — "only the crafter ever had it; this
  side's clone fact table heals via the 1 Hz character snapshot"). A carried row took
  `CraftVerdict.TransferredRemove` (transfer table + kernel row) and never reached
  `CharacterDataSync.RemoveCarriedItem`, whose only caller was the `ItemDropped` event.
- The non-craft auto-pickup path is not inverted: there the pending drop (when one exists) defers
  `OnInventoryChanged`, so the snapshot cannot precede the fact. Only the Craft scope suppressed the
  event coverage.
- The HOST's own craft had the same hole on its side of the authority: the in-craft destroy claim
  (`CraftingSync.ShouldSuppressDestroy`) keeps `ItemWorldSync.OnItemDestroyed` off the consumed material,
  and the own-report branch aligned only world rows ("the scene IS the record") — so the host's kernel row
  for a destroyed carried item stayed alive.

## What a fix has to decide

- **Retire the carried row from the fact table when the craft report says it was destroyed.** The craft
  report IS the event; it reaches a carried-removal event in the item domain, applied on the host and on
  every relay receiver. Reusing the destroy event wholesale is a decision, not a given:
  `ItemApplication.OnRemoteItemDestroyed` deliberately refuses to kill a carried item's OBJECT ("A carried
  item or a remote clone display proxy must never be killed by a remote destroy"), so the fact-table
  retirement and the object retirement are separated into a new event.
- **The retirement must be attributed.** The fact table removes a row by id, and the drop path removes it
  from every owner (the drop event names no owner) — a craft report's Destroyed entry must not be allowed
  to remove a member's row just because a report named its id. The host anchors the id to the sender
  (transfer table, or the kernel row when the registration is the lost half) and names that owner; the
  relay applies the host's stamp. A destroy the host cannot attribute is skipped and warned, exactly as
  before.
- **The relay cannot classify for itself** (its tables are empty): the host stamps the routing
  (`CraftApplyKind.WorldDestroy` / `CarriedRetire`; `None` = no action) so each receiver acts on its scene
  copy or on that owner's fact row — one existing `ApplyKind` field, no new wire member.
- **Skip the in-craft snapshot** (`WearWearablePatch.Postfix` under `Origin.Craft`): the craft postfix
  already sends one right after `OnCraftEnd`, so the operation still gets one immediate re-report, now
  after the report that explains it. The two craft entry points are the only `Origin.Craft` entrants, and
  both send that snapshot.
- **The host's own craft retires its kernel row** for a destroyed carried entry — the report is the only
  event left, because the ordinary destroy report is claimed off.

## The fix (this cycle)

- **A carried-removal event with an owner, not the destroy event.** `IItemControl.CarriedItemRetired`
  (owner, itemId) is published by `ItemService.RetireCarriedLocal`, and `GameAdapterSessionBinding` routes
  it into `CharacterDataSync.RetireCarriedItem` → `CloneFactTable.RetireCarriedItem`, which removes the id
  from THAT owner's fact table only (top-level or nested). The drop path keeps its own every-owner
  `RemoveCarriedItem` (the drop event names no owner). The world `ItemDestroyed` event keeps its meaning
  and its comment now says a carried row never travels through it.
- **The host anchors, then stamps.** `ApplyGuestReport` retires on `TransferredRemove` (the transfer table
  says the sender owns it) and on `UnknownSkip` only when the host's kernel row still says the sender
  carries it (`CraftCarriedRowRetirement.IsOwnedBy`); an unattributable destroy keeps the old skip + warn.
  The anchored entries are stamped `CarriedRetire`, world destroys `WorldDestroy`. `ApplyRelayed` routes
  `WorldDestroy` to `RemoveWorldItemLocal`, `CarriedRetire` to `RetireCarriedLocal(msg.OwnerSteamId, id)`,
  and `None` to no action, and logs the routing counts (`[Crafting] <Kind> relay of <sender>: … applied.`).
- **The host's own craft** retires its kernel row (`CraftCarriedRowRetirement.RetireOwnRow` →
  `ItemKernelAuthority.TryDestroy`, named at Information when there is no row to retire), stamps
  `CarriedRetire` for the guests' fact rows and `WorldDestroy` for their scene copies.
- **The in-craft snapshot leaves the scene.** `WearWearablePatch.Postfix` computes
  `CallContext.IsWithin(CallContext.Origin.Craft)` once and exits before `OnInventoryChanged` when it is
  true; the craft postfix's snapshot (after `OnCraftEnd`) is the one immediate re-report.
- **Pinned, source and runtime.** `InventoryChangeReportOrderPinTests` gained the wear postfix's withhold
  (with a matcher that ties the exit to the guard's OWN block, plus positive, direct-guard, local-guard,
  inverted, comment-only, multi-call and wrong-condition samples), the craft postfix order, a by-pass
  check (no patch calls `ReportInventoryChanged` directly), the session-binding route, both apply paths'
  retirement and stamps — including the host-own `WorldDestroy` stamp — and the attribution helpers.
  `CraftSyncSimulationTests` drives the host apply, the relay apply, the kernel-anchored untracked entry,
  the world-destroy negative, the other-guest-pickup race (no retirement of the winner's row), the Changed
  negatives (guest and host-own) and the host-own kernel row. `CloneFactTableDivergenceMonitorTests` adds
  the fact-table half (a row retired by the report leaves the next snapshot quiet).
- **Wire shape.** Two enum values on the existing `CraftEntryMsg.ApplyKind` field (`WorldDestroy = 2`,
  `CarriedRetire = 3`); no version bump (pre-release, decision 241). Sync-coverage row I6 now says a
  carried destroy retires each receiver's fact row instead of falling back to the snapshot.

## Acceptance

The owner (a member) crafts a wearable product whose recipe consumes destroyable, id-bound carried
materials on the deployed artifact: every peer's window over the craft carries **zero**
`[CharSync] divergence` for each consumed material across at least one full 1 Hz cycle, the host's craft
application line (`[Crafting] <Kind> of <sender>: … applied.`) and the third peer's relay line are present
in the same window, each receiver whose fact table held the row logs its `[CarriedSync] removed …` line in
that window (so the fact-table retirement ran as its own event, before the snapshot), and the product
reaches both peers as a carried fact. A second variation pre-wears an incumbent in the product's wear
slot: by the native refusal above the incumbent keeps its row and the product stays a world item — the
zero-divergence row is judged for the incumbent and the consumed materials.

## Evidence

- This cycle's self-check, mechanism × change × evidence table and the independent review's dispositions:
  `docs/evidence/selfchecks/items/craft-carried-fact-retirement-selfcheck.md`.
- The runtime half: `docs/evidence/acceptance/craft-carried-fact-retirement-20261025-d.md` — the member
  crafted the wearable `slingbag` from four carried destroyable materials, and again with the wear slot
  occupied (native refusal); every viewer applied the report's four removals, the host apply line and the
  third peer's relay line are in the window, and **zero** divergence appears in any of the three 6 s
  windows.

## Limits

- The acceptance batch drives a member as the owner; the host-as-owner direction is covered by the
  simulation half only (the kernel-row and stamp tests). The patch code is role-independent, but a
  host-owner repetition stays useful.
- A Destroyed entry the host cannot attribute to the sender (neither the transfer table nor the kernel
  row names it, e.g. another member won the pickup) is still skipped and warned; its row is not retired.
  That is deliberate: a report may not retire another member's fact row, and the receiver has no table to
  classify with.
- The craft report is sent reliably but `PacketSender` drops a refused send without retry: if the report
  never arrives, the next 1 Hz snapshot announces the disappearance again (the pre-existing transport
  property; the retirement is not a convergence guarantee).
