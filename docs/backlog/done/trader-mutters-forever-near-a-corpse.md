# The trader keeps talking while a dead player lies beside it

## Design

`docs/design/surfaces/remote-body-presentation.md` owns what a peer may hear from another body. Read before the fix; the fix writes its conclusion back there.

- Status: Done — fixed and measured (2026-11-02, 17:58). The cause was the one in the mechanism section: a body''s talker calls `Talker.PromptTraderResponse`, which forwards to `PlayerCamera.main.lastTrader.PromptResponse`, and `PromptResponse` starts a `DelayedTalk` coroutine per call — so a REMOTE CLONE whose condition poll fired repeatedly made the local trader answer over and over. The talk patch''s rule ("a clone''s speech is reported, never performed") now covers this second door: the forward is refused when the talker belongs to a remote body. Read on the deployed build with a dead player lying beside the trader: **2 trader bubbles in twelve seconds**, against 329 in five before the fix.
- Priority: Medium (audible defect: the typing sound never stops while a corpse is near the trader)
- Category: Audio / speech replication
- Source: user report, 2026-11-02.
- Related: `done/` speech sync records; `review/suppressed-native-call-sounds-stay-unheard.md` (the clip recorder this was read with).

## What is read

- Rate: 329 `[Speech]` lines in five seconds with the dead body beside the trader, ~20/s from 200 units away with the body still there, 2 in five seconds after the body was moved away.
- Both clients play the `speech` clip continuously (measured with the one-shot recorder: `sound-log mode=read match=speech` answers a dozen entries per three seconds on each side).
- The text is a body line, and the talker is trader-attached, so the speaker is the trader's own `Body.talker` re-rolling a condition line — the trader's DIALOGUE goes through `TraderScript.Convo`'s own calls, whose strings are locale lines like `tradeemeet`/`traderseegun`.

## What is not yet read

Which method re-fires the trader body's talk each frame, and whether our trade-state replication feeds it (the host's trader is native and authoritative; the guest's copy is written from snapshots). The diagnostic log answers the first half on the next occurrence; the second half is a code read of `Talker.Update`'s condition loop against what we write into a trader.

## Why it is a defect and not the game

The native talk loop has its own cooldown (`Talker` talks a condition line, then waits); a trader muttering twenty times a second is not a state the game reaches without something re-arming the condition or the cooldown on every frame. The fix is to find that re-arm, not to mute the trader.
## The mechanism, read from the native chain (2026-11-02, 16:20)

`Talker.PromptTraderResponse(textType)` is the bridge from a body's own condition line to the trader's answer:

    if (PlayerCamera.main.lastTrader) PlayerCamera.main.lastTrader.PromptResponse(textType);

and `TraderScript.PromptResponse` starts `DelayedTalk` (a 0.9-1.5 s wait, then `talker.Talk(...)`). Every call starts ANOTHER coroutine, so a caller that fires per frame produces a stream of overlapping answers — which is exactly the shape the log shows (20-65 bubbles/s, each a different random response line).

The caller is the body talker's own condition poll (`Talker.Update`), whose conditions are gated by `newTalkTime` and `lastLineType`. Which talker keeps re-firing is what the deployed diagnostic answers: it names the talker's GameObject, its body and the text on every trader bubble.

## A second reading from the same session: the host sends ~200 KB/s

The traffic monitor on the host, with the same setup, prints `send ... 202-228 KB/s` against `recv ~9 KB/s` while nothing is happening in the world. A speech storm alone does not explain that size, so the round also has to name which message family carries it — the same instrument (`NetworkTrafficMonitor`) exists and only needs a per-family breakdown to answer.