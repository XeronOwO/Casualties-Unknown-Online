namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item reached Terminal and can never be resurrected.
/// </summary>
public sealed record ItemDestroyedEvent(
	ItemIdentity Identity,
	ulong Revision,
	ItemLocation TerminalLocation,
	TerminalKind Kind) : ItemEvent(Identity);
