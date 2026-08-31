namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The permission level attached to one command definition. The command console
/// keeps this as data so UI hints and the execution gate use the same source
/// without the UI owning policy. Mods declare this on their console commands
/// through the Abstractions API.
/// </summary>
public enum CommandPermission
{
	Anyone,
	HostOnly,
}
