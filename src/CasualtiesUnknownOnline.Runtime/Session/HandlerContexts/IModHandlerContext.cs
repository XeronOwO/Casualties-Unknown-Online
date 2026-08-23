using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The mod-domain control surface a mod packet handler may use.</summary>
public interface IModHandlerContext
{
	IModsControl Mods { get; }
}
