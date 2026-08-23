namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session-only control surface a session packet handler may use.</summary>
public interface ISessionHandlerContext
{
	ISessionControl Session { get; }
}
