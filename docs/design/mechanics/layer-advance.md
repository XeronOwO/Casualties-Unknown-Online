# Layer advance: ending a layer and entering the next one

Reaching the end of a layer, and the choice to go down.

## What the player does

Reaches the bottom of a layer, where the game offers the way down, and chooses it. The player expects the whole party to
move together: whoever chooses the descent does not leave the others behind, and no fact from the layer being left
survives into the layer being entered.

## What the game itself does

The descent is the game's own layer transition: the world generates the next layer and the bodies are placed in it. In
co-op the choice must be one member's choice applied to a host-owned world.

## Rules and states

- The choice is the MEMBER's; the layer is the HOST's. A member who chooses the descent reports the intent; the host
  decides when the party moves, so two members choosing at once cannot generate two worlds.
- **Generation's random stream is ISOLATED, because the game's one stream is shared with everything else.**
  `GameAdapter/WorldGen/WorldGenRandomIsolation.cs`: the game "consumes UnityEngine.Random — one global stream — from many places
  at once: generation coroutines, `Body.Update` sound/particle effects … earthquake timers", and generation "is a cross-frame
  coroutine … that yields every ~100 columns, so without isolation the stream diverges between peers at every suspension point:
  host and guest start from the same captured `Random.state` … but the stream they continue from is polluted by
  frame-rate-dependent consumers in between".
- **The advance guard is the game's own, minus the clause that ties it to the local body.** `LayerAdvancePolicy.DecideDrive`
  is "the native entry's own guard (`WorldGeneration.ContinueRun`, `WorldGeneration.cs:1013`) minus its LOCAL-BODY clause":
  `!doingRegen && !generatingWorld && worldExists` are the clauses that ask "can this world take a regeneration right now?",
  "and the fourth — the local body standing at the layer's bottom — is the clause that ties the choice to the body that made
  it", which a member's admitted request must not be held to.
- **The panel's native trigger is a height, and the host replaces it with the living share.**
  `LayerPanelGate.cs`: "the native trigger asks one question — is the LOCAL body below `-halfHeight + 3.1`", which cannot
  answer "whether the HOST's body is among the finished (a dead host has no bod[y]) and how much of the LIVING group is down
  there", so the host "counts its own body (only while aliv[e]) … in-world member whose reported vitals say alive, and applies
  `LayerPanelVisibility`".
- **A layer-relative request carries the generation it was made in.** `LayerAdvanceRequestMsg` "carries the requester's
  kernel generation stamp, the same (run epoch, layer index) identity every layer-relative world report carries, so the host
  arbitrates it against the identity it stamps its own" — which is why a request from a layer the host has already left is
  refused rather than applied to the new one, and why the panel appears only on the client whose own body is at the bottom.
- **A layer's size is the game's own world-unit scale, and the corpus states it so labels cannot disagree.**
  `Runtime/World/WorldUnits.cs`: the game reports DEPTH "by multiplying world units by `0.3f`" (`WorldGeneration.cs`:
  `return -(this.body.transform.position.y - this.halfHeight) * 0.3f`) and converts back where it accumulates
  (`this.totalTraveled / 0.3f`), so "one world unit is 30 cm, and a layer is 2 × halfHeight units — with `halfHeight` of 512
  that is 1024 units, i.e. the 307 m a player reads as 'one layer is ab[out] …'".
- **Both of the panel's choices are the HOST's, and a member does not see them at all.**
  `src/CasualtiesUnknownOnline.Runtime/Session/World/LayerPanelChoicePolicy.cs` implements the user's 2026-10-24 ruling:
  "only the host" may press "Continue" (next layer) or "Save and exit"; on a member both are HIDDEN rather than refused,
  "because a control a player may not use should not be there to press". Solo play and the host keep both.
- **The layer's timer is the run setting `timelimit` in minutes, and it is what starts the radiation line.**
  `WorldGeneration.cs:258` sets `maxTimePerLayer = GetRunSettingFloat("timelimit") * 60f`, and `:861-863` activates the line when
  "`layerTimeSpent > maxTimePerLayer && !RadiationLine.line.active`" — so a party that lingers is pushed down by the game's own
  deadline, not by a CUO rule.
- **The game's own "Save and exit" writes its save; CUO's replacement deliberately does not.** `SaveAndExit()`
  (`WorldGeneration.cs:1024-1033`) sets `forceWalk = false`, raises `deepestlayer`, calls `IncreaseDepthByLayer()`,
  `SaveSystem.SaveGame()` and `PlayerCamera.main.ToMainMenu()`. CUO keeps the player-facing action but takes a CUO cut instead,
  which is the same independence the saves page states from the other direction.
