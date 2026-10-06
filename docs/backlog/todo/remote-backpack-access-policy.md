# Who may open and operate another player's backpack: an access policy, not one switch

- Status: Todo — **user decision (2026-10-06)**: asked whether every operation on another player's backpack
  has a switch, the user ruled that a three-way policy should exist — allow operating a conscious player's
  backpack / only an unconscious (or dead) one / deny operating other players' backpacks — and that the
  option labels are the agent's to choose. The audit below is what stands today.
- Priority: Medium
- Category: Host rules / remote inventory access / user-visible policy
- Source: User message (2026-10-06), raised while the remote-inventory rework's rows were being read: "check
  whether operations on another player's backpack all have a switch — e.g. a dropdown in the multiplayer
  config with allow / unconscious-only / deny".
- Related: `todo/remote-inventory-native-parity-rework.md` (the operation surface this gates),
  `todo/give-item-to-another-player.md` (the new push gesture must obey the same policy),
  `src/CasualtiesUnknownOnline.Runtime/Session/HostRules/HostRulesService.cs`, decisions 154 (cross-player
  take/heal/use are host-validated), 218 (the native-intent window), `review/remote-interaction-local-gating.md`
  (the reach/sight verdict is the actor's own client's), `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`.

## What stands today (audited 2026-10-06, from the code)

1. **One global host rule, a bool, covers the whole family — except opening the view.** `AllowRemoteInventoryTake`
   (default `true`) is bound to BepInEx `HostRules/AllowRemoteInventoryTake` ("other players may take carried
   items from a remote player's inventory (unconscious/dead loot remains the default rule; false disables
   cross-player inventory take entirely)") and is live-editable in the Online UI's admin page
   (`admin.rule_allow_remote_inventory_take`, `HostRulesConfigEditor`). It is enforced on the HOST in two
   places: `PlayerRemoteInventoryIntentService.HandleRemoteInventoryIntentRequest` refuses every native intent
   when it is false ("host has disabled remote inventory manipulation"), and
   `PlayerInventoryTakeService.HandleTakeCore` refuses the take/transfer paths ("host has disabled
   cross-player inventory take"). A denied intent is a log line, not a silent no-op.
2. **Opening the remote backpack has its own gates, and no consciousness test.** `RemoteBackpackCoordinator.Open`
   requires an active session, a local player in world, `IPlayerInteractionVisibility.HasLineOfSight` (a
   Ground-only linecast between the two authoritative positions; missing evidence deliberately does not
   block) and a live render clone; the Online UI offers the button only for a member that `CanSee`, is not
   local and is in world. Nothing asks whether the OWNER is conscious.
3. **A consciousness rule exists, but only on the old Online-UI take list, and it is the opposite
   default.** `HandleTakeRequest` passes `allowConsciousSource: false`, so a conscious/alive source is
   refused ("is conscious/alive and not takeable"), and `OnlineUiMemberProjection.canTake` lists takeable
   items for unconscious/dead members only. The native remote-backpack viewer deliberately passes
   `allowConsciousSource: true` — its own doc comment: "The remote backpack is an explicit co-op inventory
   surface, so transferring an item to the opener's own inventory is allowed for a conscious remote owner as
   long as `AllowRemoteInventoryTake` is enabled; the Online UI's unconscious-only take rules are not applied
   to this dedicated gesture."
4. **Host rules are not replicated to guests.** There is no host-rules message and `HostRulesService` reads
   the LOCAL options monitor, so a guest's UI decides what to offer from its own local value while the host
   enforces the real one. Any policy UI has to answer this, either by shipping the rule at join/handshake or
   by refusing with a precise line when a guest offers a control the host denies.
5. **What is NOT gated at all:** operating INSIDE a conscious owner's backpack (move, swap, drop, use, pour,
   favourite, container moves) once the view is open, and the cross-player use-by-drag gesture
   (`PlayerItemUseService`, which has its own conscious/alive checks on both sides but is not part of the
   backpack policy).

## What the policy should be

- One enumerated host rule replacing the bool (pre-release policy: replace it, keep no compatibility layer):
  **Allow** (today's `true`), **UnconsciousOrDeadOnly** (the mid value the user asked for), **Deny** (today's
  `false`), exposed as one BepInEx dropdown under `HostRules` and one row on the Online UI's admin page, with
  localized labels.
- Enforced in ONE host place for the whole family — the intent request handler plus the take/transfer paths
  already funnel there — and asked by the client side too, so the view is not opened (and the button is not
  offered) when the policy denies it. A refusal is a named log line on the refusing side.
- The owner's state that decides it is the authoritative one: `PlayerState.Conscious` / `Alive` from the
  character snapshot and kernel (`IsInWorld` already gates the same paths). "Unconscious or dead" is the
  recommended reading of the middle value, because it is the rule the existing Online-UI take list already
  applies (`!Conscious || !Alive`); a strictly-unconscious variant would be a different decision and should be
  taken deliberately.

## What a fix has to decide

1. Does the middle value cover dead owners as well as unconscious ones (recommended: yes, matching the landed
   take rule), and what it does when the owner's vitals are unknown (no snapshot yet) — recommended: fail
   closed for the middle and deny values, allow for **Allow**.
2. Whether the policy gates the whole remote-inventory family (open + every intent + the take) or only taking
   items out. Recommended: the whole family — an open view on a forbidden owner can still shuffle and drop
   their items, which is exactly what the option is meant to prevent.
3. How a guest learns the host's policy (replicate at join vs. refuse-and-name on the attempt), and whether
   the Online UI hides the button or shows it disabled with the reason.
4. Whether the policy also covers the cross-player use-by-drag gesture and the new push gesture
   (`todo/give-item-to-another-player.md`). Recommended: yes for the push (it writes into their inventory);
   the use-by-drag keeps its own conscious/alive checks and is named in the ticket rather than silently
   folded in.

## Acceptance (the rows a run must read)

A three-client run (operator = the guest, owner = the host, third peer = the other sandbox), once per policy
value:

| # | Scenario | Expected |
|---|---|---|
| 1 | Policy = Allow, owner conscious | The view opens and every row of the rework's matrix still behaves as it does today |
| 2 | Policy = UnconsciousOrDeadOnly, owner conscious | The view is not offered/opens refused, or every intent is refused with a line naming the policy; the owner's items are byte-identical before and after |
| 3 | Policy = UnconsciousOrDeadOnly, owner unconscious | The view opens and the operation works |
| 4 | Policy = Deny | Neither the view nor any intent is available, with one line naming the policy |
| 5 | Solo / no session | Unchanged local behaviour |
| 6 | A guest whose local config differs from the host's | The host's policy wins and the refusal names it |

## Non-goals now

- Not a new permission system per player or per item: one host-wide enumerated rule, enforced host-side.
- Not a change to the medical surfaces (the remote WoundView and the medical operation sessions keep their own
  gates), and not the carry relation.
- Not the push gesture itself, which is `todo/give-item-to-another-player.md`; this ticket only decides the
  policy both obey.
