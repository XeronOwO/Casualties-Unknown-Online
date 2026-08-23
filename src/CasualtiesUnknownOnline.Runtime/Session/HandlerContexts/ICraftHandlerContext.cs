using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The crafting control surface a craft packet handler may use.</summary>
public interface ICraftHandlerContext
{
	ICraftControl Craft { get; }
}
