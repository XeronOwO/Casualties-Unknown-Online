using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The world + kernel-protocol control surface a world-instruction packet handler may use.</summary>
public interface IWorldKernelHandlerContext
{
	IWorldControl World { get; }

	IKernelProtocolControl KernelProtocol { get; }
}
