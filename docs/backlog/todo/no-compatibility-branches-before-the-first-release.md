# No compatibility branches: the first release has not happened, so nothing is owed to old shapes

- Status: Todo — opened 2026-10-30 from the standing ruling (user): "the project has not released anything and the wire version does not increment, so any
  breaking change is acceptable — do not leave historical debt."
- Priority: High — every compatibility branch is a second implementation kept alive by a client that does not exist, and each one is a place the next change
  has to satisfy twice.
- Category: Architecture / release discipline / debt removal

## Design

Read `docs/design/model/item-custody.md` (the one-authority rule a leftover dual path violates) and `docs/decisions/active.md` decision 241 (the pre-release
wire baseline this ruling extends) before opening this ticket; the layer boundary a removal must not hollow out is `docs/architecture/current.md`.

`docs/decisions/active.md` carries the pre-release rule (decision 241's baseline and the standing ruling beside it): before the first official release
`ProtocolVersion.Current` is the frozen baseline and does NOT move, the handshake stays the only compatibility boundary, and a change is recorded by its
commit, its ticket and the sync-coverage matrix. Nothing in this tree has shipped, so no client, save, wire message or mod in the wild needs an old shape.

**What the ruling forbids, stated as a rule**: a branch, a type, a `[Obsolete]` member, a dual code path or a "for old data" fallback whose ONLY reason to
exist is an older version of this project. It does not forbid data normalisation that is part of the CURRENT format (a save written by today's build must
still load), and it does not forbid the game's own legacy surfaces — the game's API is not ours to simplify.

## What exists today and is debt by that rule (read, not remembered)

| Where | What it is | Why it is debt |
|---|---|---|
| `Abstractions` `ModConsoleCommand` + its registration path | the flat, pre-definition console command contract | superseded by the definition contract (`the-command-console-is-a-contract.md` decision 4); no mod has shipped outside this tree |
| `GameAdapter/GameAdapterBridge.cs` (`TryGetMarkerId` fallback) | "Legacy fallback: extremely old clone renders may still carry a domain item id from before the id-free display boundary" | a display path kept for an object shape this tree can no longer produce; it hides a real "no marker" case behind an `ItemInstanceId` read |
| the `[Obsolete]`/`deprecated`/`legacy` mentions across `src` | 99 matches, most of them prose about the game's own legacy modules or about a design that replaced an older one | not all are branches; the ticket's first job is to separate prose from code, so the sweep does not delete a sentence it mistook for a shim |

## The work

1. **Census first, delete second.** Enumerate every candidate with the reason it exists today: a call site that would break without it, or "nothing — it was
   for the old shape". The second kind is deleted in this ticket; the first kind is deleted only after its caller moves (and the caller's move belongs to that
   caller's ticket, named here).
2. **Each removal names who it was for** in the commit message ("kept for a client that never shipped", "kept for the pre-namespace id format"), so the
   history records the reason the branch existed and not only its disappearance.
3. **A gate, not a habit.** Once the tree is clean, a normative gate refuses a NEW `[Obsolete]` member or a `// legacy`/`// compat` branch that names no
   current format — the same shape as the existing architecture gates. A new compatibility branch then needs a decision recorded in `docs/decisions/active.md`
   and a published release to point at.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The census lists every candidate with its reason; the list is in this ticket and each entry says "deleted here" or "deleted with <ticket>" |
| 2 | No `[Obsolete]` member and no `// legacy`/`// compat` branch remains in `src` whose reason is an older version of this project — the gate is the proof |
| 3 | A save written by the previous build of THIS tree still loads (the current format's own normalisation is not what this ticket removes) — one session, the archive restored |
| 4 | A mod using the old console contract fails to compile with a name that says what to use instead (the definition contract), not with a missing-type error buried in a stack |
| 5 | The gate refuses a seeded `[Obsolete]` member and a seeded `// compat` branch, and passes the tree (its matcher is pinned with both samples) |

## Limits

- "The game's own legacy surfaces" are out of scope: `UnityEngine.InputLegacyModule`, the game's `legacy protobuf messages` mentions and the KrokMP reference
  tree are not this project's debt.
- A branch kept for a REAL current constraint (a save field that older sessions of the same run wrote, the game's own two API shapes) stays, and the ticket
  says which constraint — the rule forbids debt, not documentation of a live constraint.
- The release date (the game's 2026-11-09 official build) does not change this: nothing of ours ships before that, and the rule is re-read on that day.
