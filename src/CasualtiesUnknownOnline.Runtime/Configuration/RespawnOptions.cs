namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Host-authoritative revive/respawn rules (KrokMP-inspired co-op lifecycle).
/// These are deliberately a small independent rule set, not a 60-field
/// wholesale copy: Permadeath, trader revive, automatic next-level revive and
/// whether a respawn keeps inventory/skills. The values are read through
/// <c>IOptionsMonitor&lt;RespawnOptions&gt;</c> so a BepInEx config change takes
/// effect on the next decision without a restart.
/// </summary>
public sealed class RespawnOptions
{
	/// <summary>
	/// True = death is terminal for the run: both trader recruit and
	/// automatic next-level revive are disabled. False = the co-op revive
	/// lifecycle below is active.
	/// </summary>
	public bool Permadeath { get; set; }

	/// <summary>
	/// True = a living player can revive a dead in-world teammate at a
	/// friendly trader (the first revive slice).
	/// </summary>
	public bool ReviveFromTrader { get; set; } = true;

	/// <summary>
	/// True = when the host finishes generating the next world layer, dead
	/// players still in the session are respawned (or rejoined if they had
	/// already left the world) automatically.
	/// </summary>
	public bool ReviveOnNextLevel { get; set; } = true;

	/// <summary>
	/// True = a respawn keeps the character's carried/worn items from its
	/// latest host-side snapshot. False = the respawn starts with an empty
	/// inventory (the world's own fresh spawn state stays as-is).
	/// </summary>
	public bool RespawnKeepInventory { get; set; } = true;

	/// <summary>
	/// True = a respawn keeps the character's skills/experience from its
	/// latest host-side snapshot. False = skills reset to zero.
	/// </summary>
	public bool RespawnKeepSkills { get; set; } = true;
}
