# A typo in a rule's config value silently widens it

- Status: Review — implemented 2026-10-24 (shape 1); awaiting the green acceptance on the deployed
  artifact (the red was observed on the pre-fix build, `.acceptance/20261024-c/clamp-red.log`).
- Priority: Medium-High
- Category: Configuration / host rules / fail-closed
- Source: the review decompiled the referenced BepInEx 5.4.20.0 (`ConfigEntry<T>.set_Value` calls
  `ConfigEntryBase.ClampValue`; `AcceptableValueList<string>.Clamp` returns `AcceptableValues[0]` when the
  text is not an exact acceptable value), so a config file value BepInEx does not recognise is replaced by
  the list's FIRST entry before CUO ever reads it. The review of the fix verified the same IL in the
  DEPLOYED core (5.4.23.4): the file path is `ConfigFile.Reload` → `SetSerializedValue` → `set_Value` →
  `ClampValue`, so the premise holds on both versions.
- Related: `done/gamerule-host-rule-command.md` (its review found this; it binds the same lists from the
  host-rule census, which this fix must keep), decision 263 (the rules are the host's), the `[HostRules]`
  and `[Respawn]` sections of `docs/en/reference/configuration.md` and `docs/zh/reference/configuration.md`.

## Problem

`src/CasualtiesUnknownOnline.Plugin/PluginDependencyRegistrar.cs` binds the two three-way rules with
`AcceptableValueList<string>("allow", …)` and documents the conservative fallback behind it:

- `RemoteBackpackAccess`: `ParseRemoteBackpackAccess` answers `Deny` for an unparseable value, with the
  comment "an unparseable access value fails CLOSED (deny) rather than widening the access a typo was meant
  to narrow".
- `NativeBindingParity`: `ParseNativeBindingParity` answers `Warn`, with the comment "a hand-edited config
  file bypasses the entry's AcceptableValueList, so an unparseable parity value falls back to the
  conservative default (warn)".

Both comments are false for a file value: BepInEx's clamp replaces the unrecognised text with the list's
first entry, which is `allow` for both rules. `RemoteBackpackAccess = Deny` (or any typo, or a case variant
such as `Warn`/`Require`, which widen a level; `Allow` clamps to the same rule) is read as **`allow`** — the
open rule — and the
fallbacks above are unreachable for file values. The clamp is ordinal, so it also bites a host who edits
the file with different capitalisation even though the runtime text forms are documented as
case-insensitive.

`docs/en/reference/configuration.md` and the zh page repeat the same assumption ("a hand-edited file
bypasses the first clamp") for the save ranges as well; the save-option comment in the plugin says it too.
The numeric `AcceptableValueRange` clamp is desirable (an out-of-range number becomes the bound, and the
console cannot request one), so the defect is the CLOSED-SET clamp and the prose built on the opposite
belief.

## The question

Which shape makes an invalid value fail closed AND keeps the config file honest?

1. **Put the conservative value first in each list** (`deny` for access, `warn` for parity): the clamp then
   lands on the documented fallback. The order is also what BepInEx shows as the acceptable list, so the
   file's own description would read in a less natural order.
2. **Drop the two `AcceptableValueList`s** and let the runtime parsers decide: `Deny`/`Warn` become
   reachable again, no clamp runs, and the generated description loses the machine-readable list (the
   description text already spells the levels out).
3. **Move the clamp under CUO's control** (read the raw string, parse, write the canonical value back):
   the most work, and it makes the options monitor the parser's owner.

The numeric range stays as it is (bound from the census, clamped, and the clamp is the desired behaviour).

## What landed (2026-10-24) — shape 1, without a second declaration

- **The census declares each named set's fallback.** `HostRuleCensus.Row` gained `ConservativeValue`,
  set to `deny` (`RemoteBackpackAccess`) and `warn` (`NativeBindingParity`); a flag or number row
  declares none, and a census test asks the question of EVERY row (`AConservativeValue_IsDeclaredExactlyForTheTwoNamedSets`).
- **The acceptable list is the census's, reordered for the clamp only.** `HostRuleValueText.ChoicesForClamp`
  returns the same set as `Choices` with the declared fallback first, and throws if a declared value is
  not one of its row's choices. Both `config.Bind` calls in `PluginDependencyRegistrar` use it; the
  console's own order stays `Choices` (`allow` first). Chosen over shape 2 because the lists were bound
  from the census by the `/gamerule` change and dropping them would undo that accepted decision; over
  shape 3 because it keeps the options monitor out of the parsing business.
- **The parser's fallback is the same census value, not a second literal.**
  `ParseRemoteBackpackAccess`/`ParseNativeBindingParity` parse the entry text, and on failure parse
  `row.ConservativeValue` through the same text form, throwing if the census declares something
  unparseable. The list's order and this path can no longer disagree.
- **The prose now states what BepInEx does, and names the parity nuance.** A hand-edited value outside
  the list is clamped BEFORE CUO reads it — a typo or a capitalised spelling lands on the declared
  fallback, never on the open `allow`; for parity that fallback is the documented default (`warn`), not
  the strictest level, so a miscased `require` becomes `warn` (one level looser than written). Fixed in
  both configuration pages, the two `*Text` doc comments, the plugin's `[Save]`/`Apply` comments,
  `SaveOptions`' class doc and `docs/architecture/save-archive-format.md` §7.
- **The red was observed before the fix.** With the deployed pre-fix artifact
  `0.1.0+77510106…`, a config hand-edited to `RemoteBackpackAccess = Deny` and
  `NativeBindingParity = Require` was read by the running host as `access=allow parity=Allow`
  (`.acceptance/20261024-c/clamp-red.log`, against the batch's `verify.log` identity); the parity value
  moving from `Warn` to `Allow` is the clamp's first entry, since the parser would have answered
  `Require` and its fallback is `Warn`.
- **Tests**: the BepInEx type itself is pinned — `new AcceptableValueList<string>(ChoicesForClamp(row))`
  clamped `Deny`/`unconcious` to `deny` and `Require`/`strict` to `warn`, and left recognised values
  untouched — beside the order/set cases, the no-fallback case, the per-row census invariant and the
  throw for a fallback outside its choices.

## Independent review (2026-10-24) — every finding and what changed

Verdict: 0 blocker, 2 major, 3 minor, 6 nits. The reviewer verified the clamp premise against the DEPLOYED
BepInEx 5.4.23.4 IL (not only the 5.4.20 reference): `AcceptableValueList<T>.Clamp` returns
`AcceptableValues[0]` after an ordinal `IsValid`, and the file path (`ConfigFile.Reload` →
`SetSerializedValue` → `set_Value` → `ClampValue`) applies it before any CUO parser. It reproduced build
0/0, the focused suite 37/37, the normative gates 777/777 and the full behaviour suite 6342/6342, and
could find no second config path that puts `allow` first (profile apply clamps through
`SetSerializedValue`; the Admin page and `/gamerule` write typed/canonical values; the announcement
carries enums). All findings are dispositioned below; everything was fixed in this commit.

- **MAJOR — "cannot widen" / "only ever tightens" was false for parity.** The accepted fallback for a
  hand-edited parity value is `warn`, which is one level looser than a miscased `require`; four places
  claimed the opposite (`PluginDependencyRegistrar`'s parse summaries, both configuration pages'
  "only ever tightens", `HostRuleCensus.Row`'s "fail-closed member", `ChoicesForClamp`'s "must be the
  fail-closed level"). **Fixed:** every one of them now says the value lands on the DECLARED FALLBACK —
  `deny` for access, `warn` for parity — never on the open `allow`, and names the parity nuance (a
  miscased `require` becomes `warn`). The chosen fallback is the ticket's acceptance, not re-decided.
  The census doc's "null for a rule whose value is not a named set" was also wrong (flags are named
  sets); it now says "null for a rule that needs no fallback (a flag or a number)".
- **MAJOR — the ticket's "plugin comments state what BepInEx actually does" was not met.** These live
  comments still asserted the bypass: the plugin's `[Save]` comment and `Apply` header, `SaveOptions`'
  class doc ("a hand-edited config file bypasses BepInEx's own range validation"),
  `docs/architecture/save-archive-format.md` §7, and both `*Text` docs' "the config reads it
  case-insensitively" (the entry clamps ordinals before the parser). **Fixed in every named location:**
  the range clamp applies to file values too; the second clamp is kept as the defence for a caller that
  builds `SaveOptions` directly; the `*Text` docs say the entry's list is ordinal and clamps before the
  parser, which itself is case-insensitive.
- **MINOR m1 — the tests pinned the helper, not the BepInEx seam.** **Fixed:** a new case builds
  `AcceptableValueList<string>` from `ChoicesForClamp` and asserts `Clamp` on `Deny`/`unconcious` →
  `deny` and `Require`/`strict` → `warn`, plus recognised values untouched. The test project already
  reaches `BepInEx.Configuration` through the Runtime reference.
- **MINOR m2 — the census invariant was backwards.** The old test started from rows that HAD a fallback
  and asserted a count of two, so a future access/parity row that omitted one would pass. **Fixed:**
  the test now asks every row whether its kind needs a fallback (`Assert.Equal(needsFallback, row.ConservativeValue is not null)`),
  then pins the two values and their first position.
- **MINOR m3 — the conservative level was declared twice.** The plugin's typed fallbacks were literals
  (`Deny`/`Warn`) that only a comment tied to the census. **Fixed:** both parse methods now take the
  census row and, on an unparseable text, parse `row.ConservativeValue` through the same text form,
  throwing if the census declares an unusable value — one declaration, two readers.
- **NITS.** The configuration pages now name `HostRuleCensus.ConservativeValue` and
  `HostRuleValueText.ChoicesForClamp` as the declarations (the sentence had pointed only at the plugin);
  the Source line records that the fix's reviewer verified the DEPLOYED 5.4.23.4 IL as well as the
  5.4.20 reference; the acceptance log is made self-contained by printing `verify-deploy … delivered
  artifact …` in `accept-config-clamp.ps1`; cited test counts name their filter
  (`FullyQualifiedName~HostRuleValueTextTests` → 38 after this fix, not the broader 90); the README row
  is updated when the ticket moves after the green acceptance; the `MinimumLevel`/`Language` lists and
  the Boolean entries were checked and are out of scope (no fail-open: their clamp first entry equals
  their own fallback), and the historical selfcheck is left as a point-in-time record.

## Acceptance

- A hand-edited `RemoteBackpackAccess` the build does not accept (a typo, and the capitalised spelling) is
  read as `deny`, and a hand-edited `NativeBindingParity` as `warn` — read on the deployed client from the
  values the client is acting on, not from the file.
- The plugin comments and both configuration pages state what BepInEx actually does, with the clamp
  behaviour and the chosen fallback named.
- The chosen shape keeps the lists bound from the host-rule census (the `/gamerule` change made the census
  their one source), and a test pins the fallback at the seam that can reach it.

## Limits

- The fallback is the RULE's declared value, not the strictest level: a miscased `require` becomes `warn`,
  a miscased `deny` becomes `deny` (the declared value), and a miscased `allow` becomes `deny`/`warn` —
  the rule never opens. Choosing `require` as parity's fallback would be a different design (a typo would
  refuse to admit members), which the ticket's acceptance deliberately does not choose.
- The clamp is BepInEx's and ordinal: a value differing only in capitalisation is unrecognised. The
  runtime text parsers remain case-insensitive for values that reach them by a path that did not read
  the entry (a caller that constructs the options), and those paths now share the census fallback.
- The `MinimumLevel` and `Language` lists are outside this ticket: their clamp first entry is also their
  own fallback (`Information`/`en`), so an unrecognised value is no worse than the parser's answer; a
  case-insensitive `debug` becoming `Information` is a usability nit, not a fail-open.
- The red observation was recorded from the deployed pre-fix artifact beside the batch's `verify.log`
  identity; the green run repeats it on the fixed artifact and prints the artifact line in its own log.
