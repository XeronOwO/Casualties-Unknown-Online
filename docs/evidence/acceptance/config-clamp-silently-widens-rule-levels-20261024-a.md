# Acceptance record — a hand-edited rule value lands on the declared fallback

- Ticket: `config-clamp-silently-widens-rule-levels` — verdict: **pass**, moved to `done/`.
- Batch: `20261024-a` — a single-host probe, one launch per case (the config entry is read at plugin
  start, so each case restarts the host).
- Commit: `2dfe2071` · Deployed artifact: `0.1.0+2dfe207183e1e880fe9feb59b510d2960510ab1e` —
  `tools/verify-deploy.ps1` exit 0 (`clamp-verify.log`), and the probe re-printed the identity in its own
  log (`clamp-green.log`, first line).
- Run: 2026-10-24 02:29, this session's clock · Host: physical machine.
- Artifacts: `clamp-green-console.log`, `clamp-green.log`, `clamp-green-config-backup.cfg`,
  `clamp-green-rules-*.json`, `clamp-green-state-*.json`, `clamp-green-verify-deploy.txt`,
  `clamp-green-session-environment.txt` in the directory named by `acceptance-artifacts-dir`.
- Method: `.acceptance/20261024-c/accept-config-clamp.ps1` edits the config file's `[HostRules]` lines,
  starts ONE host client, waits for the plugin endpoint, and reads `host-rules-read` — the value the
  running process is acting on (the recipe reads `HostRulesService.Current`, which with no announcement
  is the options object built from the config), never the file. The stock values were restored and the
  client closed afterwards; the game process count is zero.

| # | Case | Verdict | Evidence |
|---|---|---|---|
| 1 | A capitalised spelling the build does not accept: `RemoteBackpackAccess = Deny`, `NativeBindingParity = Require` | **pass** | `clamp-green-rules-capitalised.json`: `access=deny parity=Warn ok=true` — the access rule now reads the fallback instead of `allow`, and the parity rule reads `warn` (the documented default) instead of `allow` |
| 2 | A typo: `RemoteBackpackAccess = unconcious`, `NativeBindingParity = strict` | **pass** | `clamp-green-rules-typo.json`: `access=deny parity=Warn ok=true` |
| 3 | The clamp order is the census's, both rules at once, and the canonical values still read as themselves | **pass by test** | `HostRuleValueTextTests.ChoicesForClamp_PutTheConservativeValueFirst` (`deny,allow,unconscious_or_dead` / `warn,allow,require`), `TheBoundAcceptableList_ClampsAnUnrecognisedTextToTheFallback` (BepInEx `AcceptableValueList.Clamp`: `Deny`/`unconcious` → `deny`, `Require`/`strict` → `warn`, recognised values untouched), `AConservativeValue_IsDeclaredExactlyForTheTwoNamedSets` — 38/38 focused (`.acceptance/20261024-c/clamp-focused3.log`) |
| 4 | The prose states what BepInEx does, and the parity nuance is not hidden | **pass by reading** | the ticket's review section names every location; both configuration pages (en/zh) say the value lands on the declared fallback and name the miscased-`require`→`warn` level; the pair hashes are recorded in `docs/standard/alignment.txt` and the alignment gate is green |

## Before / after

- Pre-fix artifact `0.1.0+77510106…`: the same hand edit (file `Deny`/`Require`) was read as
  `access=allow parity=Allow` (`.acceptance/20261024-c/clamp-red.log`, red).
- Fixed artifact `0.1.0+2dfe2071…`: `access=deny parity=Warn` (`clamp-green.log`, green).

## Limits

- **One probe host, no member.** The rules are host-authoritative; a member receives the announced
  enum set and is unaffected by the clamp, so the single-host probe is the surface that changes.
- **Case restarts.** The entry is read at plugin start; the two cases are two launches. A hot-reload
  path (edit while the game runs) was not driven — BepInEx re-reads the file and the same clamp runs,
  but that is code reasoning, not this run's evidence.
- **The file was restored**, and the acceptance leaves the install at the stock `allow`/`warn`.
- **The typo case names the access value `unconcious`** (a plausible misspelling) and the parity value
  `strict` (a non-member word); neither is a value the build accepts.
- **`MinimumLevel` and `Language` were not probed**: their clamp first entry equals their own fallback
  (`Information`/`en`), so no fail-open is possible; the ticket's Limits record them as out of scope.
