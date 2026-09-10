using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What produced a snapshot (§3.2 <c>kind</c>, §7): the layer boundary, an
/// arbitrary moment inside a layer, or the interval autosave. The JSON spelling
/// is the format's and comes from <see cref="SaveArchiveFormat.CutKindName"/>
/// through the archive's <see cref="JsonStringEnumConverter"/>.
/// </summary>
public enum WorldCutKind
{
	/// <summary>Cut at the boundary where the run descends to the next layer.</summary>
	LayerEnd,

	/// <summary>Cut inside a layer, with the world diff and transient policy applied.</summary>
	MidRun,

	/// <summary>Cut by the configurable interval autosave.</summary>
	Auto,
}
