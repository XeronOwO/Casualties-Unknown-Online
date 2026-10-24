# UI text lives in embedded JSON resources, and a mod can add a language

- Status: Done — closed 2026-10-25: the UI's text is an embedded JSON data file per language (English as the fallback), the
  catalogue is read by a strict loader, the gates read the data, a mod registers a language through `RegisterCatalog` and the
  picker lists it, and the contributing page pair documents all of it. Batch `20261025-e` then read the deployed client's own
  service (record: `docs/evidence/acceptance/localization-live-read-20261025-e.md`): the host answered `zh` with `首页` and the
  Chinese console-ready sentence while the guest on the same build answered `en` with `Home` and the English one, both listing
  `supported: en,zh`. The one thing left is a PIXEL read — that the overlay drew those sentences — which is a frame-level
  residual for the user, the same capability gap the visual rows carry. Opened 2026-10-25 at the user's direction.
- Priority: Medium-High
- Category: Localization / architecture / mod surface
- Source: the user, while the console's bilingual slice was being built — the same objection applies to the whole
  `LocalizationCatalog`: two `Dictionary<string, string>` literals in C# (`English`, `Chinese`) inside the Runtime assembly.

## What is wrong today

`src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs` holds **every** user-visible string as C#
dictionary literals — one table in English, one in Chinese, in the same file, compiled into the assembly. Consequences:

- **A translator edits C#.** Changing a sentence is a code change through the full build/gates cycle, and a translation
  review cannot be diffed as data.
- **Adding a language is a code change**: a third dictionary, a third branch, a new build.
- **A mod cannot add a language at all** — `LocalizationCatalog` is `internal`, and `ILocalizationService` offers no way to
  register a catalogue.
- **The two tables drift silently**: nothing but a gate's key comparison ties the English and Chinese sets together, and a
  missing Chinese entry is invisible until a Chinese client shows English.

## Design

1. **One JSON file per language, embedded in the Runtime assembly**: `Localization/en.json`, `Localization/zh.json`, added
   as `EmbeddedResource` and read once by the catalogue loader. The shape is flat (`{ "window.title": "…" }`) — the keys are
   already stable identifiers and a nested shape would only buy ceremony. Encoding UTF-8 without BOM, no comments (JSON has
   none; the reasoning lives here).
2. **English is the fallback language**, as the catalogue already documents: a lookup asks the client's language first, then
   English, then the caller's own literal. A language that is missing a key therefore shows English, never a blank.
3. **A mod adds a language through an interface, not a build**: `ILocalizationService.RegisterCatalog(string language, string
   json)` (or a stream overload) lets a mod ship its own `xx.json`; registration fills gaps and never overwrites a built-in
   key, so a mod cannot redefine the UI's own text. The service exposes `SupportedLanguages`, and the client's language
   selection lists them — the UI's language picker then offers a mod's language with no code change.
4. **The gates move onto the data**: `LocalizationCatalogueGateTests` learns to read the JSON; the existing "every declared
   key is read by product source" and "the two tables have the same keys" rules become JSON comparisons (each built-in file
   against English), plus a new one — every embedded file is valid JSON with unique keys.
5. **Migration is mechanical and key-preserving**: the dictionaries move into the two files unchanged (keys and text), the C#
   tables are deleted, and no call site changes (`T("…")` keeps working). Anything that reads `LocalizationCatalog.English`
   directly moves to the loader.

## Run sheet

- [x] **Step 1 — the files.** `en.json` and `zh.json` generated from the old dictionaries key-for-key (244 each, equal counts), embedded as resources in the Runtime assembly.
- [x] **Step 2 — the loader.** `LocalizationCatalog` reads the two embedded files through `LocalizationJson`, a strict reader that refuses a malformed file, a duplicate key or an unterminated string by name; the public behaviour and the fallback order (client language → English → caller's literal) are unchanged.
- [x] **Step 3 — the gates.** `LocalizationCatalogueGateTests` reads the JSON (key-set equality between the built-in files, every declared key read by product source), and the console-page pin folds both files the same way.
- [x] **Step 4 — the mod interface.** `ILocalizationService.RegisterCatalog(language, json)` in the service, gap-filling only (a built-in entry is never overwritten), malformed input refused by name; four unit cases pin it.
- [x] **Step 5 — the picker.** The preferences page builds its choices from `SupportedLanguages`, so a registered language appears without a code change.
- [x] **Step 6 — the docs.** The contributing page pair (`gates-and-rules.md`, both blocks, alignment re-recorded) now explains the JSON catalogues, the key rules, the console's keyed lines and `RegisterCatalog` for mods.
- [x] **Step 7 — deploy and read.** Deployed twice since (artifacts `0.1.0+adbf2828…` and `0.1.0+7605a140…`, `verify-deploy` exit 0), and batch `20261025-e` read the live service on both clients (record `docs/evidence/acceptance/localization-live-read-20261025-e.md`): the host answered `zh` with 首页 and the Chinese console-ready sentence while the guest on the same build answered `en` with Home and the English one, both listing `supported: en,zh`. The PIXEL half — that the overlay drew those sentences — is the user's frame-level residual, named rather than claimed.

## Non-goals

- Translating the console's command syntax (the console ticket's boundary: verbs stay English).
- A translation service, download or hot-reload from the network — files ship with the build, mods ship with theirs.
- Reworking the `T(key)` call shape: keys stay the contract.
