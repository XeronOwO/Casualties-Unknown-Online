using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// What one reconcile of the live world against an authoritative item set did:
/// how many entries landed (bound to a matching local object, or materialized
/// because nothing matched), how many unclaimed local objects were destroyed, and
/// which entries the live world did not take at all.
/// </summary>
/// <param name="Entries">The authoritative set's size.</param>
/// <param name="Bound">Entries that adopted an existing local object.</param>
/// <param name="Materialized">Entries that created a new object (nothing local matched).</param>
/// <param name="Destroyed">Standalone local world items no entry claimed.</param>
/// <param name="Refused">The entries that could not be landed, named for the restore report.</param>
internal readonly record struct GeneratedItemReconcileOutcome(
	int Entries,
	int Bound,
	int Materialized,
	int Destroyed,
	IReadOnlyList<string> Refused)
{
	internal int Applied => Bound + Materialized;

	internal bool Complete => Refused.Count == 0;
}
