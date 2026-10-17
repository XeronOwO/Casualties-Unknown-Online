using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One parsed <c>/give</c> line, before anything is created: the ONE body that
/// receives the item, the definition it names, the destination slot the operator
/// asked for (-1 = the first free one) and the property tokens that follow.
///
/// <para>
/// The property tokens stay TOKENS here on purpose. The same list is what a
/// member sends to the host, and the host re-parses it against its own capture of
/// the definition — so the parse that judges the values happens on the side that
/// creates the item, not on the side that asked. The local client parses them too
/// (through <see cref="ItemGiveParser"/>'s property entry), so a malformed line is
/// refused before it is sent rather than after a round trip.
/// </para>
/// </summary>
internal sealed record ItemGiveRequest(
	string? Selector,
	string DefinitionId,
	int Slot,
	IReadOnlyList<string> PropertyTokens);
