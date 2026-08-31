namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The permission level attached to one command definition. The console keeps
/// this as data so UI hints and the execution gate use the same source without
/// the UI owning policy.
/// </summary>
public enum CommandPermission
{
	Anyone,
	HostOnly,
}
