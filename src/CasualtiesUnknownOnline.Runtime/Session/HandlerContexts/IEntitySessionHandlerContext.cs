using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + entity-sync control surface an entity/join packet handler may use.</summary>
public interface IEntitySessionHandlerContext
{
	ISessionControl Session { get; }
	IEntitySyncControl Entities { get; }
}
