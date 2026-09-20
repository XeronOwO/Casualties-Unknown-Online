namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What a world-library action asked for (decision 198). The kind is part of the
/// report because a refusal's wording is not the only thing that tells the two
/// apart: "select" changes which world the Continue entry opens and touches no
/// snapshot, "restore" replaces one world's live snapshot.
/// </summary>
public enum WorldMaintenanceKind
{
	/// <summary>Make a world the one the Continue entry opens.</summary>
	Select,

	/// <summary>Replace a world's live snapshot with a chosen backup archive (§6).</summary>
	Restore,
}
