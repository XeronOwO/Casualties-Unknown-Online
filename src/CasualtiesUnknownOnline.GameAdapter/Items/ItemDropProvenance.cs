namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The provenance of a runtime-spawned world item. The classifier is a pure
/// function over the markers the adapter attaches at creation time, so the
/// distinction between a block-break drop, a building-entity death drop and an
/// ordinary runtime spawn is lockable without a live Unity scene.
/// </summary>
internal enum ItemDropProvenance
{
	Normal,
	BlockDrop,
	BuildingDeathDrop,
}
