using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The end-of-layer choice's control surface an end-of-layer packet handler may use.</summary>
public interface ILayerAdvanceHandlerContext
{
	ILayerAdvanceControl LayerAdvance { get; }
}
