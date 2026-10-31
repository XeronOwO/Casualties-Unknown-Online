namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The permission level attached to one command definition. The command console
/// keeps this as data so UI hints and the execution gate use the same source
/// without the UI owning policy. Mods declare this on their console commands
/// through the Abstractions API.
/// </summary>
public enum CommandPermission
{
	/// <summary>Any player may run it.</summary>
	Anyone,

	/// <summary>Only the host may run it; a member is refused before the handler.</summary>
	HostOnly,

	/// <summary>
	/// The cheat family: the host's cheat SWITCH must be open, and for a member the host's ROSTER must name that player — the same two rulings every built-in
	/// cheat verb follows (`/give`, `/summon`, the write half of `/data`). A refusal names its ground, and it happens on the side that typed the line as well
	/// as on the host, so a relayed attempt the host would refuse never runs its handler.
	/// </summary>
	Cheat,
}
