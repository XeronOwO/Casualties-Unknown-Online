using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + character-data control surface a character packet handler may use.</summary>
public interface ICharacterSessionHandlerContext
{
	ISessionControl Session { get; }
	ICharacterDataControl CharacterData { get; }
}
