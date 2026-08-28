namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Narrow handler context for the Phase C kernel-envelope packet handler.
/// </summary>
public interface IKernelProtocolContext
{
	IKernelProtocolControl KernelProtocol { get; }
}
