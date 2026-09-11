using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One cut to write: which world, what the trigger was, the phase the seam was
/// in, and the characters collected for it. The kind is decided by the caller
/// (<see cref="WorldCutWriter.KindOf"/>) because the CALLER's transient policy
/// depends on it: a layer-end cut carries no in-layer fact, so no in-flight
/// state can be lost by one.
/// </summary>
/// <param name="WorldId">The world folder this cut writes into.</param>
/// <param name="DisplayName">The renameable display name recorded in the manifest.</param>
/// <param name="Reason">The trigger.</param>
/// <param name="Kind">The cut kind the payload will carry.</param>
/// <param name="CutPhase">The manifest's cut phase (which seam the cut was taken at).</param>
/// <param name="Characters">Every member's character as collected at the cut instant.</param>
internal sealed record WorldCutWriteRequest(
	string WorldId,
	string DisplayName,
	WorldCutReason Reason,
	WorldCutKind Kind,
	string CutPhase,
	IReadOnlyList<SavedCharacter> Characters);
