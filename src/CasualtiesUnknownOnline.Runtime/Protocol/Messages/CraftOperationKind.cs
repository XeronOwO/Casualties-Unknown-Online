namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Which crafting operation a CraftReportMsg describes. Diagnostic only — the
/// host's apply never branches on it (the entries carry the full facts).
/// Craft = 0 is the wire default (protobuf omits zero values; the omission
/// decodes back to Craft transparently, so the default enum value must be the
/// semantic default).
/// </summary>
public enum CraftOperationKind
{
	Craft = 0, // Recipe.TryMake (the crafting menu)
	Combine = 1, // Body.CombineItems (drag-combine: gun/mag, mag/round, condition merge)
	LiquidTransfer = 2, // LiquidTransfer.Finish → Body.CombineLiquids
}
