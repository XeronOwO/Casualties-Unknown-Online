using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What the Continue entry resolved to. <see cref="Started"/> is the adapter's
/// verdict input: false means the entry must NOT fall through to the native
/// load-run path (decision 165 — CUO never reads <c>save.sv</c>), so the run
/// simply does not start and the summary is logged.
///
/// <see cref="Salvage"/> carries every skip and fallback of the load (§6). S2
/// logs them; choosing the in-game surface for them is S4's decision.
/// </summary>
public sealed record WorldContinueOutcome(bool Started, string WorldId, string Summary, SalvageResult Salvage)
{
	/// <summary>A refusal: nothing was applied, and the caller must not continue.</summary>
	public static WorldContinueOutcome Refused(string worldId, string summary, SalvageResult salvage) =>
		new(false, worldId, summary, salvage);
}
