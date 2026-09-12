using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What the Continue entry resolved to. <see cref="Started"/> is the adapter's
/// verdict input: false means the entry must NOT fall through to the native
/// load-run path (decision 165 — CUO never reads <c>save.sv</c>), so the run
/// simply does not start and the summary is logged.
///
/// <see cref="Salvage"/> carries every skip and fallback of the load (§6). S2
/// logs them; choosing the in-game surface for them is S4's decision.
///
/// <see cref="LocalCharacter"/> is what that restore produced for the LOCAL
/// player — the character the adapter still has to put back on its own body. The
/// Runtime cannot: the body does not exist at the click (the scene loads
/// afterwards), so the character travels with the outcome to the one seam that
/// owns the local body's restore. It is null when the archive carries no
/// character the local player claims, which is decision 162's "that player joins
/// as a NEW character" — never a reason to invent one, and never a silent
/// failure: the adapter says so in its own log.
/// </summary>
public sealed record WorldContinueOutcome(
	bool Started,
	string WorldId,
	string Summary,
	SalvageResult Salvage,
	CharacterDataMsg? LocalCharacter = null)
{
	/// <summary>A refusal: nothing was applied, and the caller must not continue.</summary>
	public static WorldContinueOutcome Refused(string worldId, string summary, SalvageResult salvage) =>
		new(false, worldId, summary, salvage);
}
