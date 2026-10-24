# UI text lives in embedded JSON resources, and a mod can add a language

- Status: Todo — opened 2026-10-25 at the user's direction: "text like this, the multilingual kind, why isn't it an embedded
  JSON resource instead of hard-coded? And leave an interface for mods so other languages can be added. Open a separate
  ticket for it."
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

- [ ] **Step 1 — the files.** `en.json` and `zh.json` generated from the current dictionaries, key-for-key, embedded as resources; a first loader test proves the counts match the old tables.
- [ ] **Step 2 — the loader.** `LocalizationCatalog` reads the embedded JSON into the same shape the service already consumes; `LocalizationService` keeps its public behaviour and its fallback order (client language → English → caller's literal).
- [ ] **Step 3 — the gates.** The catalogue gates read the JSON; the key-set comparison is between the built-in files; a malformed file fails by name.
- [ ] **Step 4 — the mod interface.** `RegisterCatalog(language, json)` in `Abstractions`, gap-filling only, with a unit case for a mod language and a case proving a built-in key cannot be overwritten.
- [ ] **Step 5 — the picker.** The UI's language selection lists `SupportedLanguages` instead of the two hard-coded options.
- [ ] **Step 6 — the docs.** The contributor page that tells a translator how to edit a file, and a mod's page for adding a language.
- [ ] **Step 7 — deploy and read.** A client on Chinese and a client on English show the same window text from the files, and a mod-shipped language appears in the picker.

## Non-goals

- Translating the console's command syntax (the console ticket's boundary: verbs stay English).
- A translation service, download or hot-reload from the network — files ship with the build, mods ship with theirs.
- Reworking the `T(key)` call shape: keys stay the contract.
