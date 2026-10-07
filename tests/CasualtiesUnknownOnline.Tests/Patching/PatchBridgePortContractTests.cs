using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The compiled half of the patch-bridge port split
/// (<c>done/patch-bridge-domain-ports.md</c>). The source gate
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

	private static readonly Type LayerPort = Adapter("CasualtiesUnknownOnline.GameAdapter.ILayerAdvancePatchPort");

	private static readonly Type ItemCategoryPort = Adapter("CasualtiesUnknownOnline.GameAdapter.IItemCategoryPatchPort");

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

	/// <summary>The layer-transition port's whole surface, as the port declares it.</summary>
	private static readonly string[] LayerAdvanceMembers =
	[
		"TryDelegateLocalAdvance",
	];

	[Fact]
	public void LayerAdvancePort_DeclaresExactlyItsMembers() =>
		Assert.Equal(Census(LayerAdvanceMembers), Census(DeclaredMembers(LayerPort)));

	/// <summary>The item category port's whole surface: the data anchor (is this id a world row) and the refusal report for a local gesture on a standing item object.</summary>
	private static readonly string[] ItemCategoryMembers =
	[
		"IsWorldItemRegistered",
		"ReportStandingItemGestureRefused",
	];

	[Fact]
	public void ItemCategoryPort_DeclaresExactlyItsMembers() =>
		Assert.Equal(Census(ItemCategoryMembers), Census(DeclaredMembers(ItemCategoryPort)));

	[Fact]
	public void Aggregate_DoesNotDeclareAnyItemCategoryMember() =>
		Assert.Empty(ItemCategoryMembers.Intersect(DeclaredMembers(Bridge), StringComparer.Ordinal));

	[Fact]
	public void Bridge_ImplementsTheItemCategoryPortAndDeclaresEveryMember()
	{
		Assert.True(ItemCategoryPort.IsAssignableFrom(BridgeImpl), "GameAdapterBridge does not implement IItemCategoryPatchPort");

		var missing = ItemCategoryMembers
			.Where(member => BridgeImpl.GetMethod(member, Any) is null)
			.ToArray();

		Assert.Empty(missing);
	}

	[Fact]
	public void Aggregate_NoLongerDeclaresAnyFluidMember() =>
		Assert.Empty(FluidMembers.Intersect(DeclaredMembers(Bridge), StringComparer.Ordinal));

	/// <summary>The layer-transition domain never lived in the aggregate: its only door is the port.</summary>
	[Fact]
	public void Aggregate_DoesNotDeclareAnyLayerAdvanceMember() =>
		Assert.Empty(LayerAdvanceMembers.Intersect(DeclaredMembers(Bridge), StringComparer.Ordinal));

	[Fact]
	public void Bridge_ImplementsTheFluidPortAndDeclaresEveryMember()
	{
		Assert.True(Port.IsAssignableFrom(BridgeImpl), "GameAdapterBridge does not implement IFluidPatchPort");

		var missing = FluidMembers
			.Where(member => BridgeImpl.GetMethod(member, Any) is null)
			.ToArray();

		Assert.Empty(missing);
	}

	[Fact]
	public void Bridge_ImplementsTheLayerAdvancePortAndDeclaresEveryMember()
	{
		Assert.True(LayerPort.IsAssignableFrom(BridgeImpl), "GameAdapterBridge does not implement ILayerAdvancePatchPort");

		var missing = LayerAdvanceMembers
			.Where(member => BridgeImpl.GetMethod(member, Any) is null)
			.ToArray();

		Assert.Empty(missing);
	}

	/// <summary>The seam's doors: the aggregate for the unmigrated domains, one per port-shaped seam.</summary>
	[Fact]
	public void Seam_ExposesTheAggregateAndThePorts()
	{
		Assert.Equal(Bridge, SeamType("Impl"));
		Assert.Equal(Port, SeamType("Fluid"));
		Assert.Equal(LayerPort, SeamType("LayerAdvance"));
		Assert.Equal(ItemCategoryPort, SeamType("ItemCategory"));
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
