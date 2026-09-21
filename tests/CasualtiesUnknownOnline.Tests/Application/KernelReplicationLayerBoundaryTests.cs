using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// Pins the kernel-replication slice's layer boundary: the replication surface
/// lives in the Application assembly and the Application assembly never reaches
/// back into the Runtime. The types that stayed behind are named here as
/// DECISIONS with their blocker, so moving one of them later is a deliberate
/// change to this list rather than a silent drift.
/// </summary>
public class KernelReplicationLayerBoundaryTests
{
	private const string ApplicationAssembly = "CasualtiesUnknownOnline.Application";

	private const string RuntimeAssembly = "CasualtiesUnknownOnline.Runtime";

	[Theory]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.RefusedItemCreations")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelDomainWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.WireCheckpointAssembler")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelProtocolService")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelProtocolCommandHandler")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelStateStreamService")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.GuestCheckpointReceiver")]
	public void KernelReplicationType_LivesInTheApplicationAssembly(string fullName)
	{
		var assembly = typeof(IKernelProtocolControl).Assembly;

		Assert.Equal(ApplicationAssembly, assembly.GetName().Name);
		Assert.NotNull(assembly.GetType(fullName));
	}

	[Fact]
	public void ApplicationAssembly_NeverReferencesTheRuntimeOrTheLayersAboveIt()
	{
		var referenced = typeof(IKernelProtocolControl).Assembly
			.GetReferencedAssemblies()
			.Select(name => name.Name)
			.ToList();

		Assert.DoesNotContain(RuntimeAssembly, referenced);
		Assert.DoesNotContain("CasualtiesUnknownOnline.GameAdapter", referenced);
		Assert.DoesNotContain("CasualtiesUnknownOnline.Plugin", referenced);
		Assert.DoesNotContain("Assembly-CSharp", referenced);
	}

	[Fact]
	public void RuntimeReachesTheMovedSurfaceThroughTheApplicationAssembly()
	{
		var referenced = typeof(KernelWireMapper).Assembly
			.GetReferencedAssemblies()
			.Select(name => name.Name)
			.ToList();

		Assert.Contains(ApplicationAssembly, referenced);
	}

	[Fact]
	public void LegacyCoupledTypes_StayInTheRuntimeForTheirOwnSlice()
	{
		// KernelWireMapper maps to the legacy protobuf message set (the enemy-combat
		// messages the Game Adapter and the character-snapshot store speak), so it
		// cannot move before those DTOs do; the Application side reaches it through
		// IKernelWireCodec.
		Assert.Equal(RuntimeAssembly, typeof(KernelWireMapper).Assembly.GetName().Name);

		// KernelBatchItemProjection carries the same legacy item DTOs (WorldItem,
		// WorldItemTable, CharacterItemMsg, NetVector2) in its own contract; moving
		// it is a projection redesign, not a move.
		Assert.Equal(RuntimeAssembly, typeof(KernelBatchItemProjection).Assembly.GetName().Name);

		// KernelEnvelopeHandler is the transport side of the seam (frame decode,
		// traffic accounting, handler dispatch) and inherits the Runtime's packet
		// handler base; the Application side owns the protocol surface it forwards to.
		Assert.Equal(RuntimeAssembly, typeof(KernelEnvelopeHandler).Assembly.GetName().Name);
	}
}
