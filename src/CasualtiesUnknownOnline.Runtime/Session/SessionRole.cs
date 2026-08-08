namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The client's lobby identity — follows the lobby, never cleared by EndSession.</summary>
public enum SessionRole
{
	None,
	Host,
	Guest,
}
