# The root README as a language pair, and English first in the documentation entry

- Status: Todo — **raised 2026-10-11 by the user**, in their own words: "根目录的 README.md 也做成中英双语的
  两份文档，可以通过超链接切换语言。docs/README.md 改成先英语后中文，考虑到对国外友人更友好点，因此英语
  优先。根目录 README.md 中，我使用过的模型新增 DeepSeek V4.1 Flash，这个是目前的主要开发使用的模型了".
  The user offered it either as this ticket or as a side task of the cycle running when they asked; it is filed
  here and worked as its own documentation cycle, so the code cycle it interrupted keeps its own commit.
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

## Non-goals

- Not a new documentation block and not a translation of the repository's docs: the `docs/en` / `docs/zh`
  trees and their registries are untouched.
- Not a rewrite of the README's content: the English page keeps its sections, its wording and its links, and
  the Chinese page says the same things.
- Not a licensing or acknowledgement change: the licence, the third-party names and the disclaimer are
  restated in both languages exactly as they are.
