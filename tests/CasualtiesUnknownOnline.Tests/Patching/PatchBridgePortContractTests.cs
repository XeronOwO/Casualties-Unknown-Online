using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The compiled half of the patch-bridge port split
/// (<c>review/patch-bridge-domain-ports.md</c>). The source gate
/// (<c>PatchBridgePortShapeGateTests</c>, fast suite) reads text; this reads the
/// adapter the game actually loads, so a build whose aggregate still answers the
/// fluid members, or a port nothing implements, fails here rather than in a
/// review of the diff.
/// </summary>
[Trait("Category", "Integration")]
public class PatchBridgePortContractTests
{
	private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

	private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	private static readonly Type Bridge = Adapter("CasualtiesUnknownOnline.GameAdapter.IPatchBridge");

	private static readonly Type Port = Adapter("CasualtiesUnknownOnline.GameAdapter.IFluidPatchPort");

	private static readonly Type BridgeImpl = Adapter("CasualtiesUnknownOnline.GameAdapter.GameAdapterBridge");

	private static readonly Type Seam = Adapter("CasualtiesUnknownOnline.GameAdapter.PatchBridge");

	/// <summary>The fluid domain's patch surface, as the port declares it.</summary>
	private static readonly string[] FluidMembers =
	[
		"ApplyLiquidTileBodyTouch",
		"OnFluidDrinkReported",
		"OnFluidFixedUpdate",
		"TryDrinkCustomLiquid",
		"TryGetCustomLiquidColor",
		"TryGetCustomLiquidName",
		"TryGetCustomWaterInfo",
		"TryRenderCustomLiquids",
	];

	[Fact]
	public void FluidPort_DeclaresExactlyTheFluidMembers() =>
		Assert.Equal(Census(FluidMembers), Census(DeclaredMembers(Port)));

	[Fact]
	public void Aggregate_NoLongerDeclaresAnyFluidMember() =>
		Assert.Empty(FluidMembers.Intersect(DeclaredMembers(Bridge), StringComparer.Ordinal));

	[Fact]
	public void Bridge_ImplementsTheFluidPortAndDeclaresEveryMember()
	{
		Assert.True(Port.IsAssignableFrom(BridgeImpl), "GameAdapterBridge does not implement IFluidPatchPort");

		var missing = FluidMembers
			.Where(member => BridgeImpl.GetMethod(member, Any) is null)
			.ToArray();

		Assert.Empty(missing);
	}

	/// <summary>The seam's two doors: the aggregate for the unmigrated domains, the port for the fluid domain.</summary>
	[Fact]
	public void Seam_ExposesTheAggregateAndThePort()
	{
		Assert.Equal(Bridge, SeamType("Impl"));
		Assert.Equal(Port, SeamType("Fluid"));
	}

	private static Type SeamType(string property) =>
		Seam.GetProperty(property, Static)?.PropertyType
			?? throw new InvalidOperationException($"PatchBridge.{property} not found.");

	/// <summary>A member is its method, property or event name — an accessor is not a member a patch writes.</summary>
	private static string[] DeclaredMembers(Type type) =>
	[
		.. type.GetMethods(Instance).Where(method => !method.IsSpecialName).Select(method => method.Name),
		.. type.GetProperties(Instance).Select(property => property.Name),
		.. type.GetEvents(Instance).Select(@event => @event.Name),
	];

	private static string Census(IEnumerable<string> members) =>
		string.Join(",", members.OrderBy(name => name, StringComparer.Ordinal));

	private static Type Adapter(string name) => GameAssemblyHost.Adapter.GetType(name, throwOnError: true)!;
}
