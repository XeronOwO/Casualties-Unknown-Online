using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
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

			// Per ENTRY, contained by the Runtime's row rule: an entry whose engine call throws
			// costs ITSELF and the entries behind it still land. The two counts are taken only
			// once the entry's write path completed, so a throwing entry is counted nowhere —
			// it is named in the containment's error line and counted in the refusal below.
			var thrown = ContainedRowLoop.RunContained(
				entries,
				entry =>
				{
					var localCopy = ItemApplication.FindExistingAt(entry.Pos, entry.Item.ItemId) != null; // Unity object — ==
					_itemApplication.SpawnWorldItem(entry);

					if (localCopy)
					{
						bound++; // the local copy adopts the authority's id (SpawnWorldItem binds, never duplicates)
					}
					else
					{
						materialized++; // a divergent local copy — the authority's version is materialized instead
					}

					// A write is only reported after it was verified BY ID: the bind
					// predicate above deliberately skips objects that already carry an id, so
					// it cannot see the write that just attached one. Reporting the intent
					// instead of the verified result would name a successful bind as a loss on
					// every restore, and a genuine refusal could not be told apart from it (§6).
					if (ItemApplication.FindWorldItem(entry.ItemId) == null) // Unity object — ==
					{
						refused.Add(Describe(entry));
					}
				},
				Describe,
				_log,
				"restored world item");

			if (thrown > 0)
			{
				refused.Add($"{thrown} restored world item row(s) reached an engine call the local world cannot serve");
			}

			// Reconciliation: destroy the standalone world items no entry claimed.
			// Bound copies carry the authority's id and are untouched. This loop iterates the LIVE
			// world and keeps its own count (destroyed), which is the other shape of the same rule:
			// one object whose destroy throws cannot cost the objects behind it, and a leftover the
			// cut never described that SURVIVES is a divergence the restore report must hear about.
			var destroyed = 0;
			var leftoverThrown = ContainedRowLoop.RunContained(
				Item.allItems.ToList(), // copy: destroying while iterating
				item =>
				{
					if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==
					{
						return;
					}

					if (!ItemWorldSync.IsStandaloneWorldItem(item))
					{
						return;
					}

					Object.Destroy(item.gameObject);
					destroyed++;
				},
				item => $"unclaimed item at ({item.transform.position.x:F1},{item.transform.position.y:F1})",
				_log,
				"unclaimed world item");

			if (leftoverThrown > 0)
			{
				refused.Add($"{leftoverThrown} unclaimed local item(s) could not be destroyed");
			}

			_log.LogInformation(
				"[ItemReconcile] {Entries} authoritative entry/entries: {Bound} bound, {Materialized} materialized, {Destroyed} unclaimed local(s) destroyed, {Refused} not taken.",
				entries.Count, bound, materialized, destroyed, refused.Count);
			return new GeneratedItemReconcileOutcome(entries.Count, bound, materialized, destroyed, refused);
		}
	}

	/// <summary>
	/// How one entry is named in the refusal list and in the containment's error line. Identity
	/// data the cut carries — never a live-object read, so naming an entry can never throw.
	/// </summary>
	private static string Describe(WorldItem entry) =>
		$"item #{entry.ItemId} ({entry.Item.ItemId}) at ({entry.Pos.X:F1},{entry.Pos.Y:F1})";
}
