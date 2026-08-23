using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The world-time control surface a world-time packet handler may use.</summary>
public interface IWorldTimeHandlerContext
{
	IWorldTimeControl WorldTime { get; }
}
