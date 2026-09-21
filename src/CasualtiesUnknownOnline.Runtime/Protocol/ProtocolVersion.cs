namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Wire compatibility version for the handshake. No released
	/// compatibility surface exists yet, so each behavioral wire extension bumps
	/// this and mixed-version sessions are rejected by the handshake.
	/// 19: `BlockStateEntryMsg.SupportLossSettled` — the receiver uses it to decide
	/// whether a snapshot row re-settles building support loss, so a peer without it
	/// would re-kill buildings the authority still holds.
	/// 20: `WorldItemsReset` / `ResetWorldItemsCommand` — the layer boundary now
	/// resets the item domain's world-rooted records, so a peer without the event
	/// would keep every earlier layer's world items and then disagree with the
	/// host's kernel after the first layer switch.
	/// 21: `RuntimeEntityRejected` — a runtime creation the host cannot
	/// materialize is now REJECTED instead of relayed, so a peer without the
	/// message would keep relaying an unowned creation to everyone and leave the
	/// reporter's copy and its re-report alive forever.
	/// 22: `BlockDamageReport` — a guest's partial block damage now has an
	/// ABSOLUTE re-report whose answer is a `BlockDamageSnapshot` carrying this
	/// host's authoritative value per reported cell. A peer without the message
	/// would never send the report (its swallowed hits would stay missing from
	/// the host's own damage list forever) and would not understand the answer's
	/// zero rows.
	/// 23: `EnemyStateMsg.SpawnPosition` — the enemy snapshot now carries the
	/// host's BIND-TIME spawn anchor beside the live position, the guest pairs
	/// its frozen copies on the anchor, and the snapshot rides the 60 s
	/// in-session repair group. A peer without it would pair on the live
	/// position (which fails the moment the host's enemy has walked away from
	/// its spawn spot, so the repair could not bind) and would never be re-sent
	/// a snapshot it missed.
	/// 24: `EnemyAttackMsg` — a host enemy attack is now an ANNOUNCEMENT broadcast
	/// to every in-world guest (`EnemyId` + `Kind` + the per-enemy `AttackSeq`)
	/// instead of a verdict addressed to one victim with a host-chosen limb
	/// (`VictimSteamId` / `LimbIndex`). Each guest judges the connection on its own
	/// view and applies the game's damage locally. A peer without it would apply a
	/// verdict this protocol no longer carries and would ignore the dedup identity,
	/// so its victims would take damage for attacks their own screens never showed
	/// connecting.
	/// 25: `MedicalOperationTargetCheckRequest` / `MedicalOperationTargetCheckAnswer`
	/// — a medical operation start's target-body preconditions are now answered by
	/// the TARGET's own client from its own live body instead of being judged by the
	/// host from the target's last 1 Hz report. A peer without the pair would never
	/// answer the host's check and would itself never ask, so every start whose only
	/// open question is the target's body would be refused (or judged on stale data)
	/// — the exact host verdict the ruling removed.
	/// 26: `MedicalOperationEndCommittedMsg.TerminalReason.AlreadyHandled` — a
	/// medical unit that resolves once (one dislocation, one amputation, one
	/// splint/tourniquet removal) is now settled by the FIRST completion, and the
	/// other operations still open on that unit are terminated with this reason
	/// carrying the winner's authoritative state. A peer without it would read the
	/// loser's terminal as an ordinary completion, keep its minigame on a limb that
	/// is already treated, and could resolve the same unit a second time.
	/// 27: the creation-before-operation invariant — an item operation
	/// (`ItemPickup` / `ItemDrop` / `ItemTransfer` / `ItemDestroy` /
	/// `ItemUpdateState`) whose item's creation the host has not judged is refused
	/// AT ONCE, with the reason the creation died when it was refused, instead of
	/// being parked in a fixed 500 ms hold window and answered late with a less
	/// precise one. The reporting side must settle every deferred creation report
	/// before it reports an operation on the same item. A peer without it would
	/// still park the claim and wait on a window the other side no longer fills,
	/// and its own operations would be judged against a creation ordering it does
	/// not implement.
	/// 28: the local-initiation world-time model — a manual speed (the speed
	/// hotkey, or the native left/right movement reset) now writes the
	/// initiator's OWN clock at once and is reported as `WorldTimeRequest`; the
	/// host arbitrates accept-first and answers every request with the
	/// authoritative `WorldTime` speed, which settles the initiator's pending
	/// intent (a refused or overridden intent ramps back to the host's value
	/// instead of sitting ahead until the next 5 s resend). The host-side
	/// velocity veto is deleted: movement is the mover's own action, never a
	/// world state the host polls. A peer without it would swallow its local
	/// manual speeds and expect a host that discards manual requests, so one side
	/// would keep running ahead of the other's clock with no verdict to settle
	/// it.</summary>
	/// 29: the world/layer generation stamp — the direct world reports whose keys
	/// are layer-relative (`BlockPlaced`, `BlockDamaged`, and the
	/// `BlockDamageSnapshot` payload that carries both the late-joiner snapshot
	/// and the guest's absolute `BlockDamageReport`) now carry the kernel run
	/// baseline's `(RunEpoch, LayerIndex)` on the wire. The receiver compares the
	/// stamp with its own generation: a stale previous-layer report is REFUSED
	/// with a precise log instead of being applied to a freshly generated world,
	/// and the one shape that used to be unattributable — a break report naming a
	/// cell this side still holds, because the sender's air-write report was lost
	/// together with the drops carrier (`Verdict.LostAirWrite`) — is now accepted,
	/// so those drops survive. A peer without the stamp would report unstamped
	/// (compared as UNKNOWN, the pre-stamp conservative behaviour) and would apply
	/// this side's stamped reports without the check, so the two sides would
	/// disagree about which generation a report belongs to exactly when a layer
	/// boundary is crossed.
	/// 30: the generation stamp reaches the remaining position-keyed families —
	/// the trap-layout snapshot (`TrapLayoutSnapshotMsg`, host → guest) and the
	/// runtime-entity creation report with its absolute table (`EntitySpawnedMsg`
	/// and `RuntimeEntitySnapshotMsg`, guest → host, the host's relay, and the
	/// re-report/repair cycle). Both materialize entities at layer-relative
	/// positions, so the receiver refuses a STALE one before anything is created
	/// or materialized, and the host answers a refused creation report through
	/// the existing rejection path (`RuntimeEntityRejectReason.StaleGeneration`)
	/// so its pending re-report ends. A peer without the stamp would report
	/// unstamped (compared as UNKNOWN, the pre-stamp behaviour) and would
	/// materialize this side's stamped reports without the check, so across a
	/// layer boundary the two sides would disagree about which world a trap
	/// layout or a creation belongs to.</summary>
	/// 31: the recipe-unlock backfill (`RecipeUnlockSnapshot`) — the blueprint
	/// unlock's one-shot report/relay now has an absolute SET beside it: the host
	/// sends its live recipe table's unlocked indices on world entry and in the
	/// 60 s repair group, and a guest reports its own set on the fallback cadence
	/// until this host's set carries it (the host merges the difference and
	/// relays exactly those through the ordinary unlock path). A peer without it
	/// would keep a crafting list permanently short by every unlock whose report
	/// or relay it missed — including every unlock that happened before it
	/// joined — and would ignore the set the other side sends.
	/// 32: partial block damage is accounted PER SENDER
	/// (review/partial-damage-delta-report-overlap). BlockDamaged now names the block
	/// CELL (the key every consumer of the family already uses) and carries the
	/// sender's cumulative contribution to that cell in the game's accumulated
	/// units; a guest's report carries its own contribution per cell instead of the
	/// cell's total, and the host's answer marks itself with AnswersReport. A peer
	/// without it would keep merging absolute totals against a delta the receiver
	/// accumulates, so two senders whose reports were both swallowed would converge
	/// to the higher value instead of the sum, and a delta landing after the report
	/// covering it would count the same hit twice.
	/// 33: `RunFacts` — the run clock base (`SaveSystem.savedRunTime +
	/// WorldGeneration.world.realTimeElapsed`) and the layer's radiation-timer
	/// accounting (`layerTimeSpent` / `maxTimePerLayer`) now reach a member that
	/// joined a run in progress, absolute and stamped with the kernel run
	/// baseline's generation. Neither value belongs to a CUO domain, so neither
	/// rides the kernel run baseline: a peer without the message keeps its own
	/// process statics, so a guest that joined 40 minutes into a run reads the
	/// end screen's clock as the time since it joined (0 on a fresh launch) and
	/// its own layer timer starts at 0 while the host's has been running since
	/// the layer was generated.
	/// 34: `WorldJoinMsg.RunEpoch` — the enter-the-world instruction now
	/// announces the run identity the host is serving, and the guest refuses a
	/// checkpoint chunk set whose epoch is not that identity BEFORE a chunk is
	/// buffered. Without the announcement a guest has no host-authored identity
	/// to compare a set against (its own kernel epoch is its own counter, and a
	/// previous session's straggler or a superseded set could hand it another
	/// run's state, whose epoch then no longer matches the live streams). A peer
	/// without the member would keep restoring whatever set arrived and would
	/// keep letting a partial set's chunks occupy the slots a live set needs.
	/// 35: `ModInfoMsg.NativeBinding` — each mod's declared native binding
	/// (`[CuoMod] NativeBinding`, decision 206) now rides the handshake's mod
	/// list, and the host judges the entries BOTH sides list against its own
	/// declaration with its `NativeBindingParity` rule: allow (silent), warn (the
	/// default — the member is admitted and the host records the mismatch), or
	/// require (the member is refused, naming the mod and both declarations). A
	/// peer without the field would declare nothing, so every both-listed mod it
	/// reports would be compared as "undeclared": a host requiring parity would
	/// refuse it for a difference it never had the chance to report, and a host
	/// warning would record `none` against its own declaration for every such mod
	/// — the rule would exist on one side only, and the member's real bindings
	/// would stay invisible exactly where the tier promised visibility. Parity
	/// stays visibility, never proof: an undeclared binding is still invisible and
	/// an equal declaration does not prove equal behaviour (decision 204).
	public const int Current = 35;

}
