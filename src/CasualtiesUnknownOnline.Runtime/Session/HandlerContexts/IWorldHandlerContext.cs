using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The world-domain control surface a world packet handler may use.</summary>
public interface IWorldHandlerContext
{
	IWorldControl World { get; }
}
