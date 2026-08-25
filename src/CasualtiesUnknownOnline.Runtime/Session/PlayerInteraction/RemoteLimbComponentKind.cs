namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The component-bearing limb tools supported by the cross-player item-use
/// slice. The Runtime only carries this neutral kind; the Game Adapter maps it
/// to the actual game component type when applying the wire state to a body.
/// </summary>
public enum RemoteLimbComponentKind
{
	None,
	Splint,
	Tourniquet,
	Icepack,
}
