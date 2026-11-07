# Acceptance — native character fields after a continue (batch 20261107-x)

Ticket: `docs/backlog/done/save-native-character-field-parity.md` (S3.4b).
Run: 2026-11-07 23:48–23:53, host desktop, deployed `0.1.0+71f65c4f` with the acceptance
channel; world `w-20261107-58cb`.

## Fixture

The ticket's three rows were the last ones without a machine reading: the wound window's
identity text, the death screen's calorie counter and the happiness history each render a
value the game's own UI writes, and the earlier record called them a user check. They are
readable on the client that owns the screen, so the fixture is:

1. `ui-fixture-seed` stages the three values through the game's own setters — `WoundView.SetCharDetails(180, 42, 777, 9)`
   (the same entry `SaveSystem.cs:440` uses), `PlayerCamera.caloriesConsumed = 4321`, and
   `Body.lastHappiness = {-50 … -5}` — then reads them back:
   `versionText "UNI-HEALTH v2.09"`, `bodyStatText "180cm | 42y | #777"`, `cInfo 180,42,777,9`, `calories 4321`,
   `window "-50,-45,-40,-35,-30,-25,-20,-15,-10,-5"`.
2. `/save` takes a mid-run cut: world `w-20261107-58cb`, revision 714, 10 files. The live
   character row carries the staged values (`nativeFields.characterInfo [180,42,777,9]`,
   `caloriesConsumed 4321`, `lastHappiness` the seeded window rotated by the game's own updater).
3. `leave-world` then `continue-run` (the game's own `PreRunScript.LoadRun`); the world
   restores from the live snapshot.
4. `ui-fixture-read` on the same client reads the game's own displays back.

The first read after `continue-run` was taken before the restore's second pass had run (it
showed the prefab's placeholder text and zeroed fields); the restore's second pass writes
after the world generation and the position gate. The reading below is the second one, taken
after the restore settled — a fixture timing caveat, not a product finding.

## Readings (after the continue)

| row | surface | reading |
|---|---|---|
| 1 | wound window identity | `versionText "UNI-HEALTH v2.09"`, `bodyStatText "180cm | 42y | #777"`, `cInfo 180,42,777,9`, `firmwareVer "v2.09"`, `specimenId 777` — the stored four values, through the native `SetCharDetails` |
| 2 | death-stats calorie counter | after `/kill @p` the death screen's counter reads `4321cal` (the stored value), not `0cal` |
| 3 | happiness history | `window "-10.7,-7.8,-5.2,-2.7,-0.1,-50,-45,-40,-35,-30"` — the seeded history is present after the continue (the game's own updater has begun shifting new values in), not ten zeros |

Rows 4 and 5 (the refused/malformed rows and the named gap) were already machine-verified on
2026-09-11 by `CharacterNativeFieldPolicyTests` and the archive round trips.

### Frame

`capture-frame name=native-fields-20261107` captured the client's own 1280×720 frame with the wound
window open after the continue; the crop of the panel header (x 8–268, y 20–98, enlarged 4×) reads
`EXPERIMENT` / `180cm | 42y | #777` / `UNI-HEALTH v2.09` — the same three strings the text-component
reading returned, so row 1 is judged from the opened, enlarged picture as well as the reading.

## What this settles and its limits

- Rows 1–3 pass on the deployed build, read from the game's own text components — the same
  values the player sees — rather than from a frame or the archive.
- The reading is single-client by nature: the three surfaces belong to the client that owns
  them, and a second client's screen cannot show another player's wound window.
- The death reading needs the character dead; the run continues afterwards, so the fixture
  kills it last.
- The recipes are acceptance tooling: no production code changed, no deploy was made for them.
