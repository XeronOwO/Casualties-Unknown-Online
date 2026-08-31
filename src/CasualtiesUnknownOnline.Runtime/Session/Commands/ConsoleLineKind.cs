namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The presentation kind of one command-console line. The Online UI uses this
/// to color command output without owning any policy.
/// </summary>
public enum ConsoleLineKind
{
	Info,
	Success,
	Error,
}
