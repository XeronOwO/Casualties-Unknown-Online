namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host-stamped apply routing for a CraftEntryMsg's relay receivers. None = 0
/// is the wire default (protobuf omits zero values; the omission decodes back
/// to None transparently) — the report side leaves it None, the host stamps it
/// from its table membership before relaying (the guests' tables are empty,
/// so they cannot classify themselves; and a wrong guess would either skip a
/// world correction or spray warnings over absent carried items).
/// </summary>
public enum CraftApplyKind
{
	None = 0, // carried/unknown — the receivers need no scene action (clone fact tables heal via the 1 Hz snapshot)
	WorldCorrection = 1, // a Changed world item — the receivers correct their scene copy through the correction machinery
}
