namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// What happened to one entry of a CraftReportMsg. Destroyed = 0 is the wire
/// default (protobuf omits zero values; the omission decodes back to Destroyed
/// transparently, so the default enum value must be the semantic default).
/// The host classifies Destroyed entries by table membership (world table vs
/// transfer table vs unknown) — the disposition itself carries no apply logic.
/// </summary>
public enum CraftEntryDisposition
{
	Destroyed = 0, // the item no longer exists (material consumed, mag/round loaded)
	Changed = 1, // the item still exists with changed state (condition/liquids/components) — PostState is the post-operation digest
}
