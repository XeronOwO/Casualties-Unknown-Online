using UnityTime = UnityEngine.Time;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The one object a cross-player eat's delegate CREATED to replace the item it was handed — the peel's opened fruit
/// (<c>Item.cs:2619</c>: <c>body.PickUpItem(Instantiate(Resources.Load("stonefruitopen"), …))</c>).
///
/// <para>
/// The object is the OWNER's row becoming another definition in place: it must get no instance id, no spawn and no pickup
/// report (its definition travels back in the eat's outcome instead), and the eat destroys the object once it has read it.
/// Two hooks see it — the instantiation (<c>ItemWorldSync.OnItemInstantiated</c>) and the delegate's own
/// <c>Body.PickUpItem</c> (<c>PickupSync.OnPickedUp</c>) — so the observation is published on the scope rather than read
/// back from a slot: the replacement's slot is wherever the game's own pickup put it, which is not necessarily the slot the
/// eat arranged, and reading a slot missed it (measured 2026-10-26: the eat reported no replacement and the owner's row was
/// destroyed instead of becoming the opened fruit).
/// </para>
///
/// <para>
/// Both hooks ask <see cref="CallContext.Origin.ItemReplacementCreation"/>, so a creation outside the eat's window can never
/// reach this holder; it is cleared when the eat opens its window and consumed when it reads the result.
/// </para>
/// </summary>
internal static class ItemReplacementObservation
{
	private static Item? _created;
	private static int _armedUntilFrame = -1;

	/// <summary>
	/// Opens a fresh window: one eat, at most one replacement, armed for this frame AND the next. The game's own pickup of the
	/// created object is not guaranteed to run inside the delegate's call, and a scope alone therefore missed it (measured
	/// 2026-10-26: the replacement still got a fresh id and a world spawn at the origin while the eat logged nothing).
	/// </summary>
	internal static void Arm()
	{
		_created = null;
		_armedUntilFrame = UnityTime.frameCount + 1;
	}

	/// <summary>
	/// Closes the window once the eat has read the result, so a later creation can never be swallowed by it. The OBJECT stays known: the report that leaked
	/// (measured 2026-10-29: <c>[ItemSpawned] local stonefruitopen … reported at (0.0,0.0), fresh False</c>, a fresh id and a world spawn at the origin)
	/// arrived on a frame AFTER the eat returned, outside any window — identity is what makes it a replacement, not the frame it was created on.
	/// </summary>
	internal static void Disarm() => _armedUntilFrame = -1;

	/// <summary>True while this frame or the previous one ran an eat's delegate — what every creation and pickup hook asks first.</summary>
	internal static bool IsArmed => UnityTime.frameCount <= _armedUntilFrame;

	/// <summary>
	/// True for the exact object this window's delegate created, on whichever frame a hook meets it. The frame window alone missed a report that ran after the
	/// eat returned, so the hooks ask this as well: the replacement is never a world item, no matter when it is first seen.
	/// </summary>
	internal static bool IsTheCreatedReplacement(Item item) => _created != null && item == _created; // Unity object — ==

	/// <summary>The object the delegate created, when this client saw one — otherwise <c>null</c>.</summary>
	internal static Item? Created => _created;

	/// <summary>Records the object a hook saw created or picked up inside the eat's window (first one wins).</summary>
	internal static void Record(Item item)
	{
		if (_created == null) // Unity object — ==
		{
			_created = item;
		}
	}
}
