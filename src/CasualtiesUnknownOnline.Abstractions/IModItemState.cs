namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One read-only item fact, read by instance id through
/// <see cref="IModGameState.TryGetItem"/>. It answers "what is this item, and
/// where is it now" for an id a mod already holds, wherever the item has moved
/// since — including onto a limb, which no inventory projection lists.
///
/// <para>
/// A member a location does not use carries its neutral value, the same
/// one-shape rule the framework's own location model follows, so
/// <see cref="Location"/> is what tells a caller which members mean something:
/// the owner and the limb for <see cref="ModItemLocation.Applied"/>, the owner
/// for <see cref="ModItemLocation.Carried"/> and
/// <see cref="ModItemLocation.Contained"/>, the position for
/// <see cref="ModItemLocation.World"/> (which may carry a container as well),
/// and nothing but identity for <see cref="ModItemLocation.Terminal"/>.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModItemState
{
	/// <summary>The framework item-instance id this fact is about — the id the caller asked for.</summary>
	ulong InstanceId { get; }

	/// <summary>The game item definition id.</summary>
	string ItemId { get; }

	/// <summary>Where the item is; the members below that mean something follow from this.</summary>
	ModItemLocation Location { get; }

	/// <summary>
	/// The Steam 64-bit id of the player this item is with, for a carried,
	/// contained or applied item; 0 when the location names no player.
	/// </summary>
	ulong OwnerSteamId { get; }

	/// <summary>
	/// The index into that body's limb array of the limb the item is installed
	/// on — the same index the character state uses — for
	/// <see cref="ModItemLocation.Applied"/>; -1 otherwise.
	/// </summary>
	int LimbIndex { get; }

	/// <summary>
	/// The id of the container this item belongs to, or 0 when it belongs to no
	/// container. An item in the <see cref="ModItemLocation.Contained"/> family is
	/// INSIDE it; a <see cref="ModItemLocation.World"/> item can carry one as well,
	/// because a world container's contents are recorded as world items bound to
	/// their container — so a non-zero parent is not the same fact as "contained",
	/// and this member is read together with <see cref="Location"/>.
	/// </summary>
	ulong ParentItemId { get; }

	/// <summary>World X for <see cref="ModItemLocation.World"/>; 0 otherwise.</summary>
	float X { get; }

	/// <summary>World Y for <see cref="ModItemLocation.World"/>; 0 otherwise.</summary>
	float Y { get; }

	/// <summary>
	/// The item's own condition (durability / charge / consumable amount) as the
	/// framework last recorded it. An applied item's condition follows the limb
	/// component the game decays, so it trails the component on screen by at
	/// most a step.
	/// </summary>
	float Condition { get; }

	/// <summary>The item's OWN favourite flag — the flag the item carries, whoever set it. A local favourite mark (user ruling 2026-10-11) is a different thing and is not projected here.</summary>
	bool Favourited { get; }
}
