using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marker on an item spawned by BuildingEntity.Update's local death branch
/// (BuildingEntity.cs:74-120). Unlike a block break, the death branch creates
/// drops with Object.Instantiate(Resources.Load(...)) rather than Utils.Create,
/// so a Utils.Create postfix can never see them. Item.Awake runs synchronously
/// inside that Instantiate call; while CallContext.Origin.BuildingDeathDrop is
/// active this marker is attached so ItemWorldSync can tell a building-death
/// drop apart from a block drop and an ordinary runtime spawn.
/// </summary>
internal sealed class BuildingDeathDropOrigin : MonoBehaviour
{
}
