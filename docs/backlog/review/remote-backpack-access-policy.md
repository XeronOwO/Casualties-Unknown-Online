# Who may open and operate another player's backpack: an access policy, not one switch

- Status: Review — implementation, independent review fixes and the gates landed 2026-10-22; the
  acceptance run below is what remains. The user's rulings: 2026-10-06 (a three-way policy should exist —
  allow / only an unconscious (or dead) one / deny — and the option labels are the agent's to choose) and
  2026-10-22 (the middle value keeps the backpack OPEN but view-only, with the game's own red slot look
  as the hint and one red alert line; only an unconscious or dead owner may be operated on, which
  includes giving items in). The audit below is what stood on 2026-10-06, before the change.
- Priority: Medium
- Category: Host rules / remote inventory access / user-visible policy
- Source: User message (2026-10-06), raised while the remote-inventory rework's rows were being read: "check
  whether operations on another player's backpack all have a switch — e.g. a dropdown in the multiplayer
  config with allow / unconscious-only / deny".
- Related: `done/remote-inventory-native-parity-rework.md` (the operation surface this gates),
  `done/give-item-to-another-player.md` (the new push gesture must obey the same policy),
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
4. **Host rules ARE replicated to guests now** (`done/host-rules-replication.md`): the host announces the
   whole rule set at a member's admission, with the world-entry and 60 s repair groups, and on every change
   while the session runs, and `HostRulesService` answers a member with the host's values. So a guest's UI no
   longer has to decide from its own local value: the member card's take affordance already follows the
   announced rule, and this ticket's policy can be read on the client the same way — what it still has to
   decide is the SHAPE of the answer (hide the button, or show it disabled with the reason), and that opening
   the view is gated at all: `RemoteBackpackCoordinator.Open` asks no rule today, so a denied policy still
   lets the view open and every intent is refused one by one.
5. **What is NOT gated at all:** operating INSIDE a conscious owner's backpack (move, swap, drop, use, pour,
   favourite, container moves) once the view is open, and the cross-player use-by-drag gesture
   (`PlayerItemUseService`, which has its own conscious/alive checks on both sides but is not part of the
   backpack policy).

## What the policy is (settled 2026-10-22)

