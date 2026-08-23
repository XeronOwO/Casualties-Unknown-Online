using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The control surface used by the handshake handler (the broadest handler context by design).</summary>
public interface IHandshakeHandlerContext
{
	ISessionControl Session { get; }
	IEntitySyncControl Entities { get; }
	ICharacterDataControl CharacterData { get; }
	IWorldControl World { get; }
	IModsControl Mods { get; }
}
