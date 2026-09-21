using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The kernel wire codec the checkpoint helpers need: the production Runtime
/// adapter, so a test round-trips through exactly the conversions the wire path
/// uses. The checkpoint assembler takes it as an argument because the mapper it
/// wraps still carries the legacy item/enemy-combat DTO branches and therefore
/// stays in the Runtime while the replication surface lives in the Application
/// layer.
/// </summary>
internal static class TestKernelCodec
{
	internal static readonly IKernelWireCodec Instance = new KernelWireCodec();
}
