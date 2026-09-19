using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The single decision every direct world report's generation comparison goes
/// through: compare the report's stamp with this side's own generation and log
/// every STALE verdict with both sides of the comparison (kind, sender,
/// reported generation, current generation). A refusal of this kind must be
/// distinguishable in the field from a first-writer loss or a malformed report,
/// so it is never silent; an UNKNOWN verdict (no stamp, or no committed run
/// baseline here) is not stale and leaves the caller's pre-stamp behaviour in
/// place.
/// <para>
/// The comparison itself is the pure <see cref="WorldReportGeneration.Relate"/>;
/// this type owns only the shared wire-side refusal log, so every stamped
/// family — the block reports, the trap layout and the runtime entity creations
/// — refuses a report of another world generation in the same words.
/// </para>
/// </summary>
internal static class WorldReportGenerationGate
{
	/// <summary>
	/// Relate an arrived report's stamp to <paramref name="current"/> (this
	/// side's own generation), logging a STALE verdict once with the report's
	/// kind, its sender and both generations. The caller decides what a refusal
	/// means for its family (drop the write, answer the reporter, skip the
	/// materialization); this method only answers "is this report about MY
	/// world?" and makes that answer observable.
	/// </summary>
	internal static WorldGenerationRelation Relate(WorldGenerationMsg? reported, WorldReportGeneration? current, string kind, ulong sender, ILogger log)
	{
		var relation = WorldReportGeneration.Relate(current, reported);
		if (relation == WorldGenerationRelation.Stale)
		{
			log.LogWarning("[WorldReportGeneration] {Kind} from {Sender} belongs to {Reported} while this side is at {Current} — the report is about another world generation and is refused as stale.",
				kind, sender, WorldReportGeneration.Describe(reported), WorldReportGeneration.Describe(current));
		}

		return relation;
	}
}
