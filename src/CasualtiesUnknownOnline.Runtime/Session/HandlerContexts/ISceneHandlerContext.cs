using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The scene-state/entity/character/world control surface the scene-state handler may use.</summary>
public interface ISceneHandlerContext
{
	ISessionControl Session { get; }
	IEntitySyncControl Entities { get; }
	ICharacterDataControl CharacterData { get; }
	IWorldControl World { get; }
}
