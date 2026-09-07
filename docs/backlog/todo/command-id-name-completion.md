# Command completion for the ID system: id/name search

- Status: Todo
- Priority: Medium
- Category: Tooling / Console / Mod API

The CUO command system should complete ID-system values by both id and name.

Requirements:

- While entering a resource/id argument, the user can type either the id
  (`fentanyl`) or the localized/common name (`芬太尼`; `芬太` as a prefix).
- The completed suggestion must be the canonical namespaced id, for example
  `cu:fentanyl`, not the raw input alias.
- This should reuse the ID vocabulary described in
  [Namespaced ID system](id-system-namespaced-ids.md).
- Pinyin search is a separate follow-up (see
  [Pinyin search mod](pinyin-search-mod.md)); this ticket covers only id/name
  matching.

Not started; record only.
