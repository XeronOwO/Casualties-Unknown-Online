# Giving an item into another player's backpack is not the native transfer

- Status: Todo — opened 2026-10-25 from the user's report: "为什么我把物品转移到他人背包里,没有音效啊,正常物品移动都有类似摩擦衣服的音效的", followed by
  the standard it must be held to: "我要的是尽可能还原原生的效果,为什么你要偷工减料".
- Priority: High
- Category: Cross-player item transfer / native parity
- Source: user report (2026-10-25).

## Goal

A give into another player's backpack IS the game's own transfer: the same native call the local move makes, on a real item and a
real body slot, so every side effect the game produces — the friction/clothing sound, the item's presentation, the container
refresh, the wear strip — happens BY CONSTRUCTION. The missing sound is one symptom of the transfer not being native; this ticket is
about the call path, not about playing a clip.

## Current behaviour (to be established by the first batch)

Not yet read. What is known from the code that exists: the give gesture releases a dragged item over a slot of the DISPLAYED backpack,
the clone's proxies are built with their colliders disabled ("never pickable/blocking", `CloneInventoryRenderer`), and the release is
turned into a remote intent before/while the native body runs (`PlayerCameraDragUsePatch` brackets `HandleReleaseDragging` and captures
the native calls). Whether the native `Container.LoadItem`/`TryPerformInventoryAction` actually RUNS against the proxy (so its sound and
presentation fire) or is replaced by the intent is the first thing to read.

Two outcomes, and they lead to different work:

1. **The native call runs** and the sound is missing for another reason (muted proxy audio source, clip not on the proxy's prefab) —
   then the fix restores the native source, not a new one.
2. **The native call is replaced** by the intent path — then the transfer must be re-plumbed so the native call runs with the patch
   answering the proxy's body/slot (the way `InvButtonBodyPatch` answers `get_body` from the focused clone), and the sound comes back
   with it.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The operator's client shows the SAME native call in the log for a local move and for a give (the function the game itself runs), captured in one batch |
| 2 | The transfer sound is present on the operator's client at the moment the item lands, read by the same instrument for the local move and the give (audio capture, or the game's own clip trace) |
| 3 | The target's own client shows the item in its slot with the game's own presentation, and the target's client plays whatever it normally plays when its inventory changes |
| 4 | A refused give plays the game's own deny sound, not the transfer sound — again from the native path |

## Limits

- A proxy transfer cannot invent the target's own audio: what the TARGET hears follows its own client's rules, and the ticket says
  which client each row was read on.
- If the native call turns out to be unreachable for a proxy (a private method or a hard collider dependency), the ticket must record
  that evidence and the exact difference from the native effect before any substitute is designed.