- **Whether the host's panel appears is decided from the LIVING share, not from the host's own body.**
  `LayerPanelVisibility.cs`: "the native panel's own trigger is the LOCAL b[ody]", which "cannot answer the question when the
  host's body is dead or when the host is not the one at…", so the host decides from the group's state — `ShouldShow(living,
  atTheFloor, requiredShare)` is false with nobody alive, true with a share of 0 ("the no-threshold setting"), and otherwise
  shows when `bodiesAtTheFloor / living >= requiredShare`.
- **A refused advance is answered, and the answer can only come from the host.** `LayerAdvanceRefusedHandler` exists because
  "the member suppresse[s its] own descent when it asked, so this line is what tells it the layer did not move", and the
  channel is host-to-guest so "a forged answer cannot print attacker-chosen text on a member's log" — a refusal is a fact
  only the host may state.
- **The layer's own rarity numbers start neutral and may only be scaled by generation.**
  `src/CasualtiesUnknownOnline.GameState/Domains/World/RunRarityMultipliers.cs` fixes `Neutral = 1f` because the game
  "initializes `WorldGeneration.lootRarityMultiplier` / `trapRarityMultiplier` to 1f … and only ever scales them during
  generation — per layer … never inside a running layer", and it REFUSES a non-finite value on every path into the kernel
  (the command path, the applied wire batch and the checkpoint restore) "so the layer keeps the game's own values instead of
  being scaled by a number that has no meaning".
- **A generating side must use the RUN's seed carrier and multipliers, not the game's fresh ones.**
  `src/CasualtiesUnknownOnline.GameState/Domains/World/RunState.cs` says the raw `RandomState` "is the game's only seed
  carrier (the game has no numeric seed)", and that the multipliers belong to the run because generation consumes them: "a
  side that generates the layer with the game's fresh 1.0 instead of the run's current value produces a different world".
- A fact about the layer being LEFT must not survive into the layer being ENTERED: the layer boundary is one family under
  one rule, and every such fact is classified rather than silently kept or silently dropped.
- Items the player carries cross the boundary with them; world facts belong to the layer they were created in.
- **"Rooted in the world" is one shared rule, and it decides what a boundary may drop.**
  `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemLocationChain.cs` is the single walk behind "is this item inside
  that container", "how deep", "does the chain end in the world" and the cycle check, and it is public because the archive
  asks it too: "two copies of this walk would eventually disagree about what 'in the world' means, and the layer-boundary
  reset and the archive would then drop different subtrees". Its verdicts are explicit — a carried item and its contents are
  NOT world-rooted ("it travels with the player"), an applied item is not rooted either ("an installed splint survives the
  cut exactly as a carried item does"), and a terminal item is not rooted because "its chain has ended, and a terminal record
  is never resurrected".
- A member who is not in the world when the host moves is placed when it arrives, not left behind.- **The group enters TOGETHER, in the world scene.** A layer change must not pull the members back to a menu: the accepted reading is that "the members stay in the world scene, run their own new-layer generation from the host's baseline and enter within 13 ms of the host", and the dropped-edge fallback "recovers through the ordinary `WorldJoin` with the group still released together".- **A member that lost its body comes back without a cold restart.** A dropped `WorldJoin` used to leave a member `inWorld: false` with `no-local-body` at +80 s and +112 s; the fix brings it back into the host's new layer with its carried items in one session.- **Everyone lands where the layer says.** A normal descent lands all members on one point, and the two abnormal entries — a dropped-edge re-entry and a mid-descent reconnect — "land at the new layer's own spawn with the old position refused", with a dead member revived into the new layer.- **A layer change never drops a member.** The accepted reading of the drop-out defect is that with both members' inbound parked inside one command "both members stay in the world with the item they carried across the change", and every client's growth "read as a size stays at 0.35–… MB/min against the ~8.2 MB/min storm"; the ticket's own control shows the unparked path is what dropped them.

## Refusals

| Condition | The player reads |
|---|---|
| the descent is not offered yet | the game's own condition, no CUO line |
| the layer moved while the request was in flight | the request is abandoned with the layer it belonged to |

## Who judges

The host owns the layer transition; a member's request is an intent, never a prediction of the new world. The member's
own client shows its body moving only when the host's new layer state arrives.

## What travels

The intent to descend, the host's decision, and the new layer's state: the generated world, the bodies' placement, and
the items that cross with their owners.

## What the player sees and hears

The descent plays the game's own transition; the party arrives in the same layer at the same moment, and every client
shows the same new world.

## Records

`../../architecture/surfaces.md` (world overlays row is unrelated; the layer boundary is a kernel rule), decisions 174
(the boundary is one family under one rule) and 242 (the choice is the member's, the layer is the host's). Accepted:
`../../backlog/done/entity-destruction-drop-guest-fresh-state-loss.md` (the boundary family's record).

## Gaps

- The exact set of facts classified at the boundary (which are captured, which are resolved before the move, which are
  dropped) is stated by the transient policy; it belongs on this page once read from there.
