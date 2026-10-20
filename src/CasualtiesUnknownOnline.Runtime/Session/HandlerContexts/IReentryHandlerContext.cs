using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The world re-entry request's control surface a re-entry packet handler may use.</summary>
public interface IReentryHandlerContext
{
	IReentryControl Reentry { get; }
}
