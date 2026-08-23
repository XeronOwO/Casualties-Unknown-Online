namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The host-rule flags that are not already owned by <see cref="RespawnOptions"/>.
/// The host rules service composes this with <see cref="RespawnOptions"/> into
/// one read-only host-rules surface. Backed by <c>IOptionsMonitor&lt;T&gt;</c>
/// so a BepInEx config edit hot-reloads without a restart.
/// </summary>
public sealed class HostRulesOptions
{
	/// <summary>
	/// Player-vs-player damage is enabled. The PVP damage domain is intentionally
	/// not built yet (backlog §2.6); this flag is the host-rule surface reserved
	/// for that future domain and currently has no gameplay effect.
	/// </summary>
	public bool PvpEnabled { get; set; }

	/// <summary>
	/// Automatically continue to the next world layer after generation finishes
	/// instead of requiring the host to click again. Reserved host-rule surface;
	/// the auto-continue flow is not wired yet.
	/// </summary>
	public bool AutoContinue { get; set; }

	/// <summary>
	/// A brand-new player may handshake and enter the host's already-running
	/// world. Disabling it rejects new members while the host is in-world;
	/// reconnects of already-known members are always allowed.
	/// </summary>
	public bool AllowLateJoin { get; set; } = true;
}
