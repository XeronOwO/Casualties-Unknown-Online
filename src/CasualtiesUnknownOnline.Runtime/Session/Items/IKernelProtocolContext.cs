using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Narrow handler context for the Phase C kernel-envelope packet handler. The
/// handler itself stays in the Runtime: it is the transport side of the seam
/// (frame decode, traffic accounting, dispatch), while the protocol surface it
/// hands the frame to lives in the Application layer.
/// </summary>
public interface IKernelProtocolContext
{
	IKernelProtocolControl KernelProtocol { get; }
}
