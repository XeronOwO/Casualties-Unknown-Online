namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One authored entry of a game entity's own drop table, as the prefab's
/// Inspector data holds it (<c>BuildingEntity.itemsDropOnDestroy</c> and
/// <c>alwaysDrop</c>): an item id, the CHANCE that entry's own roll is gated on,
/// and the condition window that roll asks for.
/// <para>
/// It is the raw row, not the bound: a declaration the spawner can never roll
/// still exists here and is dropped when the table resolves it into a
/// <see cref="DropBound"/> — the same split as a mod tile's authored
/// <c>IModTileDefinition.Drops</c> against the rows
/// <c>GameBlockDropTable.ForTile</c> builds from them. The chance is carried for
/// the bucket that reads it (<c>itemsDropOnDestroy</c>); the always-drop bucket
/// ignores it, because the game does.
/// </para>
/// </summary>
internal readonly record struct AuthoredDrop(string ItemId, float Chance, float MinCondition, float MaxCondition);
