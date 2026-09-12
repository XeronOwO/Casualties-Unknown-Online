using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Lands an authoritative item set on the live world: one entry, one physical
/// object. The algorithm is "bind, materialize, drop the leftovers" —
/// a local object at the entry's spot with the same definition adopts the
/// entry's id and state, an entry nothing local matches is materialized, and a
/// standalone world item no entry claimed is destroyed.
///
/// Two callers share it because both are looking at objects the LOCAL generation
/// just created that must BECOME the authority's objects rather than live beside
/// them: the guest applying the host's generation snapshot
/// (<see cref="GeneratedItemApplication"/>) and the host applying a restored
/// cut's world items to the layer it just regenerated
/// (<see cref="GeneratedItemAuthority"/>). Without the shared step the host would
/// publish the regenerated objects under fresh ids and leave two item families at
/// one physical spot.
///
/// The whole application runs under the remote-apply guard: binding an id to a
/// freshly generated object must not be reported back as a local spawn.
/// </summary>
internal sealed class GeneratedItemReconcile(
	ItemApplication itemApplication,
	ILogger<GeneratedItemReconcile> log)
{
	private readonly ItemApplication _itemApplication = itemApplication;
	private readonly ILogger<GeneratedItemReconcile> _log = log;

	internal GeneratedItemReconcileOutcome Apply(IReadOnlyList<WorldItem> entries)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var bound = 0;
			var materialized = 0;
			var refused = new List<string>();
			foreach (var entry in entries)
			{
				if (ItemApplication.FindExistingAt(entry.Pos, entry.Item.ItemId) != null) // Unity object — ==
				{
					bound++; // the local copy adopts the authority's id (SpawnWorldItem binds, never duplicates)
				}
				else
				{
					materialized++; // a divergent local copy — the authority's version is materialized instead
				}

				_itemApplication.SpawnWorldItem(entry);

				// A write is only reported after it was verified BY ID: the bind
				// predicate above deliberately skips objects that already carry an id, so
				// it cannot see the write that just attached one. Reporting the intent
				// instead of the verified result would name a successful bind as a loss on
				// every restore, and a genuine refusal could not be told apart from it (§6).
				if (ItemApplication.FindWorldItem(entry.ItemId) == null) // Unity object — ==
				{
					refused.Add($"item #{entry.ItemId} ({entry.Item.ItemId}) at ({entry.Pos.X:F1},{entry.Pos.Y:F1})");
				}
			}

			// Reconciliation: destroy the standalone world items no entry claimed.
			// Bound copies carry the authority's id and are untouched.
			var destroyed = 0;
			foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
			{
				if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==
				{
					continue;
				}

				if (!ItemWorldSync.IsStandaloneWorldItem(item))
				{
					continue;
				}

				Object.Destroy(item.gameObject);
				destroyed++;
			}

			_log.LogInformation(
				"[ItemReconcile] {Entries} authoritative entry/entries: {Bound} bound, {Materialized} materialized, {Destroyed} unclaimed local(s) destroyed, {Refused} not taken.",
				entries.Count, bound, materialized, destroyed, refused.Count);
			return new GeneratedItemReconcileOutcome(entries.Count, bound, materialized, destroyed, refused);
		}
	}
}