- One enumerated host rule replacing the bool (pre-release policy: replace it, keep no compatibility
  layer): **`RemoteBackpackAccess`** = `allow` (today's `true`, the default) / `unconscious_or_dead` (the
  mid value the user asked for) / `deny` (today's `false`), exposed as one BepInEx dropdown under
  `HostRules` and one row on the Online UI's admin page, with localized labels. The old
  `AllowRemoteInventoryTake` name and its boolean go with it (`allow` / `unconscious_or_dead` / `deny`
  are the text forms the config and the console's `/hostrules` JSON use).
- **The middle value is VIEW-ONLY on a conscious owner** — the user's ruling of 2026-10-22, answering
  the one open question in the ticket: the backpack still OPENS, the items are visible, but nothing may
  be operated on them. The reference is the native game's own LOOK: an unconscious player can open their
  own pack, cannot operate, and the native slot buttons are RED. The remote view reproduces that look —
  the focused clone's body slots are recoloured with the same button fill, deliberately WITHOUT changing
  `InventorySlot.canPickUp` (that answer drives the native drop-on-unconscious branch, which a view-only
  display must not newly fire on a conscious clone) — and one red native alert line names the rule on
  open and on a refused gesture. The native state also refuses the PICKUP; this design permits the
  pickup and refuses the release. An unconscious or dead owner's pack is fully operable under the same
  value.
- **`deny` closes the view too**: the member card does not offer the backpack button, a direct open is
  refused by name, and every intent is refused. `allow` keeps today's behaviour exactly.
- **The whole family obeys it**: opening the view, every native intent captured from it (move, swap,
  drop, use, wear, combine, battery, drain, trader hand-in, apply-to-limb), the cross-player take, and
  the give/push direction — the give is an operation on the destination's inventory, so it is refused
  by the middle value for a conscious receiver like every other operation. The cross-player
  use-by-drag keeps its own conscious/alive checks and is named here rather than folded in.
- **The host is the verdict, at two seams**: `PlayerRemoteInventoryIntentService` for every intent and
  `PlayerInventoryTakeService.HandleTakeCore` for the custody transfer (the direct card take included).
  Both judge the OTHER player — the owner for a take, the DESTINATION body for a give — by the
  authoritative `CharacterDataMsg.Health` (`!Conscious || !Alive`, the same rule the Online-UI take list
  already applies), and both fail closed when that health block is unknown (the middle and deny values
  refuse; `allow` proceeds). The actor's own client refuses the same gestures locally so the player sees
  the reason at the gesture, but that check is an interception: the host's refusal is the one that
  settles the record, and the INTENT path answers the actor by name. The direct card take and the
  `TransferToBody` replay discard the core's reason string — a pre-existing gap owned by
  `review/intent-refusal-never-reaches-the-actor.md`, named here rather than re-claimed as answered.
- **The Online-UI member card** reads the rule in force: the host's own config on the host, and on a
  member the announced set (with none announced the local check is skipped, decision 263) — the
  backpack button is offered unless that rule is `deny`, and its direct take rows keep their own
  stricter rule (an unconscious or dead body only) under every value — the policy can close a surface,
  never open one the landed take rule keeps shut.
- **Who learns what**: the host announces the whole rule set with the rest of the host rules (decision
  263), so a guest's card and its local refusals read the HOST's announced policy; the member's own
  config is never applied as the host's policy, and before the first announcement no local refusal is
  made at all — the host's verdict decides and answers.

## What a fix had to decide, and how each was settled

1. Does the middle value cover dead owners as well as unconscious ones — **yes**, matching the landed
   take rule (`!Conscious || !Alive`).
2. Does it gate the whole remote-inventory family or only taking items out — **the whole family**,
   including the give direction, because an open view on a forbidden owner would otherwise still let a
   player write into it.
3. What happens to a conscious owner's view — **it opens view-only** (the 2026-10-22 ruling above), not
   hidden, because the native game already gives players that red-slot look and this design reproduces
   the look while permitting the pickup and refusing the release.
4. The unknown-vitals fallback — **fail closed** for `unconscious_or_dead` and `deny`; `allow` does not
   need the state at all.
5. The presentation of a forbidden open — under `deny` the button is hidden and a direct call is refused
   with one line naming the rule; under the middle value on a conscious owner the button stays (the
   view is allowed) and the reason is the view's own red state plus the alert line.

## Acceptance (the rows a run must read)

A three-client run (operator = the guest, owner = the host, third peer = the other sandbox), once per policy
value:

| # | Scenario | Expected |
|---|---|---|
| 1 | Policy = Allow, owner conscious | The view opens and a take/transfer still works as it does today |
| 2 | Policy = UnconsciousOrDeadOnly, owner conscious | The view opens VIEW-ONLY: the items are visible, every operation is refused with a line naming the policy, the focused clone's body slots are red (the native look), and the owner's items are byte-identical before and after |
| 3 | Policy = UnconsciousOrDeadOnly, owner unconscious | The view opens and the operation works |
| 4 | Policy = Deny | Neither the view nor any intent is available, with one line naming the policy |
| 5 | Solo / no session | Unchanged local behaviour |
| 6 | A guest whose local config differs from the host's | The host's policy wins and the refusal names it |
| 7 | Policy = UnconsciousOrDeadOnly, a conscious giver hands an item to an unconscious teammate | The give succeeds; the same give to a conscious receiver is refused by the policy, naming the destination |
| 8 | The host changes the rule (allow → unconscious_or_dead → deny) while a view is open | The open view becomes view-only, then closes, on the host's announcement — the same live-change path the rest of the host rules use |

## Non-goals now

- Not a new permission system per player or per item: one host-wide enumerated rule, enforced host-side.
- Not a change to the medical surfaces (the remote WoundView and the medical operation sessions keep their own
  gates), and not the carry relation.
- Not the push gesture itself, which is `done/give-item-to-another-player.md`; this ticket only decides the
  policy both obey.

## Independent review (2026-10-22) and what each finding changed

Fresh context, FULL tier (user-visible behaviour, cross-player, wire change), against the frozen tree. Verdict:
no blocker; one confirmed major defect, two major verification gaps, ten minors, four nits. The suite was
6235 behaviour + 755 gates green before the fixes; the 2 462-byte repair pass reproduced. The report is kept
whole here, because a finding fixed without its record is one the next cycle re-learns.

| # | Severity | Finding, with the code it named | Disposition |
|---|---|---|---|
| M1 | major | Encoding corruption from a UTF-8→GBK→UTF-8 pass in three changed files, including the user-visible config description at `PluginDependencyRegistrar.cs` (the em dash in "Host-only: the cheat switch — true = …" had become a code-page artefact) and seven log format strings plus eleven comments in `RemoteDragIntentDispatcher.cs`; `git diff --check` and the build could not see it. | **Fixed.** The three files were saved byte-for-byte to `.acceptance` and restored from HEAD, then re-applied with the edit tool; a UTF-8 scan now finds zero corrupted characters in them, and the diff is back to the intended hunks. The two files the reviewer saw as LF (`docs/architecture-debt.json`, `tools/acceptance/recipes/host-rules-read.cs`) were normalized to the checkout's CRLF. Root cause: the source was edited through a PowerShell `Get-Content`/`Set-Content` round trip, which is exactly what `AGENTS.md`'s "edit files with the file tools" rule forbids and what `.agent-local/harness.md` already records (2026-10-16); this cycle violated the rule and the fix restores it. |
| M2 | major | The give-direction half of the take-core gate was never exercised: `PlayerInventoryTakeService.HandleTakeCore`'s `watchedIsDestination ? to : from` was refused earlier by the intent handler, so dropping the flag would have passed the whole suite. | **Fixed.** `RemoteIntentRequestTests.GiveToBody_UnderUnconsciousOrDeadOnly_ToAnUnconsciousReceiver_MovesTheItem` (middle value, conscious giver, unconscious receiver) reaches the core's give branch and fails if the flag is ignored; the refusal case stays as the intent-handler test. |
| M3 | major (risk) | The first `InventorySlot.canPickUp` patch could newly fire `InventorySlot.Update`'s `dropWhenUnconscious` branch on a CONSCIOUS clone (`InventorySlot.cs:45-51`); the serialized flag cannot be read from the tree. | **Fixed by design.** The patch no longer answers `canPickUp`; it recolours `InvButton.UpdateGraphic` red for the focused clone's body slots only (`ReadOnlyBackpackSlotPatch`), so the display cannot change any native decision. The red look remains the acceptance's visual row. |
| m1 | minor | The client read `IHostRules` (announced-OR-local) and could present its own config as "host rule" with no announcement (`configuration.md:75`, decision 263). | **Fixed.** `RemoteBackpackCoordinator.AccessInForce()` reads the host's own config on a host and `AnnouncedHostRules.Current` on a member; null skips the local check entirely. The member card uses the same announced-only rule. |
| m2 | minor | `architecture` said both seams fail closed when health is unknown; `allow` needs no state. | **Fixed** in §3.1.1: middle fails closed, `allow` needs no state, `deny` refuses regardless. |
| m3 | minor | "Native body-search rule" was an unverified game fact. | **Fixed**: the enum and policy docs now name the landed CUO take rule (`!Conscious || !Alive`) only. |
| m4 | minor | "The same state the native game gives" over-claimed: the native state also refuses the pickup. | **Fixed**: the pages now say this design reproduces the LOOK and permits the pickup, refusing the release. |
| m5 | minor | The card-take and `TransferToBody` refusals are silent; the ticket claimed every host refusal answers by name. | **Fixed in the ticket's wording**, scoped to the intent path; the pre-existing gap stays owned by `review/intent-refusal-never-reaches-the-actor.md`. |
| m6 | minor | An unparseable config value fell back to `allow` (the widest) while calling it conservative. | **Fixed**: `ParseRemoteBackpackAccess` fails closed to `deny`; the entry's documented default stays `allow`. |
| m7 | minor | A 2 s notice window kept the game's 6 s alert alive. | **Fixed**: 7 s, longer than the alert's own life. |
| m8 | minor | The backlog index still said "unconscious-only". | **Fixed** in `docs/backlog/README.md`. |
| m9 | minor | The ticket stayed in `todo/` with no code left, and had no give/live-change rows. | **Fixed**: moved to `review/`; acceptance rows 7 (give) and 8 (live rule change) added. |
| m10 | minor | The open review ticket still named `AllowRemoteInventoryTake` as the lever. | **Fixed** with a rename note on the historical reading. |
| n1 | nit | The census gate pinned wire-field NAMES, not types. | **Fixed**: `HostRuleCensusTests.EveryMemberRule_WireFieldCarriesTheRulesValueShape` (enum rules travel as `int`). |
| n2 | nit | `RemoteBackpackView.Open` resets the pickup flag if another view opens mid-drag. | **Noted**: the host gate still refuses the intent; no code change. |
| n3 | nit | The receiver log spells the enum `UnconsciousOrDeadOnly`. | **Noted**: kept, because routing it through `RemoteBackpackAccessText.Format` would push the localization accessor-census count over its diagnostic bound; log-only. |
| n4 | nit | Pre-existing mojibake in `docs/decisions/active.md` and `KernelCommandGateway.cs`. | **Recorded** in `todo/docs-mojibake-cleanup.md` (the source file added to that carrier; the doc was already listed). |
