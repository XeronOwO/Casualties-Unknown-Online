# A typo in a rule's config value silently widens it

- Status: Todo — found by the separate review of the `/gamerule` change (2026-10-23); pre-existing.
- Priority: Medium-High
- Category: Configuration / host rules / fail-closed
- Source: the review decompiled the referenced BepInEx 5.4.20.0 (`ConfigEntry<T>.set_Value` calls
  `ConfigEntryBase.ClampValue`; `AcceptableValueList<string>.Clamp` returns `AcceptableValues[0]` when the
  text is not an exact acceptable value), so a config file value BepInEx does not recognise is replaced by
  the list's FIRST entry before CUO ever reads it.
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

## Acceptance

- A hand-edited `RemoteBackpackAccess` the build does not accept (a typo, and the capitalised spelling) is
  read as `deny`, and a hand-edited `NativeBindingParity` as `warn` — read on the deployed client from the
  values the client is acting on, not from the file.
- The plugin comments and both configuration pages state what BepInEx actually does, with the clamp
  behaviour and the chosen fallback named.
- The chosen shape keeps the lists bound from the host-rule census (the `/gamerule` change made the census
  their one source), and a test pins the fallback at the seam that can reach it.
