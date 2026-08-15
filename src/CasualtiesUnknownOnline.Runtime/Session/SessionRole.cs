namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The client's lobby identity — follows the actual lobby (None while in no lobby); EndSession keeps it for same-lobby rejoin.</summary>
public enum SessionRole
{
	None,
	Host,
	Guest,
}
