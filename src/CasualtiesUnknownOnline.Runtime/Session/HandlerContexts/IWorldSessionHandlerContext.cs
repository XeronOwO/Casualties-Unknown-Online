using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + world control surface a world/session packet handler may use.</summary>
public interface IWorldSessionHandlerContext
{
	ISessionControl Session { get; }
	IWorldControl World { get; }
}
