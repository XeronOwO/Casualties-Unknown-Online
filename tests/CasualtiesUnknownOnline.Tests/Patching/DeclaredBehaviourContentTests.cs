using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// A declared behaviour with no function. A mod can declare, through the content
/// API, that its item is usable and that its liquid can be applied to a limb or
/// injected — but the API cannot carry the function those flags gate, and the
/// game's own call sites do not null-check: <c>Body.UseItem</c> and
/// <c>Body.UseItemInHand</c> run <c>ItemInfo.useAction</c> behind nothing but the
/// item's own <c>usable</c> flag, and <c>WaterContainerItem.Drink</c> /
/// <c>ApplyToLimb</c> / <c>Inject</c> run the liquid's <c>onDrink</c> /
/// <c>onHealthUse</c> behind only the liquid's own flags. So what the two content
/// providers BUILD is the contract this suite pins: whatever they hand the game
/// carries the delegate, the delegate reports instead of throwing, and an item
/// that declares no usability keeps no invented action. The test project never
/// compile-references GameAdapter, so the two build sites are driven reflectively.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class DeclaredBehaviourContentTests
{
	private const string ItemProviderTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";
	private const string ItemInfoFactoryTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.ModItemInfoFactory";
	private const string LiquidProviderTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidContentProvider";

	/// <summary>The clause every installed no-effect delegate must carry.</summary>
	private const string NoEffectFragment = "no effect";

	[Fact]
	public void Liquid_BuiltWithNoAuthoredEffect_CarriesBothDelegatesTheGameCallsWithoutANullCheck()
	{
		var provider = CreateProvider(LiquidProviderTypeName, out _);

		var liquid = BuildLiquid(provider, "custom_solvent", new ModLiquidDefinition { HealthUsable = true, Injectable = true });

		Assert.NotNull(ReadDelegate(liquid, "onDrink"));
		Assert.NotNull(ReadDelegate(liquid, "onHealthUse"));
	}

	[Fact]
	public void Liquid_DrinkingWithNoAuthoredEffect_ReportsTheLiquidAndDoesNotThrow()
	{
		var provider = CreateProvider(LiquidProviderTypeName, out var entries);
		var liquid = BuildLiquid(provider, "custom_broth", new ModLiquidDefinition());

		var onDrink = ReadDelegate(liquid, "onDrink");
		Assert.NotNull(onDrink);

		onDrink!.DynamicInvoke(200f, null);

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_broth", StringComparison.Ordinal)
			&& entry.Message.Contains(NoEffectFragment, StringComparison.Ordinal));
	}

	[Fact]
	public void Liquid_AppliedToALimbWithNoAuthoredEffect_ReportsTheLiquidAndDoesNotThrow()
	{
		var provider = CreateProvider(LiquidProviderTypeName, out var entries);
		var liquid = BuildLiquid(provider, "custom_ointment", new ModLiquidDefinition { HealthUsable = true });

		var onHealthUse = ReadDelegate(liquid, "onHealthUse");
		Assert.NotNull(onHealthUse);

		onHealthUse!.DynamicInvoke(10f, null);

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_ointment", StringComparison.Ordinal)
			&& entry.Message.Contains(NoEffectFragment, StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringUsabilityWithNoBehaviour_IsBuiltWithAUseActionThatReportsInsteadOfThrowing()
	{
		var logger = CreateLogger(ItemProviderTypeName, out var entries);

		var info = BuildItem("custom_widget", new ModItemDefinition { Usable = true, UsableWithLmb = true }, logger);

		var useAction = ReadDelegate(info, "useAction");
		Assert.NotNull(useAction);

		useAction!.DynamicInvoke(null, null);

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_widget", StringComparison.Ordinal)
			&& entry.Message.Contains(NoEffectFragment, StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringNoUsability_KeepsNoInventedUseAction()
	{
		var logger = CreateLogger(ItemProviderTypeName, out _);

		var info = BuildItem("custom_relic", new ModItemDefinition(), logger);

		Assert.Null(ReadDelegate(info, "useAction"));
	}

	[Fact]
	public void Item_DeclaringItsOwnToolBehaviour_KeepsThatActionRatherThanTheNoEffectOne()
	{
		var logger = CreateLogger(ItemProviderTypeName, out var entries);

		var info = BuildItem("custom_hammer", new ModItemDefinition { Tool = new ModItemTool() }, logger);

		var useAction = ReadDelegate(info, "useAction");
		Assert.NotNull(useAction);

		// The tool action returns on a null body/item (the test host cannot build
		// live Unity objects), so what this case discriminates is WHICH delegate the
		// provider installed: the tool's own, not the no-effect report.
		useAction!.DynamicInvoke(null, null);

		Assert.DoesNotContain(entries, entry => entry.Message.Contains(NoEffectFragment, StringComparison.Ordinal));
	}

	private static object CreateProvider(string typeName, out List<(LogLevel Level, string Message)> entries)
	{
		var providerType = GameAssemblyHost.Adapter.GetType(typeName, throwOnError: true)!;
		var logger = CreateLogger(typeName, out entries);
		return Activator.CreateInstance(providerType, [logger])!;
	}

	private static object CreateLogger(string categoryTypeName, out List<(LogLevel Level, string Message)> entries)
	{
		var category = GameAssemblyHost.Adapter.GetType(categoryTypeName, throwOnError: true)!;
		var loggerType = typeof(RecordingLogger<>).MakeGenericType(category);
		var logger = Activator.CreateInstance(loggerType)!;
		entries = (List<(LogLevel Level, string Message)>)loggerType
			.GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetValue(logger)!;
		return logger;
	}

	private static object BuildLiquid(object provider, string id, ModLiquidDefinition definition) =>
		Invoke(provider, provider.GetType(), "BuildLiquid", [id, definition]);

	private static object BuildItem(string id, ModItemDefinition definition, object logger)
	{
		var factoryType = GameAssemblyHost.Adapter.GetType(ItemInfoFactoryTypeName, throwOnError: true)!;
		return Invoke(null, factoryType, "Build", [id, definition, logger]);
	}

	private static object Invoke(object? target, Type type, string name, object?[] arguments)
	{
		var method = type.GetMethod(
			name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"{type.FullName}.{name} not found.");
		return method.Invoke(target, arguments)!;
	}

	private static Delegate? ReadDelegate(object target, string fieldName) =>
		(Delegate?)target.GetType()
			.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
			.GetValue(target);
}
