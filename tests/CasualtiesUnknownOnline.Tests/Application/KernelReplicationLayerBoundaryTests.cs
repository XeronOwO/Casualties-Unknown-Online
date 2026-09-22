using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// Pins the kernel-replication slice's layer boundary: the replication surface
/// AND the pure kernel &lt;-&gt; wire vocabulary live in the Application assembly,
/// and the Application assembly never reaches back into the Runtime. The types
/// that stayed behind are named here as DECISIONS with their blocker, so moving
/// one of them later is a deliberate change to this list rather than a silent
/// drift.
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
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelPlayerInteractionWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelEnemyCombatWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelLimbWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.KernelComponentWireMapper")]
	[InlineData("CasualtiesUnknownOnline.Application.Kernel.ItemSpawnWireMapper")]
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
		// The Runtime is still the side that speaks the legacy message set, so the
		// direction has to hold from there: a Runtime type that reaches the moved
		// mapper does it through the layer reference, never by reaching past it.
		var referenced = typeof(KernelEnvelopeHandler).Assembly
			.GetReferencedAssemblies()
			.Select(name => name.Name)
			.ToList();

		Assert.Contains(ApplicationAssembly, referenced);
	}

	[Fact]
	public void LegacyMaterializationTypes_StayInTheRuntimeWithTheirReason()
	{
		// KernelBatchItemProjection is a MATERIALIZATION projection, not a mapper:
		// its constructor and every output path build the legacy item vocabulary
		// (WorldItem, WorldItemTable, CharacterItemMsg, NetVector2) and write the
		// rebuildable world-item table, so it cannot move without redesigning its
		// output contract into kernel facts plus a Runtime materializer. Recorded in
		// review/legacy-wire-dto-slice.md with the contract evidence.
		Assert.Equal(RuntimeAssembly, typeof(KernelBatchItemProjection).Assembly.GetName().Name);

		// KernelEnvelopeHandler is the transport side of the seam (frame decode,
		// traffic accounting, handler dispatch) and inherits the Runtime's packet
		// handler base; the Application side owns the protocol surface it forwards to.
		Assert.Equal(RuntimeAssembly, typeof(KernelEnvelopeHandler).Assembly.GetName().Name);
	}
}
