using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

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

	void FireCharacterDataReceived(CharacterDataMsg msg);
}
