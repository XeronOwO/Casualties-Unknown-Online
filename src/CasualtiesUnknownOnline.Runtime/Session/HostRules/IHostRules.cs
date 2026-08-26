namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The read-only host-rules surface. Composed from
/// <see cref="Configuration.HostRulesOptions"/> (host-only new flags) and
/// <see cref="Configuration.RespawnOptions"/> (save/revive flags), so one small
/// service owns the whole high-value host-rule field set without a 60-field
/// KrokMP-style struct. Wire/network is not involved: host rules are local
/// host configuration, not a protocol message.
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

	/// <summary>Host-only: widen the native custom run-settings slider ranges in co-op for lobby-size tuning.</summary>
	bool WidenRunSettings { get; }

	/// <summary>Host-only: the fraction of a carried rider's full encumbrance added to the carrier while a carry/piggyback relation is active.</summary>
	float PiggybackWeightMultiplier { get; }

	/// <summary>Respawn keeps inventory (from <see cref="Configuration.RespawnOptions"/>).</summary>
	bool SaveInventory { get; }

	/// <summary>Dead players may be revived at a trader (from <see cref="Configuration.RespawnOptions"/>).</summary>
	bool ReviveFromTrader { get; }

	/// <summary>Dead players auto-respawn on the next layer (from <see cref="Configuration.RespawnOptions"/>).</summary>
	bool ReviveOnNextLevel { get; }

	/// <summary>Death is terminal (from <see cref="Configuration.RespawnOptions"/>).</summary>
	bool Permadeath { get; }
}
