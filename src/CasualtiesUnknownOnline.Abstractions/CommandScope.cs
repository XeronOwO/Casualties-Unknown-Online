namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Where a command runs. The distinction is the ceiling on what a command may do, so it is DECLARED, never inferred from what the handler happens to touch.
/// </summary>
public enum CommandScope
{
	/// <summary>Runs in the process whose player typed the line, with that process's own view of the session.</summary>
	Local,

	/// <summary>
	/// Runs on the AUTHORITY (the host), because the answer is a world fact only the host can state — a give, a spawn, a rule change. The member's request
	/// travels, the host decides (its rules plus the cheat switch), and the answer comes back as keys the member's own client renders. Requires the mod on both
	/// sides; a member without it never sees the command.
	/// </summary>
	HostRelayed,
}
