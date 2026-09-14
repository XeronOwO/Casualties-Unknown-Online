using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The account of ONE Continue attempt, in the form a player-facing surface
/// renders: how the attempt resolved, one headline line, and the itemized
/// detail. §6 requires the restore's losses to be surfaced IN-GAME — the count
/// per domain, the reason, the affected content id and the backup a load fell
/// back to — and a log line is not a surface, so the command console prints this
/// report where the player is already reading.
///
/// It is raised at the moment the attempt resolves, which is why the disposition
/// has three values rather than a "success" flag: the click can refuse the
/// archive (nothing is applied), apply it (the run starts), or apply it and then
/// be abandoned before any world generation consumes it — and that last one is
/// the case a player otherwise experiences as "the button did nothing".
///
/// <see cref="Summary"/> is the same one-line account the log carries (it names
/// the joined damage with its per-domain counts); <see cref="Details"/> is the
/// same account itemized (one line per skipped entry with its content id, per
/// refused claimant, per field the body cannot take, per fallback). A surface
/// shows the summary as the headline and the details behind it — never the
/// details alone, which would lose the counts, and never the summary alone,
/// which cannot name an id.
/// </summary>
/// <param name="WorldId">The world the attempt named ("" when there was none).</param>
/// <param name="Result">How the attempt resolved.</param>
/// <param name="Summary">The one-line account, in the words the console shows.</param>
/// <param name="Details">The itemized account, one line per loss, skip, refusal or fallback; empty = nothing was lost.</param>
public sealed record WorldRestoreReport(
	string WorldId,
	WorldRestoreReport.Disposition Result,
	string Summary,
	IReadOnlyList<string> Details)
{
	/// <summary>
	/// What one Continue attempt resolved to. The three are distinct states of the
	/// same attempt, not degrees of failure: a refusal applied nothing, an applied
	/// attempt started the run, and an abandoned one was applied but no generation
	/// ever consumed it (see <see cref="IWorldSaveControl.AbandonRestore"/>).
	/// </summary>
	public enum Disposition
	{
		/// <summary>Nothing was applied and the run must not start (decision 165: never the native regenerate path).</summary>
		Refused,

		/// <summary>The checkpoint and the stored characters were applied; the run starts and the live world takes the facts at the world-entry seam.</summary>
		Applied,

		/// <summary>The click applied the archive, but no world generation will consume it; every handover the click armed was released.</summary>
		Abandoned,
	}

	/// <summary>True = the archive opened with nothing lost: the only case a surface may report as a clean restore.</summary>
	public bool Clean => Result == Disposition.Applied && Details.Count == 0;
}
