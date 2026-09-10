namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The transport key space a snapshot's <c>characters/</c> files belong to
/// (docs/architecture/save-archive-format.md §2, decision 162): Steam keys are
/// <c>steam-&lt;steamId64&gt;</c>, IP-direct keys are
/// <c>name-&lt;sanitized display name&gt;</c>. A world written in one mode is never
/// claimed in the other, because the prefixes never match.
///
/// The space is DERIVED from the snapshot's own character file names rather
/// than stored: a host restoring a world clicks Continue from the main menu,
/// where no transport is active yet and the live mode is therefore unknown.
/// The keys themselves are the authority.
/// </summary>
public enum PlayerKeySpace
{
	/// <summary>No character file named a key space (an empty snapshot); the caller falls back to the live transport mode.</summary>
	Unknown,

	/// <summary>Steam transport: <c>steam-&lt;steamId64&gt;</c>.</summary>
	Steam,

	/// <summary>IP-direct transport: <c>name-&lt;sanitized display name&gt;</c>.</summary>
	IpDirect,
}
