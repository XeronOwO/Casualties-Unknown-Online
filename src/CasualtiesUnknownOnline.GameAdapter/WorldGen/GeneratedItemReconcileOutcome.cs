using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// What one reconcile of the live world against an authoritative item set did:
/// how many entries the live world took (bound to a matching local object,
/// materialized because nothing matched, or deferred to the landing pipeline
/// because its local object may still be registering), how many unclaimed local
/// objects were destroyed, and which entries the live world did not take at all.
/// </summary>
/// <param name="Entries">The authoritative set's size.</param>
/// <param name="Bound">Entries that adopted an existing local object.</param>
/// <param name="Materialized">Entries that created a new object (nothing local matched).</param>
/// <param name="Deferred">Entries the landing pipeline holds for a local object that may still be landing; the pump adopts or materializes them within the grace while the queue survives it, so they are neither a write nor a refusal (batch 20261002-j's review, major-4). A queue cleared inside the grace drops them instead — that drop is reported, never silent.</param>
/// <param name="Destroyed">Standalone local world items no entry claimed.</param>
/// <param name="Refused">The entries that could not be landed, named for the restore report.</param>
internal readonly record struct GeneratedItemReconcileOutcome(
	int Entries,
	int Bound,
	int Materialized,
	int Deferred,
	int Destroyed,
	IReadOnlyList<string> Refused)
{
	/// <summary>Every entry the live world or its landing pipeline took: a deferred entry is owned by the pump (which lands it while the queue survives the grace), so counting it as a refusal would report a restore incomplete that is not — and a queue cleared inside the grace reports its own drop.</summary>
	internal int Applied => Bound + Materialized + Deferred;

	internal bool Complete => Refused.Count == 0;
}
