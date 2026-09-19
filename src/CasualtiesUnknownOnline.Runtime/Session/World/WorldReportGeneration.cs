using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world/layer generation identity the direct world reports carry: the
/// kernel run baseline's <c>(RunEpoch, LayerIndex)</c>. Both sides read it from
/// the same place — the run baseline the host commits and the guests follow —
/// so the stamps are comparable without a new counter and without trusting
/// either side's local world state.
/// </summary>
internal readonly record struct WorldReportGeneration(ulong RunEpoch, int LayerIndex)
{
	/// <summary>
	/// The relation between this side's generation and a received report's stamp.
	/// Pure: both inputs are explicit, and a missing half is <see
	/// cref="WorldGenerationRelation.Unknown"/> rather than a match.
	/// </summary>
	internal static WorldGenerationRelation Relate(WorldReportGeneration? current, WorldGenerationMsg? reported) =>
		current is not { } mine || reported is null
			? WorldGenerationRelation.Unknown
			: mine.RunEpoch == reported.RunEpoch && mine.LayerIndex == reported.LayerIndex
				? WorldGenerationRelation.Current
				: WorldGenerationRelation.Stale;

	/// <summary>The stamp to put on an outbound report (<c>null</c> = no committed run baseline, so the report travels unstamped).</summary>
	internal static WorldGenerationMsg? Stamp(WorldReportGeneration? generation) =>
		generation is { } value ? new WorldGenerationMsg { RunEpoch = value.RunEpoch, LayerIndex = value.LayerIndex } : null;

	/// <summary>The log shape of a stamp — used for both sides of a comparison so a refusal names the generations it compared.</summary>
	internal static string Describe(WorldReportGeneration? generation) =>
		generation is { } value ? $"run {value.RunEpoch} layer {value.LayerIndex}" : "no generation";

	/// <summary>The log shape of a received stamp (a report may travel unstamped).</summary>
	internal static string Describe(WorldGenerationMsg? reported) =>
		reported is null ? "no generation stamp" : $"run {reported.RunEpoch} layer {reported.LayerIndex}";
}
