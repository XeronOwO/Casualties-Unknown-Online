using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The character-data control surface a character-only packet handler may use.</summary>
public interface ICharacterDataHandlerContext
{
	ICharacterDataControl CharacterData { get; }
}
