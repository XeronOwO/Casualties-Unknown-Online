# The mod surface for an applied item — stage 3 review record (2026-10-14)

The independent adversarial review of stage 3 (`docs/evidence/selfchecks/items/applied-item-mod-surface-selfcheck.md`,
ticket `docs/backlog/review/item-identity-across-transforms.md`), run in a fresh context against the
FROZEN uncommitted tree at HEAD `a36db820` with the prompt template in
`docs/development/review-prompt.md` (risk tier FULL: the change adds a public contract and rewrites two
human page pairs). **The reviewer modified nothing.** Every finding below was fixed in the same change,
before the commit, and the tree was re-frozen for the fix round.

## What the review reproduced (its own runs)

- `dotnet test CasualtiesUnknownOnline.slnx` — behaviour 5028/5028, and 614 of the 615 normative gates,
  the open one being the checklist gate the review cycle itself had not yet satisfied (see F1).
- `dotnet test ... --filter "FullyQualifiedName~ModItemStateTests"` — 8/8.
- The baseline is byte-identical to the gate's emitted candidate, and the candidate's mtime is 13 s older
  than the reviewed file, so it is this cycle's product rather than a stale artifact.
- `docs/standard/alignment.txt`'s three pairs match `git hash-object` exactly; the MANIFEST carries
  exactly one row for the new page; the ticket's `- Status:`, its index row and its `review/` container
  agree; the en/zh page sets stay path-identical.
- The seven line counts quoted in the self-check's §6 all reproduce.

## What the review could NOT falsify

The read's role coverage (a guest's own command really travels the wire; `BroadcastCommittedBatch` →
`SendToGuests` iterates every handshaken peer without excluding the sender; the assertion could not pass
for another reason), the terminal row's survival into a checkpoint, the slot-omission reasoning
(`command.Data ?? item.Value.Data`), the switch-not-cast mapping, the `Experimental` levels against the
modification policy, the contract's shape questions (no `object`, no erased payload, no binary envelope),
the reachability and ordering of the permission check, the three-definitions boundary against `Item.cs`,
the cooking and crafting paths against `DecideCook` and `CraftSyncService`, and the claim that no
`Protocol/` file is in the diff.

## Findings and disposition

**F1 — reported as blocker; a real reproducibility defect, fixed as such.** The reviewer ran the full
suite on the frozen tree and got a RED gate: `DeliveryChecklist_NoIncompleteRequiredBoxes`, because the
cycle's own checklist still had its build box and its structure-review box open. The self-check's §6 said
"green at gates 615/615 … on the frozen tree" with no caveat, so the number could not be reproduced at
that point in the cycle. Fixed rather than excused: §6 now names the 615th gate as the checklist
satisfying itself and says a mid-cycle run reports 614 of those 615, the same caveat stage 1's page
carried; the boxes were then checked in order and the final run before the commit has all 615 green.

**F2 — major: the lifecycle rule over-claimed, and this stage exists to state that rule.** The English
and Chinese `mod-api.md` both said "everything else — cooking, crafting and every other limb tool — ends
the source row and gives the product a new id". The game's own data says otherwise for the limb tools:
the ice pack's `useLimbAction` only spends `item.condition` and adds `ChilledLimb`
(`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:1621-1639`, with `destroyAtZeroCondition = false`),
and the suture's does the same (`Item.cs:370-386`), so the item is never destroyed and never becomes
anything — the id simply keeps pointing at it where it is. The change's own design record had the right
distinction ("the verdict is the absence of a path back, not the item's fate") and the ticket's Limits
said the ice pack survives, so the page had contradicted its own evidence. Fixed: the rule is now three
bullets — the three definitions that keep the id, the other limb tools that do not transform the item at
all, and cooking/crafting that really do end the source row and mint a new id — in both language blocks,
and the how-to pages name the same three classes.

**F3 — minor: a number that did not reproduce.** "17 added lines and one changed line" for the baseline;
`git diff --numstat` reads `18 0`, five pure-insertion hunks and no changed line (the reviewer inferred
the arithmetic that produced the wrong figure: 17 lines of new types plus 1 line in an existing type).
Fixed in the self-check's §1 and §6 and in the checklist's fourth box, which now also names the command
that produces it.

**F4 — minor: `ParentItemId` was documented as a contained-only member.** It is not: a world container's
contents are recorded as world items bound to their container, so `ItemProjection.ApplyDrop` builds
`ItemLocation.World(pos.X, pos.Y, parentItemId)` and a mod can see `Location == World` with a non-zero
parent. Fixed in `IModItemState`'s own doc (the member and the interface's one-shape paragraph), in both
`mod-api.md` pages and in both how-to tables, each now saying that a non-zero parent is not the same fact
as being contained.

**F5 — minor: a `false` had a third cause nobody documented.** A guest that has not yet received the join
checkpoint holds no item facts at all, so every id answers `false` — while the pages defined `false` as
"the framework holds no such fact", which reads as "it never existed". Fixed in the `TryGetItem` doc, in
both `mod-api.md` pages, in both how-to pages and in the self-check's §2.3.

**F6 — minor: five dangling ticket paths.** The rename `todo/` → `review/` left the ticket path stale in
five self-check pages (the review exempts `evidence/selfchecks/` from its reference gate, so nothing
failed), while the MANIFEST's copy had been updated — the reviewer called the half-fix out. Fixed in all
five, so every live pointer resolves.

**F7 — nit: a registration claim that was true for one word and not the other.** The self-check said both
new words were registered "before their first human-facing use"; `instance id` was already in use in
`mod-api.md` before this cycle (the Chinese page wrote 实例 id). The registration is right and stays —
what changed is the claim, which now says `applied item` was registered before its first use and
`instance id` is registered now, with its Chinese rendering brought to one spelling.

**F8 — nit: a claim with no case behind it.** §2.1 said the read answers on the member that performed the
apply, but no case read it there. Rather than soften the claim, the case was added: the third-member test
now also asserts the applier's own kernel answers `Applied` on the same limb.

**F9 — nit: a claim that is inferred rather than pinned.** "An applied item is not in anyone's
`IModPlayerInventory`" has no direct case, because the harness feeds that projection by injecting
character snapshots and a case would only assert what the test injected. It is now a §5 limit with the
two hops that stand behind it (the projection's source, and the native apply having destroyed the carried
object), instead of reading as proven.

**F10 — nit: the MANIFEST described stage 3 as still pending** in the stage 1 and stage 2 rows. Both rows
now say it landed and point at this cycle's page.

**F11 — nit: the two new words were not linked to the glossary at first use** in `mod-api.md`, a page that
otherwise links every registered word inline. Both pages now link `instance id` and `applied item` at
their first use (English: `glossary.md`; Chinese: the same, with the registered first-use spelling).

## What the review could not check at all

- `dotnet format` (the review was instructed not to run it, since it rewrites files), so "format clean" is
  reported by the cycle's own run and not independently confirmed.
- Anything needing the game process: `LimbItemTransform` and its three patch bodies, and what a real ice
  pack apply looks like on screen — which is also why F2's correction rests on the decompiled branch plus
  the design record rather than on a run.
- Whether `IModPlayerInventory` end to end excludes an applied item (F9).
- The date convention: the machine clock and HEAD both read 2026-10-11 while this cycle's pages are dated
  2026-10-14 (stage 1 and stage 2 carry the same offset). The reviewer judged it a pre-existing habit
  rather than this change's defect and did not count it as a finding; it is recorded here so the next
  reader does not have to rediscover it.
