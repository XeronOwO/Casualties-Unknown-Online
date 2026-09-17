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
					log.LogError(ex, "{What} row {Row} reached an engine call the local world cannot serve and is REFUSED.", what, identity(row));
				}
			}
		}

		if (thrown > DetailedFailures)
		{
			log.LogError("{What}: {Remaining} further row(s) were refused the same way — {Total} refused in all.", what, thrown - DetailedFailures, thrown);
		}

		return thrown;
	}
}
