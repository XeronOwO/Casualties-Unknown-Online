namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

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
}
