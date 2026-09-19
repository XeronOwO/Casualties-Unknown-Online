using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// ONE throwing row costs only ITSELF. A restored cut is written by ROW LOOPS — the
/// world-entity half's three appliers and the world-fact half's block-state and
/// partial-damage tables — and any row can reach an engine call the local copy cannot
/// serve: an entity the regenerated layer does not hold, a component the game never
/// initialized, a block cell whose info the game cannot resolve. An unguarded loop loses
/// every row BEHIND the throwing one, and because the throw escapes into the seam that
/// called the applier it also costs every applier that has not run yet and the count the
/// restore's account is built from.
///
/// The rule lives here rather than in the Game Adapter — the layer that owns the loops —
/// for the reason decision 173 gives the sibling rule: the account it feeds is the
/// Runtime's, the refused count has to be EXACT, and this is where that can be
/// machine-checked without a game (the adapter's own types are loaded reflectively by the
/// test host, never compiled against). The adapter keeps what only it knows: the per-row
/// action and the row's IDENTITY (a trap's kind and position, an entity's position, a
/// block cell), which is what the error line names.
/// </summary>
internal static class ContainedRowLoop
{
	/// <summary>
	/// How many throwing rows are named individually before the rest are counted in one
	/// line. A systematic failure is a demonstrated shape here, not a theory — decision
	/// 175 exists because every copy of one entity threw — so the tail is summarised
	/// rather than repeated: the account stays exact and the log stays readable.
	/// </summary>
	private const int DetailedFailures = 5;

	/// <summary>
	/// Run <paramref name="apply"/> over every row, containing one row's failure at a time.
	/// The applier says whether the live world TOOK the row, so the outcome is exact:
	/// applied plus refused is always the row count, and the restore's account names the
	/// rows that were lost instead of the whole half. A row that THROWS is refused and is
	/// logged at error level with its identity; a row the applier refuses by returning
	/// false is refused silently, because the appliers log their own reason.
	/// </summary>
	/// <param name="rows">The rows the cut recorded, in the order the applier applies them.</param>
	/// <param name="apply">The adapter's per-row write: true when the live world took the row.</param>
	/// <param name="identity">How the error line names ONE row (the adapter's knowledge, never a generic index).</param>
	/// <param name="log">The applier's logger — the row's identity and its exception ride one error line.</param>
	/// <param name="what">What the rows are, for the error line ("Restored trap", "Partial-damage").</param>
	internal static LiveWorldWriteOutcome Run<TRow>(
		IReadOnlyList<TRow> rows,
		Func<TRow, bool> apply,
		Func<TRow, string> identity,
		ILogger log,
		string what)
	{
		var applied = 0;
		RunContained(rows, row =>
		{
			if (apply(row))
			{
				applied++;
			}
		}, identity, log, what);

		return new LiveWorldWriteOutcome(applied, rows.Count - applied);
	}

	/// <summary>
	/// The same containment for an applier that keeps its OWN accounting — a table that
	/// already classifies a row as written, unchanged, or refused by its own rule. Returns
	/// how many rows threw, which is the number that applier adds to its refused total, so
	/// the caller's counts stay exact and no row is counted twice.
	/// </summary>
	/// <param name="rows">The rows the cut recorded, in the order the applier applies them.</param>
	/// <param name="apply">The adapter's per-row write, with its own accounting inside.</param>
	/// <param name="identity">How the error line names ONE row (the adapter's knowledge, never a generic index).</param>
	/// <param name="log">The applier's logger — the row's identity and its exception ride one error line.</param>
	/// <param name="what">What the rows are, for the error line ("Restored trap", "Partial-damage").</param>
	internal static int RunContained<TRow>(
		IReadOnlyList<TRow> rows,
		Action<TRow> apply,
		Func<TRow, string> identity,
		ILogger log,
		string what)
	{
		var thrown = 0;
		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			try
			{
				// Per ROW, never around the loop: the row that threw cannot take the rows
				// behind it with it, and the count it is missing from stays exact.
				apply(row);
			}
			catch (Exception ex)
			{
				thrown++;
				if (thrown <= DetailedFailures)
				{
					log.LogError(ex, "{What} {Row} reached an engine call the local world cannot serve and is REFUSED.", what, Describe(identity, row));
				}
			}
		}

		if (thrown > DetailedFailures)
		{
			log.LogError("{What}: {Remaining} further row(s) were refused the same way — {Total} refused in all.", what, thrown - DetailedFailures, thrown);
		}

		return thrown;
	}

	/// <summary>
	/// The same containment for a loop whose unit is a LIVE WORLD OBJECT rather than one of
	/// the cut's rows — the two native tables that iterate what the world currently holds and
	/// count the RESTORED rows they matched (a keypad's <c>Openable</c>, a <c>GeyserScript</c>).
	/// The live object's position is the only identity such a loop has, and "matched rows" is
	/// the count it reports.
	///
	/// A throw in one live object cannot cost the objects behind it. The caller keeps its own
	/// count and increments it only after the matched row's write path COMPLETED, so an object
	/// that threw stays out of <c>applied</c>. That is why this shape returns NOTHING: the
	/// callers that report refusals compute them as (restored rows - applied), so a throwing
	/// object is already inside that count, and a number returned here and added to a row-based
	/// refused total would count the same object twice. (The two callers that report no refusal
	/// at all — the live broadcasts, which log <c>applied</c> — are covered by the same rule.)
	/// </summary>
	/// <param name="live">The live world's objects, in the order the applier iterates them.</param>
	/// <param name="apply">The adapter's per-object write, counting the matched row itself once its write path completed.</param>
	/// <param name="identity">How the error line names ONE live object (the adapter's knowledge: its position).</param>
	/// <param name="log">The applier's logger — the object's identity and its exception ride one error line.</param>
	/// <param name="what">What the objects are, for the error line ("restored keypad code").</param>
	internal static void RunLiveWorld<TLive>(
		IReadOnlyList<TLive> live,
		Action<TLive> apply,
		Func<TLive, string> identity,
		ILogger log,
		string what) =>
		// The per-object loop is the row rule's loop, bounded log included; only the counting
		// contract differs, and that one belongs to the caller (see the summary).
		_ = RunContained(live, apply, identity, log, what);

	/// <summary>
	/// How the error line names ONE unit, made SAFE: the identity is the adapter's knowledge,
	/// and for a live object it can read the very member the failed write touched (a position on
	/// an object whose component just threw). Naming it must therefore never be able to replace
	/// the original failure with one of its own — an identity that cannot be produced is reported
	/// as unknown, the loop goes on, and the count stays exact.
	/// </summary>
	private static string Describe<TRow>(Func<TRow, string> identity, TRow row)
	{
		try
		{
			return identity(row);
		}
		catch (Exception)
		{
			return "<identity unavailable>";
		}
	}
}
