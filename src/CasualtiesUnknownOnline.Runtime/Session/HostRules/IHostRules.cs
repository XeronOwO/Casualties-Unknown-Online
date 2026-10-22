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

	/// <summary>Host-only: who may open and operate another player's backpack — allow, an unconscious or dead owner only (a conscious owner's pack opens view-only), or deny.</summary>
	RemoteBackpackAccess RemoteBackpackAccess { get; }

	/// <summary>Host-only: the cheat switch — members may run the cheat verbs (<c>/data</c>'s write verbs, <c>/give</c>) at all, and only those this host's roster (<c>/op</c>) also names. Off by default, and enforced on both sides: the member's own console against the announced value, the host at the admission seam.</summary>
	bool AllowMemberCheatVerbs { get; }

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

	/// <summary>Host-only: A spectating player may free-fly the camera instead of only following a teammate. On by default, aligned with the reference mod: a free camera can scout the layer ahead, so it is a group decision rather than one client's rendering choice.</summary>
	bool SpectatorFreeFly { get; }

	/// <summary>Host-only: An UNCONSCIOUS player may spectate. Off by default: in this project's model an unconscious body is still its owner's to act on, so this changes a live player's client rather than a dead one's.</summary>
	bool SpectateWhileUnconscious { get; }

	/// <summary>Host-only: A client that has not entered the world yet may spectate. Off by default: that is the late-join case, and it is a separate promise about what a not-yet-in-world client may see.</summary>
	bool SpectateWhileWaitingToJoin { get; }
}
