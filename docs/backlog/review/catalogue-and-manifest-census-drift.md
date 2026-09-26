# Two censuses that drifted from reality: the catalogue's unreferenced keys, and the selfcheck MANIFEST

- Status: Review
- Priority: Low-Medium
- Category: Localisation catalogue / evidence index hygiene
- Source: The independent adversarial review of the Online UI console-page removal (2026-09-26) measured both while checking that cycle's "no unused localisation key remains" claim and its evidence links.
- Related: `docs/backlog/review/remove-the-online-ui-console-page.md`, `docs/evidence/selfchecks/MANIFEST.md`, `docs/evidence/selfchecks/tooling/catalogue-and-manifest-census-selfcheck.md`, `src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs`

## The gaps

**1. The catalogue carries keys no source file reads.** A census of every declared key against every
`T("…")` read under `src/`, `tests/` and `tools/` (build output excluded, comments stripped) reports
81 declared-and-unreferenced keys outside the `tab.`/`console.` family the console-page removal
cleaned: 59 `medical.*`, 8 `prefs.*`, 3 `member.*`, 3 `hud.*`, 3 `common.*`, and one each of
`chat.send`, `home.already_in_lobby`, `ip.session_title`, `lobby.title`, `players.section`. Spot
checks (`medical.heart_rate`, `hud.title`, `chat.send`, `common.yes`, `lobby.title`) find no reader.
The delivered census corrected that list in two ways: the eight `prefs.*` keys are read through the
interpolated key space `ctx.T($"prefs.color.{name}")`, and `players.section` is a false positive
(`OnlineUiPlayersDrawer` reads it) — the real orphan in that family is `players.not_in_lobby`,
which the delivered 73-key set carries instead.

**2. The selfcheck MANIFEST is not the complete per-file index it claims to be.**
`docs/evidence/selfchecks/README.md` calls `MANIFEST.md` "the complete per-file index", while the
manifest carries 198 table rows against 262 files under `docs/evidence/selfchecks/*/`. Two known
missing rows were added by hand (`ui/cuo-launcher-idle-fade-selfcheck.md` and
`ui/online-ui-console-page-removal-selfcheck.md`); the rest of the drift is untouched, and the
manifest's own intro still points at `docs/selfchecks/`, a path that no longer exists.

## Why they are one ticket

Both are the same failure shape — an index that asserts completeness while reality moved on — and the
same census pass found them. Neither is fixed in the console-page cycle on purpose: deleting 81 keys
is a catalogue-wide decision (some may be reserved for a surface under construction or for mods), and
completing the MANIFEST is a per-file current-versus-historical judgement over 64 missing rows.

## What done looks like

1. Every unreferenced key is either deleted or classified, with the reason and the surface that will
   read it recorded here.
2. The MANIFEST lists every file under `docs/evidence/selfchecks/` or stops claiming to be complete,
   and its intro names the real path.
3. The census is reproducible from this ticket: declared keys versus `T("…")` reads, build output
   excluded, comments stripped — one command, re-runnable next cycle.

## What landed (2026-09-26 cycle)

Both censuses are gates now, and the drift they measured is gone in the same change.

- **The catalogue declares exactly what the source reads.** The 73 declared-and-unread keys the review's
  exploratory census found are deleted from both tables: 59 `medical.*` (the rejected CUO IMGUI medical
  panel's vocabulary; the native `WoundView` remote focus is the only medical surface, per
  `review/remote-player-medical-panel.md`), 3 `member.*`, 3 `hud.*`,
  3 `common.*`, `chat.send`, `home.already_in_lobby`, `ip.session_title`, `lobby.title` and
  `players.not_in_lobby` (superseded duplicates of keys their surfaces read instead, such as
  `home.already_in_session` and `players.not_in_session`). The catalogue shrank from 561 to 415 lines and
  every surviving key is read.
- **The census counts reads and follows every shape the tree builds.** `LocalizationCatalogueGateTests`
  scans the product source's accessor calls (`T`/`F`/`Format`), the key space an interpolated argument
  opens (`ctx.T($"prefs.color.{name}")` keeps the eight `prefs.color.*` keys), the key handed to a helper
  whose `labelKey` parameter the helper resolves itself (the two `admin.rule_*` keys its number and parity
  helpers translate) and a declared key-producing helper (`KindKeyOf`'s three `worlds.kind.*` literals).
  Every shape carries a positive and a negative sample, so a mention in a comment, a log literal or prose
  cannot stand in for a read, and the census keeps a floor.
- **The MANIFEST is the complete per-file index it claims to be.** 66 rows were added (the 65 the red
  listed as unindexed plus this cycle's own new sheet) against the 264 markdown files now under
  `docs/evidence/selfchecks/` (the manifest itself excepted), the intro's retired
  `docs/selfchecks/` path is corrected, and `SelfcheckManifestGateTests` fails on an unindexed file, a
  duplicate row, a row whose file is gone, and a manifest that stops naming the tree it indexes.
- **The gates are registered** in `docs/evidence/normative-gates.md` and decision 229 records the two
  invariants.

No wire protocol, save shape, host rule or rendered string changed: the deleted keys had no reader, and the
census is the proof.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | Declare a catalogue key nothing reads | The gate fails and names the key | `LocalizationCatalogueGateTests.EveryDeclaredKey_IsReadByProductSource` + its orphan sample |
| 2 | Read a key the catalogue does not declare | The gate fails and names the key | `LocalizationCatalogueGateTests.EveryKeyTheSourceReads_IsDeclared` + `TheGate_FlagsAReadKeyThatIsNotDeclared` |
| 3 | Read a key through a helper or an interpolated key space | It counts as a read, so a live key is never swept | `TheCensus_ReadsAKeyHandedToAKeyCarryingHelper`, `TheCensus_ReadsAnInterpolatedKeySpace`, `TheCensus_ReadsAKeyReturnedByADeclaredHelper`; the 73-key deletion left every live key behind |
| 4 | Add a self-check file without a MANIFEST row | The gate fails and names the file | `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` + its samples |
| 5 | Build, tests, gates, format | Green | selfcheck §6 |

## Evidence

- Selfcheck: `docs/evidence/selfchecks/tooling/catalogue-and-manifest-census-selfcheck.md`
- Gates: `tests/CasualtiesUnknownOnline.NormativeGates.Tests/LocalizationCatalogueGateTests.cs`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SelfcheckManifestGateTests.cs`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SourceScan.cs`
- Catalogue: `src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs`
- Index: `docs/evidence/selfchecks/MANIFEST.md`

## Limits

- The census scans `src/` alone: a key read only by a test is dead product data by design, and test
  fixtures embed read-shaped text that must not count as a read. The surface is therefore narrower than
  the exploratory census (which also scanned `tests/` and `tools/`), which is why the orphan set measured
  73 rather than 81 — the eight-key difference is the `prefs.color.*` family the interpolation shape keeps.
- The census is text, not a compiler: an accessor reached through a receiver it cannot resolve (a
  pass-through such as `OnlineUiContext.T`, an echoed parameter) is a recorded limit in the loud
  direction — a key only such a site can produce is reported as unread — while a key such a site produces
  without declaring it stays invisible until it renders as its own key text.
- The MANIFEST pass is a triage: presence, uniqueness, resolvability, and each sheet's own header for the
  current-versus-historical status. It does not re-audit the contents of the 65 sheets it indexed.

## Non-goals

- Not a catalogue redesign and not a translation change: the English and the Chinese dictionary stay
  in step.
- Not re-opening the console-page removal's own two-key deletion, which is landed and pinned.
