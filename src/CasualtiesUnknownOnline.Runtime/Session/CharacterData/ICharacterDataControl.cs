using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The character-data surface packet handlers operate on — implemented by
/// CharacterDataStore. Handlers depend on this narrow interface instead of the
/// concrete service, which keeps the constructor graph acyclic (abstract
/// extraction, user rule).
/// </summary>
public interface ICharacterDataControl
{
	void SaveCharacterData(ulong steamId, CharacterDataMsg msg);

	void SendSavedCharacter(ulong steamId);

	/// <summary>Host: the latest report per SteamID (clone inventory rendering on body creation).</summary>
	CharacterDataMsg? GetSavedCharacter(ulong steamId);

	/// <summary>Host only: broadcast the host's own character snapshot (the guests render the host's clone inventory from it).</summary>
	void BroadcastHostCharacterData(CharacterDataMsg msg);

	void FireCharacterDataReceived(ulong sender, CharacterDataMsg msg);

	/// <summary>Guest: the host's own character snapshot arrived — render its clone inventory (never apply to the local body).</summary>
	void FireHostCharacterDataReceived(CharacterDataMsg msg);
}
