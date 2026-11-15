# Character selection and the solo unlock store (1.0)

This page records the game's own character-picker rules and the boundary CUO puts around them in a multiplayer session.
It is the design page for the picker; the run-start gate and the character wire are separate mechanisms.

## What the player does

In solo play the player opens the pre-run screen and sees three character portraits. The two later characters are
dimmed and cannot be clicked until the player has won a run as the character before them. In a multiplayer session the
player expects the same picker to offer all three characters, because the session is not the single-player progression;
a friend who has not finished the game must still be able to pick the character they want. After the session, the solo
locks must be exactly where they were.

## What the game runs

- **The unlock store is `PlayerPrefs`, and the key is `wonchar<character-1>`.** The first character has no key; the
  second is gated by `wonchar0`, the third by `wonchar1` (`reversing/release-1.0/Assembly-CSharp/PreRunScript.cs:56-61`).
  `PreRunScript.Awake` reads each key and sets the portrait button's `interactable` to false when the key is zero.
- **The tooltip is added at Start.** The same key decides whether the locked portrait gets the unlock tooltip
  (`PreRunScript.cs:164-172`).
- **The portrait is dimmed every frame.** `PreRunScript.HandleCharacterSelect` checks the key again and lerps a locked
  portrait toward gray; the selected portrait gets the white highlight (`PreRunScript.cs:244-267`).
- **Selection itself is not gated.** `PreRunScript.SelectCharacter` writes `PreRunScript.characterSelected` and refreshes
  the text; it does not read the unlock key (`PreRunScript.cs:270-275`). The button's `interactable` flag is the gate.
- **The choice becomes the body at spawn.** `PlayerCamera.SpawnCharacter` stores the choice as `lastchar` and spawns the
  prefab for that index (`PlayerCamera.cs:799-800`); the index table is `CharacterIndex` (0 Experiment, 1 Milky, 2 Dune).
- **A won run writes the unlock.** `WorldGeneration.SetGameWon` writes `wonchar<charType>` and unlocks the character
  achievement (`WorldGeneration.cs:1358-1362`). It is called by the ending paths, including `EscapeEnding`
  (`WorldGeneration.cs:1352`).

## CUO's rule

- **A session views every character as selectable.** While the client is in a session, the pre-run screen treats all
  three portraits as unlocked: no store write, no store-derived gate, but the same native picker and the same native
  selection path. Outside a session the store's own rule is untouched.
- **A session win must not write the character unlock.** The character-unlock write and its achievement are skipped
  while a session is active; the session's ending is not the single-player progression.
- **The boundary is the character unlock, not every preference.** `lastchar` is a selection preference, and the ending
  flags (`didbiopod`, `didescape`, `didmerc`) are run/ending progression; this rule does not claim them. The store keys
  this page protects are `wonchar0` and `wonchar1`.
- **The wire does not change.** The chosen index already rides the character snapshot (`CharacterDataMsg.Character`).

## What the player sees

- In a session, all three portraits are bright and clickable, including the ones that are locked in solo play.
- The chosen character spawns as itself, and the other client renders that character.
- After the session, the same portraits are locked again if the player has not won those characters in solo play.
- A session win does not unlock anything in the solo picker.

## Acceptance rows

1. A client with no unlock in a session sees all three portraits interactive and can select any; the spawned body is the
   chosen character.
2. The other client's copy renders the chosen character.
3. The `wonchar0`/`wonchar1` values before and after a session win are identical.
4. Outside a session, a character without the unlock remains unselectable.

## Acceptance evidence (2026-11-15)

- Focused `character-choice` PASS on `0.1.0+4d37f810e`: session picker
  `buttons=true,true,true`, `tooltips=false,false,false`; host body `charType=2`, member body
  `charType=1`; host `remoteCharTypes=1`, member `remoteCharTypes=2`; host `wonchar0=1,
  wonchar1=0` before and after `ending=Merc`; after leaving the session the picker reads
  `buttons=true,true,false`, `tooltips=false,false,true`.
- Final full suite on `0.1.0+4d37f810e`: 34 passed, 0 failed, wall 477.7 s; the `character-choice`
  row took 64.4 s with host 21 warnings/0 errors and member 34 warnings/0 errors.

## Gaps

- The native portrait color targets are noted in the source but not reproduced here; the view must use the game's own
  targets rather than invent a palette.
- The exact ending call sites for Biopod and Merc are not listed here; the one character-unlock write is `SetGameWon`.
