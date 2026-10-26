namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The pointer question CUO's member menu has to ask before it answers a right-click: is a world ITEM under the pointer?
///
/// <para>
/// The game answers it first on every right-press — <c>PlayerCamera.TryPickupFromWorld</c> (<c>PlayerCamera.cs:1423-1441</c>) raycasts the <c>Item</c>
/// layer before anything else and returns with the item as the current drag — so an item under the pointer owns the press and CUO's menu must stay out
/// of it. The question needs the game's scene, so it lives here as one narrow query; the menu is in the plug-in and must not know game types.
/// </para>
///
/// <para>
/// Two floats, not a <c>Vector2</c>: this assembly is engine-free, and the caller's point is a GUI coordinate pair (the plug-in's IMGUI event, origin
/// top-left). The implementation converts to the screen/world space the native call uses.
/// </para>
/// </summary>
public interface IWorldItemPointerQuery
{
	/// <summary>True when a world item lies under the given GUI point — the press belongs to the game's pickup, not to CUO's menu.</summary>
	bool IsWorldItemUnderPointer(float guiX, float guiY);
}
