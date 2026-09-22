# Bilingual documentation: layers, pairing, and the maintenance contract
> **Superseded (2026-09-22):** the pairing contract below is replaced by [`docs/AGENTS.md`](../AGENTS.md); this page is kept only until the migration finishes.

This page defines which documents exist for people rather than for agents, which of them are
maintained in two languages, and how a paired document stays consistent. It is the policy the
pairing gate implements (`tests/CasualtiesUnknownOnline.NormativeGates.Tests/HumanDocsPairingGateTests.cs`);
the renderings themselves live in [terminology.md](terminology.md), which is the only source of
truth for wording.

## The layer rule

One question decides where a new document belongs: **is it needed in order to DO the work, or in
order to UNDERSTAND the product?**

| Layer | Index | Language | What it holds |
|---|---|---|---|
| Agent instruction | `AGENTS.md` | English only, unpaired | rules, gates, paths, identifier-first detail; the auto-loaded chain |
| Reference / evidence | `README.md` in that layer | English only, unpaired | architecture, API, features, decisions, evidence, backlog |
| Human guide | `README.md` + `README.zh.md` | paired English + Chinese | what the mod is, how to play it, how it is built |

An agent document never restates the human guide, and a human guide never restates the reference
layer — it links into it. The two guide levels are level 1 (player guide, `docs/guide/`) and
level 2 (developer guide, `docs/developer/`); the reference layer is level 3 and stays English.

## Paired scope

Exactly two directories are paired:

- `docs/guide/**` — the level-1 player guide;
- `docs/developer/**` — the level-2 developer guide.

Everything else is English only, including this page, [terminology.md](terminology.md),
[../README.md](../README.md), [../AGENTS.md](../AGENTS.md) and every document under
`docs/architecture/`, `docs/api/`, `docs/features/`, `docs/decisions/`, `docs/evidence/` and
`docs/backlog/`. A `.zh.md` or `.i18n.yaml` anywhere else in the repository is a gate error, not a
style question: the inverse check scans the repository's own file set — tracked files plus
untracked-and-not-ignored ones — rather than only the directory a pair happens to live in.
`docs/README.md` points a Chinese reader at [../guide/README.zh.md](../guide/README.zh.md) rather
than carrying a counterpart of its own: it is the entry into all three layers, and only the two
guide levels are paired.

## The pairing contract

- **A pair is three sibling files** in one directory: `foo.md` (English), `foo.zh.md` (Chinese)
  and `foo.i18n.yaml` (the consistency record). No locale directories, no second tree, no
  translated copy of a reference document.
- **Both sides carry equal authority.** Either may be authored first and the counterpart is
  written from it; the binding requirement is that they say the same thing. The switcher is
  navigation, not a statement that one side outranks the other.
- **Switcher.** The English side carries `English | [中文](foo.zh.md)` immediately after its H1;
  the Chinese side carries `[English](foo.md) | 中文` in the same position.
- **The block sequence matches:** heading depth and order, paragraph count, list kind and item
  count, the shape each list item carries (links, inline code, bold spans), table row count, column
  count and per-cell shape, verbatim code blocks, and link targets in order (inline links, images
  and reference-style definitions). A relative link inside the paired corpus is localized (`foo.md`
  on the English side, `foo.zh.md` on the Chinese side); a link into the English-only reference layer
  keeps the same target on both sides. Translated words are never compared — a checker that cannot
  read cannot compare them — so two items of identical shape can be reordered, and two cells of
  identical shape can be swapped, without failing the gate.
- **The record.** `foo.i18n.yaml` holds the full git blob hash of each side as of the last time the
  two were confirmed to say the same thing:

  ```yaml
  foo.md: 3f786850e387550fdab836ed7e6dc881de23001b
  foo.zh.md: 89e6c98d92887913cadf06b2adb97f26cde4849b
  ```

  Blob hashes, not commit hashes: `git hash-object foo.md` reproduces the recorded value for a file
  edited in the working tree, so consistency is a pure content comparison.

## What a green gate proves — and what it does not

The gate checks the contract mechanically: every topic in scope has all three files, each side's
current blob hash equals the recorded one (editing one side without re-recording the pair names
that pair), both switchers are present and well-formed, the block sequence and link targets match
as described above, no `.zh.md`/`.i18n.yaml` exists outside the paired scope, and the workspace
instruction chain stays inside its budget.

Its limit, stated plainly: **a green run means the pair was confirmed consistent at these exact
contents, not that the counterpart is accurate, well-termed or natural.** The check counts and
compares language-independent markers; it never reads either side, so a paragraph can say something
different, a heading can keep its depth while its meaning drifts, and two same-shaped list items or
table cells can trade places — all of that passes. Language quality stays a review duty, exactly as
`docs/evidence/normative-gates.md` records for the English-only rule.

## Maintenance workflow

When either side of a pair is edited:

1. patch the counterpart minimally against the edited side's diff — the recorded hashes recover
   the last confirmed text of both sides, which is what makes a minimal patch possible;
2. run the focused gate (`dotnet test ... --filter "FullyQualifiedName~HumanDocsPairing"`);
3. re-record both hashes with `git hash-object <file>` in the pair's `.i18n.yaml` — that record is
   the reviewable act of confirming the pair.

Re-translating a whole document is forbidden. Routine work is done directly by the working agent
after reading [terminology.md](terminology.md); there is no separate translation tool, repository
or build step.

## Language rules

- Chinese means **Simplified Chinese**. No Traditional counterpart is maintained; a word that has
  no settled rendering goes into [terminology.md](terminology.md) rather than being invented per
  page.
- Committed content is English by default (`AGENTS.md`, *Document Scope & Classification*). The
  two guide levels are the deliberate, bounded exception: the deliverable targets Chinese-reading
  players and contributors, so a paired page is committed in both languages at once.
- Code identifiers, type names, file paths, commands, config keys and wire field names are never
  translated — they are the vocabulary the code uses, and a reader who looks them up must find the
  same string.

## The instruction budget

A nested instruction file shares one 65,536-byte workspace budget with the root `AGENTS.md`, and
the loader only discovers it once a file operation touches that scope. `docs/AGENTS.md` therefore
carries rules for this subtree only, links the detail, and both its own size and the size of the
instruction chain are pinned by the gate — a large nested file would push the root instruction file
out of the loader's baseline, which is a worse failure than the one it fixes.
