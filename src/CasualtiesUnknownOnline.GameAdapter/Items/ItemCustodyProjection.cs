using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The custody projection, in SHADOW mode: it computes what the kernel's rows say this client should be showing, compares that with the
/// objects actually in the scene, and logs every disagreement — it changes nothing yet. The comparison itself is
/// <see cref="ItemCustodyComparison"/> (pure, covered without a game); this class is only the wiring that feeds it, because that is the half
/// that must be proven on a running session before any reader is switched to it.
///
/// <para>
/// Why it exists: the adapter's five stores of "where is it" disagree, and every item loss of 2026-10-25 was one of those disagreements. The
/// kernel already answers the question once (`ItemState.Location`), so the projection asks it and nothing else. Stage 1 of
/// `docs/backlog/todo/item-custody-single-authority.md` is this shadow pass; stage 2 switches the readers and stage 3 makes the mutations
/// issue commands.
/// </para>
///
/// <para>
/// It is throttled to one comparison per second: a mismatch is a state, not an event, and a per-frame line would bury it.
/// </para>
/// </summary>
internal sealed class ItemCustodyProjection(IItemControl items, ILogger<ItemCustodyProjection> log)
{
	private const float IntervalSeconds = 1f;

	private readonly IItemControl _items = items;
	private readonly ILogger<ItemCustodyProjection> _log = log;
	private float _nextAt;
	private bool _reportedClean;

	internal void Tick(ulong localSteamId)
	{
		var now = Time.realtimeSinceStartup;
		if (now < _nextAt)
		{
			return;
		}

		_nextAt = now + IntervalSeconds;

		var rows = _items.QueryJudgedItems();
		if (rows.Count == 0)
		{
			return;
		}

		var objects = LocalObjects();
		var mismatches = ItemCustodyComparison.Compare(rows, objects, localSteamId);
		if (mismatches.Count == 0)
		{
			if (!_reportedClean)
			{
				_reportedClean = true;
				_log.LogInformation("[ItemCustody] rows={Rows} objects={Objects} mismatches=0 — the kernel's rows and this client's objects agree.", rows.Count, objects.Count);
			}

			return;
		}

		_reportedClean = false;
		_log.LogWarning("[ItemCustody] rows={Rows} objects={Objects} mismatches={Count} — the first ones: {Mismatches}",
			rows.Count,
			objects.Count,
			mismatches.Count,
			Describe(mismatches, rows));
	}

	/// <summary>
	/// Every item object this client currently has, with the kind the projection expects: a row this client owns is REAL, every other is a
	/// DISPLAY it incarnates. The kind is read from the object itself — the two markers the adapter already keeps — so the projection never
	/// decides from a second bookkeeping store, which is the defect it exists to end.
	/// </summary>
	private static List<ItemCustodyComparison.LocalObject> LocalObjects()
	{
		var found = new List<ItemCustodyComparison.LocalObject>();
		var all = Object.FindObjectsOfType<Item>();
		foreach (var item in all)
		{
			var id = item.GetComponent<ItemInstanceId>();
			if (id == null || id.Id == 0) // Unity object — ==
			{
				continue;
			}

			var display = item.GetComponent<RemoteCloneRender>() != null || StandingItems.Is(item); // Unity object — ==
			found.Add(new ItemCustodyComparison.LocalObject(id.Id, display));
		}

		return found;
	}

	private static string Describe(IReadOnlyList<ItemCustodyComparison.Mismatch> mismatches, IReadOnlyDictionary<ulong, ItemState> rows)
	{
		var parts = new List<string>();
		var count = 0;
		foreach (var mismatch in mismatches)
		{
			if (count++ == 5)
			{
				break;
			}

			var kind = rows.TryGetValue(mismatch.InstanceId, out var row) ? row.Location.Kind.ToString() : "no-row";
			parts.Add($"{mismatch.InstanceId}:{mismatch.Kind}:{kind}");
		}

		return string.Join(", ", parts);
	}
}
