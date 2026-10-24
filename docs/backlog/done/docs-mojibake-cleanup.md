# Four tracked docs carry code-page mojibake

- Status: Done — the pass ran 2026-10-24 over six files (the four named here, the source file, and a test
  file the closing scan found); every tracked text file that carries the families now carries them only in
  this ticket's own quotations and in the Chinese pinyin data.
- Priority: Low
- Category: Documentation / repository hygiene
- Source: found 2026-10-18 while editing `done/member-report-bounds.md` (its prose reads `鈥?` where the sentence needs
  an em dash); `done/give-item-to-a-player-command.md` had already recorded the same artifact as "the file's
  repo-wide pre-existing encoding artifact, left to the encoding cleanup" (its review finding 7), so this ticket
  gives that cleanup a target.
- Related: `docs/AGENTS.md` (the document standard — committed prose is English, `docs/zh/` is the Chinese block),
  `docs/standard/document-census.txt` (one of the four, its comment header included)

## What the bytes say

Measured on this tree at `2afbb61f` by scanning the English docs for a high character directly followed by `?` — the
shape every one of these round trips leaves — and then confirming each hit by its context:

| File | Damage |
|---|---|
| `docs/backlog/done/member-report-bounds.md` | 148 spots: `鈥?` (em dash / ellipsis), `鈫?` (arrow), `脳` (`×`) |
| `docs/backlog/done/item-and-entity-data-commands.md` | 64 spots of the same families, plus `鈭?` (`−`) |
| `docs/decisions/active.md` | 39 spots, including `鈥揈` for "–E" in "Phase A–E" |
| `docs/standard/document-census.txt` | 4 spots in its comment header, e.g. `# Document census 閳?one row …` |

The shape is a UTF-8 file read through the system code page and written back: `—` (E2 80 94) became `鈥?`, `→`
(E2 86 92) became `鈫?`, `×` (C3 97) became `脳`. The last byte of each character was consumed by the `?`, so the
original is not recoverable from the corrupted text alone.

Two other English docs read as hits by the same scan and are NOT damage: `done/document-corpus-audit.md` and
`done/give-item-to-a-player-command.md` quote a Chinese review comment, and a Chinese sentence ending in `?` is the
ordinary question mark. Leave them alone.

Found 2026-10-22 during the remote-backpack access-policy review: the same artifact is also in one tracked
SOURCE file — `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` (39 occurrences of the
`鈥?`/`鈫?` family). It is source, not a document, and it is named here so the cleanup's carrier owns it instead of
a later cycle rediscovering it; the per-sentence method below applies unchanged. `docs/decisions/active.md` measured
41 occurrences on the same scan (the table's 39 were measured at `2afbb61f`).

## Why it is worth fixing

These are documents a reader opens directly: `docs/decisions/active.md` is the record every design argument cites,
and the ticket is the next cycle's input. The damage never changes a word, but it makes every dash and arrow
unreadable, and it survives edits — a line touched by a later edit tends to acquire another occurrence, which is how
this was found (the 2026-10-18 edit on the ticket pasted one in and had to correct it).

## What the fix has to do

- Restore each spot from its MEANING, never with one global replace: `鈥?` stands for any E2 80 xx character — an em
  dash, an ellipsis, a curly quote — and the `?` no longer says which; the edited ticket mixes two real `…` with its
  em dashes, so the two cases are not interchangeable. `鈫?` → `→`, `脳` → `×`, `鈭?` → `−` and `鈥揈` → `–E` are
  unambiguous; `鈥?` needs the sentence read.
- Never touch the `docs/zh/` block: its CJK is content, not damage.
- After the pass, re-read every affected sentence end to end. A bulk diff is not a proofread, and a wrong guess
  silently changes a punctuation mark in a normative record.

## How to verify

Scan the four files for high characters followed by `?` (and, for the plain `脳`/`鈭?` cases, for the character alone)
and read every remaining hit. This ticket quotes the corrupted sequences on purpose, so it is NOT part of the pass and
a scan must exempt it. No gate checks these characters, so the evidence is the proofread diff itself; if the pass
lands with a prevention check, it is a scan for the corrupted byte sequences over the English tree only (the `zh/`
block is exempt by construction).

## The pass (2026-10-24)

- **What was restored.** `docs/decisions/active.md` (39 spots: 37 em dashes, 2 `–E`, 1 arrow),
  `docs/standard/document-census.txt` (4 spots: three em dashes and `§6`), `docs/backlog/done/member-report-bounds.md`
  (139: 125 em dashes, 22 arrows, 2 `×`, two private-use strays that followed a dash),
  `docs/backlog/done/item-and-entity-data-commands.md` (62: 58 em dashes, 4 arrows, one private-use stray),
  `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` (36 em dashes), and
  `tests/.../Session/HostRuleAnnouncementTests.cs` (4 em dashes — a file the original inventory missed).
- **How each spot was read.** Every occurrence's sentence was read before any replacement: the damaged
  `鈥?` is a clause join in every one (the `—` sits between two clauses), the `鈫?` are arrows in code paths and
  state names, the `鈥揈` are `–E` in "Phase A–E", the `鈥?` in the census header and the `搂6` are the header
  punctuation; the file's intact `…` (13 in the member-report record) are all inside quoted log excerpts and
  were left untouched. **One limit, recorded honestly:** the corrupted byte loses WHICH E2 80 character it was,
  so the two `…` this ticket's Problem names cannot be told apart from the dashes by the bytes; each spot was
  therefore read by its sentence, and none of the restored spots reads as an elision.
- **Two spots the edit tool could not reach.** In the two `done/` records one occurrence is a `鈥` followed by a
  private-use byte instead of `?`; the text editor cannot express that character, so the pair was removed with
  an explicit UTF-8 string replace and the line was re-read (no BOM, no other byte changed). Every other edit
  went through the editor.
- **The scan after the pass**: `git ls-files`, restricted to `.md`/`.txt`/`.cs`/`.ps1`/`.json`/`.cfg`, finds the
  families only in this ticket (its own quotations, exempt) and in
  `src/CasualtiesUnknownOnline.PinyinSearch.Core/Search/pinyin_data.txt` (its CJK data, excluded by the
  ticket's own `zh/`-block rule — that file is data, not prose).
- **No prevention gate.** The ticket allowed one but did not require it; the scan surface would have to whitelist
  the pinyin data and this ticket, and a gate that cannot reach the exact characters without a whitelist is the
  shape the repository's gate rules warn about. The proofread diff is the evidence; the next pass, if one is
  ever needed, starts from the same scan.
