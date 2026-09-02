namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The declared runtime data scope for one <see cref="IModData"/> slot.
/// The scope is the mod's contract with the framework: it says where the value
/// may live and how it may be changed. CUO never auto-replicates runtime data;
/// a shared mirror is applied explicitly from a host-owned
/// <see cref="IModNetwork"/> message, so there is no generic snapshot protocol.
/// </summary>
public enum ModDataScope
{
	/// <summary>
	/// Per-process ephemeral data that may differ per member and never crosses
	/// the wire (presentation, configuration, debug state). Any network mode
	/// may use it.
	/// </summary>
	LocalOnly = 1,

	/// <summary>
	/// Data that represents a cooperative shared value. The host owns writes;
	/// guests may keep a local mirror by applying a value received from the
	/// host over <see cref="IModNetwork"/>. Only state-bearing network modes
	/// may declare it.
	/// </summary>
	Shared = 2,

	/// <summary>
	/// Data that only the host owns. The framework keeps no guest mirror;
	/// if a guest needs the value for presentation it must receive it through
	/// the mod's own <see cref="IModNetwork"/> coordination. Only
	/// host-authoritative or synchronized/requires-all scenarios may declare it.
	/// </summary>
	HostAuthoritative = 3,
}
