# Host metal-scrap block placement sound not heard on guest

- Status: Todo
- Priority: Medium
- Category: Block placement / character audio sync
- Source: User report (2026-09-06) — when the host, while holding metal scrap, places a block into the world, the guest client does not hear the placement sound.

## Goal

Make the host's metal-scrap block placement sound audible to the guest, and verify the reverse direction and third-party view during the implementation cycle.

## Reproduction

1. Host and guest are in the same session.
2. Host holds metal scrap in hand.
3. Host places it into the world as a block.
4. Observed: the guest client does not hear the placement sound.

## Acceptance criteria (to be refined during implementation)

- Guest hears the same one-shot placement sound when the host places metal scrap.
- Reverse direction (guest places -> host hears) is checked if it uses the same path.
- Third-party view is checked via the host relay if applicable.
- No per-frame audio stream or voice chat introduced; a dedicated one-shot sound event is preferred.
- Solo / no-session behavior is unchanged.

## Investigation notes (open)

- Identify the native audio source for metal-scrap block placement.
- Check existing one-shot sound event paths (`CharacterSoundMsg`, building/item sound sync) and whether this placement sound already has a reportable boundary.
- Check whether the same defect family affects other held-material block placements, not only metal scrap.
- Confirm whether the sound is a host-only native local effect or should ride the existing remote character/item presentation path.

## Non-goals

- Not adding voice chat or continuous audio streaming.
- Not inventing a new audio protocol unless the existing one-shot event family cannot carry this sound.
