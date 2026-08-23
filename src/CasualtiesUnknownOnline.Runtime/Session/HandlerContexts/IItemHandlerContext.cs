using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The item-domain control surface an item packet handler may use.</summary>
public interface IItemHandlerContext
{
	IItemControl Items { get; }
}
