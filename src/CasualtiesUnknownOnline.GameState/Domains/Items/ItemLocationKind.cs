namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The five item location families in the kernel model.
/// <para>
/// <see cref="Applied"/> is the one family that is NOT an inventory position:
/// the item is the installed form of a limb component (a splint or a tourniquet
/// the game turned into a <c>SplintLimb</c> / <c>TourniquetScript</c>), it is
/// held by the limb rather than carried, and no inventory gesture reaches it. It
/// is deliberately NOT the wear encoding — a worn item is CARRIED with a negative
/// slot index, so it is still in the inventory and a search can reach it, which
/// is exactly what an applied item is not.
/// </para>
/// </summary>
public enum ItemLocationKind
{
	World,
	Carried,
	Contained,
	Applied,
	Terminal,
}
