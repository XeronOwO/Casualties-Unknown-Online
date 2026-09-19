using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Where a direct world report's generation stamp comes from: the kernel run
/// baseline. The host commits it and the guests follow it, so both sides stamp
/// and compare the same identity; before a run is committed there is nothing to
/// stamp and the reports travel unstamped (compared as UNKNOWN by the
/// receiver).
/// </summary>
internal sealed class KernelWorldGenerationSource(ItemKernelAuthority authority)
{
	private readonly ItemKernelAuthority _authority = authority;

	internal WorldReportGeneration? Current =>
		_authority.QueryRun() is { } run ? new WorldReportGeneration(_authority.CurrentRunEpoch.Value, run.LayerIndex) : null;

	internal WorldGenerationMsg? Stamp() => WorldReportGeneration.Stamp(Current);
}
