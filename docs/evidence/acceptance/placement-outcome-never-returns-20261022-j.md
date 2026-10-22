# Batch 20261022-j — placement-outcome-never-returns row 6, second staging attempt

- Row: row 6 only. Artifact: `0.1.0+e3cdc117…` (the review's fix commit, deployed and verified before the
  clients started).
- Staging that was tried: the receiver's body change via the game's own console command
  (`item-provide type=bandage mode=create`) issued to the receiver's client immediately after the giver's real
  right-button release. The gesture under test stayed the real pointer drag; the body change is the same kind of
  declared setup substitution the earlier batches used, just timed at the release instead of before it.

## What happened

The give completed normally (query 1, answer `slot 5`, commit, apply):

```
[00:18:25.136] [Placement] asked … where item bandage (id 983868491715) can go (query 1, slot 5, container 0).
[00:18:25.155] [Placement] answered query 1 for item bandage (id 983868491715): slot 5
[00:18:25.169] [Take] … takes bandage (id 983868491715) from …
[00:18:25.215] [PlayerInteraction] placed bandage (id 983868491715) in local slot 5.
```

The console command reached the receiver after the transfer had already placed the item, so it answered
`no-free-slot` (an error in its own right, not the row's scenario).

## What this establishes

The answer→apply window on the fixed artifact is about **60 ms** (`00:18:25.155` → `00:18:25.215`), not the
~540 ms the h batch's row 3 measured on the earlier artifact (that reading included the slower pre-fix
re-report path). A driver's round trip is ~100-250 ms, so no command can be aimed inside a 60 ms window from
outside the receiving client. Batch `20261022-i` established the other half: the receiver's own pointer gesture
cannot overlap the giver's on one physical pointer and one foreground window.

## Verdict

**UNPROVEN**, and now for a measured reason: the window is 60 ms, shorter than any external call and shorter than
the two-client input switch. The row needs either a deliberately delayed in-client action (a seam the acceptance
rules refuse as evidence) or a user ruling that the behaviour test plus the wired return is the accepted reading.
The ticket stays in `todo/` and records this.

## Limits

- Nothing else was read; rows 1-5 remain the `20261022-h` readings.
- The clients were closed at the end of the attempt.
