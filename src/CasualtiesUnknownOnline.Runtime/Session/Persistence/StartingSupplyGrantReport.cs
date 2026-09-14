using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The account of ONE starting-supplies grant — what a player entering a world the
/// world has no character for was handed, or why they were handed nothing (S4.3).
///
/// It is a report rather than a log line for the reason decision 179 gives the
/// restore's account: the grant answers a question the player can only ask after the
/// fact ("why do I have nothing?" / "why do I have these?"), and the answer belongs on
/// the surface the save system already prints to. The <see cref="Outcome"/> separates
/// the three things that must not be confused:
///
/// - <see cref="Disposition.Granted"/> — the run's <c>startingsupplies</c> setting was
///   handed to this player, once, on this body;
/// - <see cref="Disposition.Disabled"/> — the run is configured with
///   <c>startingsupplies = none</c>, so there is nothing to hand out. Expected, and
///   worth saying once: a player who expects supplies needs to see that the run, not
///   the mod, decided against them;
/// - <see cref="Disposition.AlreadyOwned"/> — the game's own first-layer grant already
///   supplied this player (a fresh run, whose body is generated with the supplies
///   inside <c>WorldGeneration.WorldPlacePlayer</c>), so CUO deliberately hands out
///   nothing. Named rather than silently skipped: it is the case that would otherwise
///   look like a duplicate-grant bug the first time a reader compares two players.
///
/// A snapshot that takes over the body later (a restore) supersedes the grant by
/// construction — the restore wipes the slots and puts the archive's items back —
/// so a grant that is never mentioned again is still correct; this report describes
/// the moment, not the inventory.
/// </summary>
/// <param name="Disposition">What the grant attempt resolved to.</param>
/// <param name="Setting">The run's <c>startingsupplies</c> setting in the game's own words (<c>none</c>/<c>light</c>/<c>full</c>, or <c>unset</c> when the run carries no such setting).</param>
/// <param name="Items">The content ids handed out, in the order they were placed; empty unless <see cref="Disposition.Granted"/>.</param>
/// <param name="Unplaced">The content ids that could not be placed on the body (no slot accepted them) — they were left on the ground at the body, named here so a partial grant is never silent.</param>
public sealed record StartingSupplyGrantReport(
	StartingSupplyGrantReport.Disposition Outcome,
	string Setting,
	IReadOnlyList<string> Items,
	IReadOnlyList<string> Unplaced)
{
	/// <summary>What one grant attempt resolved to.</summary>
	public enum Disposition
	{
		/// <summary>The setting's items were created and placed on the body (every one, unless <see cref="Unplaced"/> names the rest).</summary>
		Granted,

		/// <summary>The run is configured with <c>startingsupplies = none</c> — nothing to hand out.</summary>
		Disabled,

		/// <summary>The game's own first-layer grant already supplied this body (a fresh run) — CUO hands out nothing.</summary>
		AlreadyOwned,
	}

	/// <summary>True = every item the setting names landed on the body (nothing was left on the ground).</summary>
	public bool Complete => Unplaced.Count == 0;

	/// <summary>
	/// The one-line account the console prints as its notification and the log keeps —
	/// the same words on both, so a log and a player's screen can be compared directly.
	/// </summary>
	public string Describe() => Outcome switch
	{
		Disposition.Granted => Complete
			? $"CUO new player: starting supplies ({Setting}) given — {string.Join(", ", Items)}."
			: $"CUO new player: starting supplies ({Setting}) given — {string.Join(", ", Items)}; {Unplaced.Count} could not be placed and lie on the ground: {string.Join(", ", Unplaced)}.",
		Disposition.Disabled => "CUO new player: this run grants no starting supplies (startingsupplies = none).",
		_ => $"CUO new player: the world's own first-layer supplies ({Setting}) are already yours — CUO granted nothing on top.",
	};
}
