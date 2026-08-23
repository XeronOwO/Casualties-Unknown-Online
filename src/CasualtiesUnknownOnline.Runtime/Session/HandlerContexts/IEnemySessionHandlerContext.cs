using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + enemy-sync control surface an enemy packet handler may use.</summary>
public interface IEnemySessionHandlerContext
{
	ISessionControl Session { get; }
	IEnemySyncControl Enemies { get; }
}
