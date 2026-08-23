using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + character-data + enemy-sync control surface an enemy/body packet handler may use.</summary>
public interface IEnemyCharacterSessionHandlerContext
{
	ISessionControl Session { get; }
	ICharacterDataControl CharacterData { get; }
	IEnemySyncControl Enemies { get; }
}
