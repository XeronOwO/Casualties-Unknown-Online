using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks an item object as a clone-inventory render. Slot renders live inside
/// the slot (which only ever holds items), but limb renders (worn items) share
/// the limb with the game's own children (bones, decorations) — the marker is
/// what the renderer destroys on update without touching the game's objects.
/// </summary>
internal sealed class RemoteCloneRender : MonoBehaviour
{
}
