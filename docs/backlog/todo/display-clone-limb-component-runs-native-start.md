# A peer's limb component on a display clone runs the game's own Start and throws every frame

- Status: Todo — **found 2026-10-11 by the acceptance run of batch `20261011-b`**, while it set up the
  Stage-3 removal scenario: the moment a peer's limb carried a splint, the client that had the remote
  medical panel open on that peer threw a NullReferenceException **every frame** until the panel was closed.
- Priority: High — a per-frame exception storm on a normal, user-reachable surface (open the medical panel
  on a teammate who is splinted), thousands of log lines a minute, and the panel's own icon bookkeeping ends
  in a broken state. It did not corrupt state in this run, but it is one live object away from doing so, and
  it makes the client's log useless for anything else while it lasts.
- Category: Remote medical / display clone / native component behaviour
- Related: `todo/medical-removal-limb-component-not-applied.md` (the same component-state plumbing, opposite
  direction: what a removal must take OFF a body), `review/remote-medical-stage-3-other-actions.md`,
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` (this run's record).
- Source: the agent's own three-client run, batch `20261011-b`.

## What the run showed

The run put a splint on the host's view of a guest in three steps, and the storm begins at the third:
the guest's limb was seeded at 12:26:23, the host's native panel was opened on that peer at 12:26:34, the
renderer created a display body for the peer at **12:26:47.162** (`Remote body created for <guest>`), and
the first exception is at **12:26:47.737** — one frame later. From then on, the host logged this per frame
(`host-splint-icon-nre-storm-archive.log.txt`, recovered from the rotated archive `2026-10-11-8.log.gz`):

```
[ERR] [CasualtiesUnknownOnline.Plugin] [Unity:Exception] NullReferenceException
UnityEngine.Component.GetComponent[T] () (at <c39a522eee05469b8171a6cfb646c59>:0)
SplintLimb+<>c.<Start>b__4_0 (Limb x) (at <ab887c07f51841c98ea015c9c29ae1af>:0)
OnLimbWound.Update () (at <ab887c07f51841c98ea015c9c29ae1af>:0)
```

- **6065 `NullReferenceException` lines** in that client lifetime: **4473** before the panel was closed
  (last at 12:28:07.530), **0 while it was closed** (12:28:08 → 12:28:19), and **1592** after it was
  reopened (first at 12:28:19.544). The close and the reopen are the run's own two driver calls
  (`host-woundview-off.json` at 12:28:07, `host-reopen-view.json` at 12:28:19), so the correlation is the
  panel's own per-frame work, not the component's `Update`.
- **391 more** in a 25-second window during the real splint-removal drive
  (`host-splint-removal-and-storm.log.txt`).
- The two sandbox clients logged **zero** in the same runs — their current logs and their own rotated
  archives, which cover the same window (`guest-alt-exception-census.txt`).

## Root cause

The display path materialises a real game component on a body CUO only renders:

- `src/CasualtiesUnknownOnline.GameAdapter/RemoteMedicalCoordinator.cs` applies a peer's limb state to the
  operator's/observer's display body through `LimbComponentStateCodec.Apply` (both the snapshot site and the
  live medical-state site), and that codec does `limb.gameObject.AddComponent<SplintLimb>()` for a limb the
  message says is splinted.
- That is the **game's own component**, so the game's own `SplintLimb.Start` runs and registers a wound-view
  icon: `WoundView.view.AddImageToLimb(this.limb, Resources.Load<Sprite>("Special/splintIcon"), false, (Limb x)
  => !x.GetComponent<SplintLimb>());` (`reversing/Assembly-CSharp/Assembly-CSharp/SplintLimb.cs:15`; the same
  file's `Start` also sets `this.limb.splinted = true`, which is why `seed-splint`'s own read shows the flag
  still false in the frame it was written).
- The display body is rebuilt under the focus (the run logs `Remote body created for <guest>` one frame
  before the first exception), so the icon list holds a limb that is no longer alive; `OnLimbWound.Update`
  then calls the icon's removal predicate, `GetComponent<T>` runs on a null `Limb`, and the exception is
  thrown inside the panel's own `Update` — every frame, for as long as the panel stays open.

## What the fix has to do

Keep native component behaviour off display clones:

- the display path needs the component STATE (the wound view already renders `splinted`, and the fact table
  carries the limb facts), not a live instance of the game's component; either the display apply stores the
  state without instantiating the component, or the component is created in a way that cannot register
  native behaviour (no `Start`, no wound-view icon).
- The removal half of the same plumbing (`todo/medical-removal-limb-component-not-applied.md`) must be
  written with this in mind: a destructive apply on a display clone must not run the game's own teardown
  either.
- A regression case can pin the shape without a game: the display path must not `AddComponent` a type whose
  `Start` touches `WoundView.view`.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | with a peer whose limb carries a splint, one client keeps the remote medical panel open on that peer for a full minute and logs zero `[Unity:Exception]` lines | any exception appears |
| 2 | in the same window the panel still shows the splint (the icon/state is not lost by the fix) | the display stops rendering the splint |
| 3 | the same holds for a tourniquet (`TourniquetScript`) | the sibling component floods |
| 4 | the same holds for a chilled limb (`ChilledLimb`), whose own `Update` also acts on the limb it is attached to | the third component of the codec's set floods or mutates a display limb |
| 5 | leaving and re-entering the focus on that peer (the display body is rebuilt) stays quiet | the storm returns on the rebuild |

## Limits

- The run observed the storm only on the client that had the panel open; a client that never opens the panel
  was quiet in the same session, which is consistent with the stack but does not prove the icon is the only
  trigger after a display-body rebuild.
- The log evidence is a stack plus the open/close/reopen count correlation, not a debugger: the null `Limb`
  is read from the game's own predicate, and the candidate mechanism (the rebuilt display body) is the run's
  own observation of `Remote body created` one frame before the first storm.
- One unrelated startup exception (a `TypeLoadException` about `System.ComponentModel.DataAnnotations`) is in
  the same archive; it is counted separately in the digest and is not this defect.
