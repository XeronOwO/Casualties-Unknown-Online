namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

using System.Collections.Generic;

/// <summary>
/// The capability facts a build's adapter reports about itself for the startup
/// log: the catalog report stage 1 of <c>review/adapter-capability-catalog.md</c>
/// produces — one line per capability with its Required/Optional class, its
/// contract count and its failure reasons, plus the session verdict. The read
/// is text-shaped today because the only consumer (the plugin's startup log)
/// prints it; a consumer that needs the facts rather than the text gets them
/// from the capability catalog, not from a second projection here.
/// </summary>
public interface IAdapterCapabilityQuery
{
	/// <summary>Human-readable capability report for startup logs.</summary>
	string CapabilityReport { get; }

	/// <summary>
	/// The REQUIRED capabilities whose hooks did not land in the last install
	/// attempt, by their player-facing titles (session first, duplicates
	/// collapsed); empty when the install succeeded or only Optional
	/// capabilities degraded. The mod's own window renders this so a refusal is
	/// never silent.
	/// </summary>
	IReadOnlyList<string> RefusedCapabilities { get; }
}
