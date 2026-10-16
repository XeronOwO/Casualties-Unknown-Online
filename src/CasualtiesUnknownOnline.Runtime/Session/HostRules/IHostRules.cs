using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The read-only host-rules surface in force for this process. Composed from
/// <see cref="HostRulesOptions"/> (host-only new flags) and
/// <see cref="RespawnOptions"/> (save/revive flags), so one small
/// service owns the whole high-value host-rule field set without a 60-field
/// KrokMP-style struct.
///
/// <para>
/// The values are the HOST's on both sides: a host reads its own configuration, and a
/// member reads the set the host announced (<c>NetMsg.HostRules</c>), because the rules
/// decide what a member's screen offers, what its console lets it do and what its own
/// carried body weighs. The replication is not part of the wire contract of this
/// interface — it is what makes the values this interface reports the ones in force —
/// and a member that received no announcement reads its own configuration, which is the
/// behaviour that existed before it (<see cref="HostRulesService"/>).
/// </para>
/// </summary>
public interface IHostRules
{
	/// <summary>PVP is enabled. Reserved for the future player-vs-player domain; no gameplay effect yet.</summary>
	bool PvpEnabled { get; }

	/// <summary>Auto-continue to the next layer is enabled. Reserved host-rule surface; not wired yet.</summary>
	bool AutoContinue { get; }

	/// <summary>A brand-new player may join the host's already-running world.</summary>
	bool AllowLateJoin { get; }

	/// <summary>Host-only: other players may take carried items from a remote player's inventory.</summary>
	bool AllowRemoteInventoryTake { get; }

	/// <summary>Host-only: a member may run the item-data write verbs (<c>/data set</c>) on an item it may report on — its own carried/installed/contained item, or world content. Off by default, and enforced on the host at the admission seam.</summary>
	bool AllowMemberItemDataWrite { get; }

	/// <summary>Host-only: widen the native custom run-settings slider ranges in co-op for lobby-size tuning.</summary>
	bool WidenRunSettings { get; }

	/// <summary>Host-only: the fraction of a carried rider's full encumbrance added to the carrier while a carry/piggyback relation is active.</summary>
	float PiggybackWeightMultiplier { get; }

	/// <summary>Host-only: how a member's declared native binding is judged against the host's own for a mod both sides list (allow / warn / require).</summary>
	NativeBindingParity NativeBindingParity { get; }

	/// <summary>Respawn keeps inventory (from <see cref="RespawnOptions"/>).</summary>
	bool SaveInventory { get; }

	/// <summary>Dead players may be revived at a trader (from <see cref="RespawnOptions"/>).</summary>
	bool ReviveFromTrader { get; }

	/// <summary>Dead players auto-respawn on the next layer (from <see cref="RespawnOptions"/>).</summary>
	bool ReviveOnNextLevel { get; }

	/// <summary>Death is terminal (from <see cref="RespawnOptions"/>).</summary>
	bool Permadeath { get; }

	/// <summary>Host-only: the radiation line stops a margin above the layer floor and stays there, so the bottom of a layer is livable and a group can share supplies and rest. On by default.</summary>
	bool RadiationLineStopsAboveTheFloor { get; }
}
