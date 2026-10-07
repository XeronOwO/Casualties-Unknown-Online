using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The Game Adapter's answer to <see cref="IWearSemantics"/>: the game's own item
/// data for the placement, and the game's own limb layout for the index
/// <c>Body.LimbByName</c> resolves it to. Registered as its own singleton and
/// replacing the Runtime's <see cref="NoWearSemantics"/> default — the same
/// standalone-service shape the two sibling content seams use, and the same
/// reason: reaching it through <c>GameAdapter</c> would close a DI constructor
/// cycle.
/// <para>
/// The limb index is answered off this client's OWN body. That is a statement
/// about the character prefab's limb layout, not about the wearer's health: the
/// session runs one game build, so every body — the host's, a guest's, a remote
/// render clone — carries the same limbs in the same order under the same names,
/// which is why the host may answer the index while the affected side still owns
/// everything STATEFUL about the limb (whether it is attached, what it wears).
/// </para>
/// </summary>
internal sealed class GameWearSemantics : IWearSemantics
{
	public bool TryGetWearPlacement(string itemId, out int limbIndex, out string wearSlotId) =>
		GameWearPlacement.TryResolve(LocalBody(), itemId, out limbIndex, out wearSlotId);

	/// <summary>The body the limb layout is read from — the local one, the same scene read <c>LocalCharacterCapture</c> uses.</summary>
	private static Body? LocalBody()
	{
		var camera = PlayerCamera.main;
		return camera != null ? camera.body : null; // Unity object — ==
	}
}
