namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host-stamped apply routing for a CraftEntryMsg's relay receivers. None = 0
/// is the wire default (protobuf omits zero values; the omission decodes back
/// to None transparently) — the report side leaves it None, the host stamps it
/// from its own tables before relaying (the guests' tables are empty, so they
/// cannot classify themselves; and a wrong guess would either skip a world
/// action, retire a carried row that still exists, or leave a destroyed row
/// standing).
/// </summary>
public enum CraftApplyKind
{
	/// <summary>No action on the receivers: a Changed entry belongs to the reporting side's own carried record (the host adopts it), and a Destroyed entry the host could not attribute to the sender is skipped by both halves.</summary>
	None = 0,

	/// <summary>A Changed world item — the receivers correct their scene copy through the correction machinery.</summary>
	WorldCorrection = 1,

	/// <summary>A Destroyed world item — the receivers remove their scene copy; the item was never in a clone fact table.</summary>
	WorldDestroy = 2,

	/// <summary>A Destroyed carried row the host attributed to the report's sender (its transfer table, or its kernel row when the registration was lost) — the receivers retire THAT owner's clone fact-table row.</summary>
	CarriedRetire = 3,
}
