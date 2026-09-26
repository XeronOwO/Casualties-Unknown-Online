# Two censuses that drifted from reality: the catalogue's unreferenced keys, and the selfcheck MANIFEST

- Status: Todo
- Priority: Low-Medium
- Category: Localisation catalogue / evidence index hygiene
- Source: The independent adversarial review of the Online UI console-page removal (2026-09-26) measured both while checking that cycle's "no unused localisation key remains" claim and its evidence links.
- Related: `docs/backlog/review/remove-the-online-ui-console-page.md`, `docs/evidence/selfchecks/MANIFEST.md`, `src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs`

## The gaps

**1. The catalogue carries keys no source file reads.** A census of every declared key against every
`T("…")` read under `src/`, `tests/` and `tools/` (build output excluded, comments stripped) reports
81 declared-and-unreferenced keys outside the `tab.`/`console.` family the console-page removal
cleaned: 59 `medical.*`, 8 `prefs.*`, 3 `member.*`, 3 `hud.*`, 3 `common.*`, and one each of
`chat.send`, `home.already_in_lobby`, `ip.session_title`, `lobby.title`, `players.section`. Spot
checks (`medical.heart_rate`, `hud.title`, `chat.send`, `common.yes`, `lobby.title`) find no reader.

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

## Non-goals

- Not a catalogue redesign and not a translation change: the English and the Chinese dictionary stay
  in step.
- Not re-opening the console-page removal's own two-key deletion, which is landed and pinned.
