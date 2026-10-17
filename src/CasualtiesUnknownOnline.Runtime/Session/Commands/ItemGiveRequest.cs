namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One parsed <c>/give</c> line, before anything is created: the ONE body that receives the
/// item, the definition it names, the destination slot the operator asked for (-1 = the first
/// free one) and the state attached to the item id.
///
/// <para>
/// <see cref="StateText"/> is the CANONICAL form of the state — the text a member sends and the
/// host re-parses against its own capture of the definition, so the parse that judges the values
/// happens on the side that creates the item, not on the side that asked. An empty text means
/// "the definition's own captured state"; the local client parses it too
/// (<see cref="ItemGiveParser.TryParseState"/>), so a malformed line is refused before it is
/// sent rather than after a round trip.
/// </para>
/// </summary>
internal sealed record ItemGiveRequest(
	string? Selector,
	string DefinitionId,
	int Slot,
	string StateText);
