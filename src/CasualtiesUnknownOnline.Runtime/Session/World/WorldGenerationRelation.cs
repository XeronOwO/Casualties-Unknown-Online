namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// How a received direct world report relates to the generation this side is
/// simulating. The comparison is one pure decision shared by every stamped
/// report family, so "is this report about MY world?" has a single answer.
/// </summary>
public enum WorldGenerationRelation
{
	/// <summary>The report carries the generation this side is at — it is about this world, and the domain rules apply as they always did.</summary>
	Current,

	/// <summary>
	/// The report belongs to another generation (another run, or another layer of
	/// this run). Its cell/position keys address a world this side no longer
	/// simulates, so applying it would write into the wrong generation: it is
	/// refused, never re-attributed.
	/// </summary>
	Stale,

	/// <summary>
	/// Nothing to compare: the report carries no stamp, or this side has no
	/// committed run baseline yet. The receiver keeps its pre-stamp behaviour
	/// (which never accepts a report it cannot attribute); this is not a licence
	/// to treat the report as current.
	/// </summary>
	Unknown,
}
