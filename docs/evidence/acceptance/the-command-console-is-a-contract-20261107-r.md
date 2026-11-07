# The command console is a contract — row 13, one key in two languages (batch 20261107-r)

- Batch `20261107-r`; artifact `0.1.0+8a44059903d39eeb8213c864e29f2ea962096063` (`verify-deploy`: "Deployment matches this tree's build output"); lobby `109775243748231437`; clients: host 18590 (`[UI] Language = zh`), guest Steam1 18591 (`[UI] Language = en`), alt Steam2 18592 (`[UI] Language = zh`); `launch-clients.ps1 -OutDir .acceptance/20261107-r` — all three up 19:24:30, all in world 19:24:49.
- The language is the client's own CUO setting (`BepInEx/config/CasualtiesUnknownOnline.cfg`, `[UI] Language`), read at start; the guest's was set to `en` before the batch launched. No client was restarted inside the batch.
- Row 13: "The same refusal reads in the same language on a member who received it over the wire: one key, two clients, two languages, same meaning."

## Reading A — the same key rendered on each client (`console.answer.session_facts`, `/whoami`)

- Host (zh, translated): `role=Host SteamID=76561198281246659 host=76561198281246659 session=true`
- Guest (en): `Role=Guest SteamId=76561198863287957 Host=76561198281246659 SessionActive=true`

## Reading B — a refusal the HOST decided, delivered over the wire (`item.answer.refused_unknown_definition`, argument `zzz`)

- The guest typed `/give @p zzz`; the host log at 19:27:22.861 names the member and the refusal:
  `[Give] 76561198863287957 -> 76561198863287957 refused: No item definition 'zzz' is known to this game — a verb names a definition from the game's own item table.`
- The guest's own console history (`console-read count=3`) read the same sentence in English:
  `Refused: No item definition 'zzz' is known to this game — a verb names a definition from the game's own item table.`
- The host's own `/give @p zzz` read (zh, translated): "no item definition named 'zzz' is known to this game — a verb names a definition from the game's own item table."
- The key exists in both catalogues with the same placeholder: `src/CasualtiesUnknownOnline.Runtime/Localization/en.json:435` and `zh.json:435` (`'{0}'`).

## Verdict

Row 13 PASS — one key, two clients, two languages, same meaning. The refusal is composed where the reason is known
(the host's own item table) and rendered where it is read (the member's own catalogue).

## Limits

- The fixture uses the client's config at startup. The in-game language dropdown could not be driven by the driver's
  `click` action (it applies `ControlInvoked`; a dropdown needs a selection, and the call returned an eval error) — a
  tool gap recorded here, not a product claim.
- After the readings the guest's config was reverted to `zh`, so the machine's sandbox client keeps its usual language.
