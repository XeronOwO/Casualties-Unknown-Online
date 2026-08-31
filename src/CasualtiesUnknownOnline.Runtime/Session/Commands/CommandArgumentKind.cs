namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The declared shape of one command argument. The console uses this to decide
/// which suggestion source applies and to keep the hint/usage text in the same
/// place as the command registration.
/// </summary>
public enum CommandArgumentKind
{
	None,
	CommandName,
	PlayerOrSteamId,
	SteamId,
	Number,
	Text,
	Json,
}
