using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The one live set of local favourite marks of this client. The decision half
/// lives in the runtime (<see cref="LocalFavouriteMarks"/>, covered without a
/// game scene); the seams that read it are static — the clone inventory
/// renderer's identity helper and the while-dragging patch — so this holder is
/// how they reach it, exactly like <see cref="RemoteDragIntentWindow"/> reaches
/// the release window. One local client can only be one player in one world.
/// </summary>
internal static class LocalFavouriteMarkStore
{
	internal static LocalFavouriteMarks Current { get; } = new();
}
