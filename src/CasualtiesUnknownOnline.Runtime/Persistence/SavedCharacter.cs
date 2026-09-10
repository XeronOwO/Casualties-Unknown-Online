using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One <c>characters/&lt;playerKey&gt;.json</c> of a snapshot: the character
/// snapshot the game's own restore path consumes, bound to the transport-scoped
/// key it was written under (§3.4, decision 162).
/// </summary>
public sealed record SavedCharacter(string PlayerKey, CharacterDataMsg Character);
