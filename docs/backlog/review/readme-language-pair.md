# The root README as a language pair, and English first in the documentation entry

- Status: Review — **raised and landed 2026-10-11 by the user**, in their own words: "根目录的 README.md 也做
  成中英双语的两份文档，可以通过超链接切换语言。docs/README.md 改成先英语后中文，考虑到对国外友人更友好点，
  因此英语优先。根目录 README.md 中，我使用过的模型新增 DeepSeek V4.1 Flash，这个是目前的主要开发使用的模型
  了". The user offered it either as this ticket or as a side task of the cycle running when they asked; it was
  filed while that cycle finished, then worked as its own documentation cycle with its own commit, so the code
  cycle kept its own. What landed is below.
- Priority: Low-Medium
- Category: Documentation / project metadata
- Related: `docs/README.md` (the switcher page whose order this changes),
  `docs/AGENTS.md` (the pair rule this borrows: same page set, one language switch, both sides edited in the
  same change), `docs/standard/alignment.txt` (the pair registry — it covers the `docs/en` ↔ `docs/zh` blocks
  today and does NOT list the root pages), `AGENTS.md` (committed content is English; the human docs are the
  paired exception), `docs/en/contributing/documentation-standard.md`
- Source: the user's message above, 2026-10-11.

## What to do

1. **Split the root README into a dated pair**: `README.md` stays the English page (it is what a repository
   visitor and GitHub's own default land on) and `README.zh.md` becomes the Chinese one, written for Chinese
   readers rather than sentence by sentence. Each carries a one-line language switch at the top, the shape
   `docs/README.md` already uses.
2. **`docs/README.md` goes English first**: the switcher block's prose and its two sections put English above
   Chinese, because the page's job is to route a stranger who does not read Chinese.
3. **The model list gains the current main model** — `DeepSeek V4.1 Flash` — in the same "Development" section
   of both READMEs. The existing line stays accurate rather than being rewritten: the code was written
   primarily with DeepSeek V4 Flash/Pro, the architecture design contribution is GPT 5.6 Sol's, and DeepSeek
   V4.1 Flash is what development runs on now; Claude Code stays the pre-Harness record it is.
4. **Decide the pair's registration**: the alignment registry covers the `docs/en` ↔ `docs/zh` blocks, so the
   root pair is either added to it (with its own gate reach) or named as deliberately outside it — the answer
   is recorded where the reader of either page can find it, not left implicit.
5. **The two versions stay fact-for-fact equal**: the same sections, the same links and commands, the same
   acknowledgements and disclaimer. A translated page that drops a section is a documentation defect, not a
   translation choice.

## Why it is a ticket rather than an edit

The change is small but it touches a rule rather than a page: the repository currently has exactly one
language-switch surface (`docs/README.md`), and a second one at the root means deciding what governs it —
which registry, which gate, and whether the root pair is part of the human documentation block or beside it.
That decision outlives the edit, so it is recorded with it.

## What landed (2026-10-11)

- **The root README is a pair**: `README.md` (English, what a visitor and GitHub's default land on) and
  `README.zh.md` (Chinese, written for Chinese readers rather than sentence by sentence), each opening with
  the switch line `**English** | [中文](README.zh.md)` / `**中文** | [English](README.md)`.
- **`docs/README.md` is English first**: its title, its prose and its two sections now lead with English,
  because the page's job is to route a stranger who does not read Chinese.
- **The model list names the current model**: both READMEs carry "*All project code has been written
  primarily with DeepSeek V4 Flash/Pro; DeepSeek V4.1 Flash is the model development runs on now; a small
  amount of architecture design was contributed by GPT 5.6 Sol*" (Chinese: the same facts), with the
  Claude-Code-to-Harness history unchanged.
- **The registration decision, recorded rather than left implicit**: the root pair is NOT added to
  `docs/standard/alignment.txt`. That registry's gate binds the `docs/en` ↔ `docs/zh` block pair — it derives
  its required rows from those two trees and checks each row's two hashes — and the root README is outside
  the human documentation block by construction (`docs/AGENTS.md` §1 defines the blocks; the root README is
  the repository's front page). Registering it would put a page the gate cannot reach into a record whose
  whole value is that a gate reports its drift, so the pair's consistency is kept by the same rule the
  registry exists for — both sides edited in one change — and by the two pages pointing at each other.
- **The two versions say the same things**: the same sections in the same order, the same links (all 14
  local targets resolve), the same acknowledgements and the same disclaimer.

## Non-goals

- Not a new documentation block and not a translation of the repository's docs: the `docs/en` / `docs/zh`
  trees and their registries are untouched.
- Not a rewrite of the README's content: the English page keeps its sections, its wording and its links, and
  the Chinese page says the same things.
- Not a licensing or acknowledgement change: the licence, the third-party names and the disclaimer are
  restated in both languages exactly as they are.
