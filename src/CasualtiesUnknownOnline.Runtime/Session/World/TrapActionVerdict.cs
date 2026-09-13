namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The verdict one trap fact's application earns — the one rule that decides
/// whether a row reached the live world for every row a SHARED ACTION answered
/// (the adapter's <c>TrapVisualReplay.ReplayState</c> / <c>ReplayShuttleDoor</c>
/// and <c>TrapEffectApplier.ApplyState</c>). That boolean is the currency the
/// restore's live-write account counts (<c>EntityEventSync.OnTrapStateProjected</c>
/// feeds it into <c>LiveWorldWriteOutcome</c>), and therefore what the player's
/// restore report names as refused.
///
/// The destructive families (the mine / turret / unstable-crystal explosions) are
/// NOT routed through here: they run their own consumption checks inline because
/// they do not go through the shared action library, so a change to what
/// "reached" means has to be applied there too.
///
/// The split exists because the Game Adapter supplies what it OBSERVED (was an
/// entity there, what did the shared action return) while what that observation
/// MEANS for the account is decided here — in the Runtime, where the account
/// lives, and where the rule is testable without a game: the adapter's own bodies
/// call <c>Object.FindObjectsOfType</c> and the game's trap types, which no test
/// host can instantiate.
/// </summary>
internal static class TrapActionVerdict
{
	/// <summary>
	/// Whether the fact this row carries is represented in the live world.
	/// <paramref name="outcome"/> is the shared action's verdict, or NULL when no
	/// entity of the expected kind was found at the row's position — the two
	/// observations every caller actually has. An APPLIED row is in the world, and
	/// so is a row the local copy already carried (a duplicate: the state the row
	/// names IS there). Everything else is REFUSED: an entity that cannot carry the
	/// fact (<see cref="TrapActionOutcome.NotApplicable"/>), a missing entity, and a
	/// verdict this rule does not DECLARE — the reached set is written positively so
	/// a follow-up outcome fails CLOSED (refused, never counted as restored) and has
	/// to be classified here deliberately.
	/// </summary>
	internal static bool ReachedTheLiveWorld(TrapActionOutcome? outcome) =>
		outcome is TrapActionOutcome.Applied or TrapActionOutcome.AlreadyInState;
}
