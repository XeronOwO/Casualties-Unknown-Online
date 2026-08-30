namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Pure classification for <see cref="ItemDropProvenance"/> from the markers
/// the adapter attaches at creation time.
/// </summary>
internal static class ItemDropProvenanceClassifier
{
	internal static ItemDropProvenance Classify(bool isBlockDrop, bool isBuildingDeathDrop)
	{
		// Block drops win when both markers somehow coexist: the block-break
		// fold is the proven older path and a building entity never runs inside
		// a DamageBlockOrigin scope.
		if (isBlockDrop)
		{
			return ItemDropProvenance.BlockDrop;
		}

		return isBuildingDeathDrop
			? ItemDropProvenance.BuildingDeathDrop
			: ItemDropProvenance.Normal;
	}
}
