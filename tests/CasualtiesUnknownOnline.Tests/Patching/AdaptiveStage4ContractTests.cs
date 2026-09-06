using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Stage 4 integration contract: the Game Adapter's stream-owning domain
/// classes must resolve the shared adaptive rate service so item, fluid and
/// trader stream senders actually consult the global governor. This is a
/// reflective contract because the test project never compile-references the
/// Game Adapter (it binds Unity/game assemblies).
/// </summary>
public class AdaptiveStage4ContractTests
{
	[Fact]
	public void ItemPositionAuthority_ConsumesAdaptiveRates()
	{
		var type = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.Items.ItemPositionAuthority");
		AssertField(type, "_adaptiveRates", typeof(AdaptiveStreamRateService));
		AssertMethod(type, "Update", BindingFlags.Instance | BindingFlags.NonPublic);
		AssertMethod(type, "ResetSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	[Fact]
	public void FluidSimulationAuthority_ConsumesAdaptiveRates()
	{
		var type = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.World.FluidSimulationAuthority");
		AssertField(type, "_adaptiveRates", typeof(AdaptiveStreamRateService));
		AssertMethod(type, "Update", BindingFlags.Instance | BindingFlags.NonPublic);
		AssertMethod(type, "ResetSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	[Fact]
	public void TradeStateSync_ConsumesAdaptiveRates()
	{
		var type = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.World.TradeStateSync");
		AssertField(type, "_adaptiveRates", typeof(AdaptiveStreamRateService));
		AssertMethod(type, "Update", BindingFlags.Instance | BindingFlags.NonPublic);
		AssertMethod(type, "ResetSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	[Fact]
	public void GameAdapter_ConstructorAcceptsAdaptiveRates()
	{
		var type = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.GameAdapter");
		var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
		var constructor = Assert.Single(constructors);
		Assert.Contains(constructor.GetParameters(), p => p.ParameterType == typeof(AdaptiveStreamRateService));
	}

	[Fact]
	public void FluidWorldSync_ResetsKernelAndAuthority()
	{
		var type = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.World.FluidWorldSync");
		AssertMethod(type, "ResetSessionState", BindingFlags.Instance | BindingFlags.NonPublic);

		var kernelType = GetAdapterType("CasualtiesUnknownOnline.GameAdapter.World.FluidRegionKernelSync");
		AssertMethod(kernelType, "ResetSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	private static Type GetAdapterType(string fullName) =>
		GameAssemblyHost.Adapter.GetType(fullName, throwOnError: false)
		?? throw new InvalidOperationException($"{fullName} not found in the adapter assembly.");

	private static void AssertField(Type type, string fieldName, Type expectedType)
	{
		var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(field);
		Assert.Equal(expectedType, field!.FieldType);
	}

	private static void AssertMethod(Type type, string methodName, BindingFlags flags)
	{
		var method = type.GetMethod(methodName, flags);
		Assert.NotNull(method);
	}
}
