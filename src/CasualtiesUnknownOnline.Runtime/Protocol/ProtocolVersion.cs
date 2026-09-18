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
	/// reporter's copy and 60 s re-report alive forever.
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
	/// connecting.</summary>
	public const int Current = 24;

}
