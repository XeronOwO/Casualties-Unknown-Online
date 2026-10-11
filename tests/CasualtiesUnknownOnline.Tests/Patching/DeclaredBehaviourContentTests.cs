using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// A declared behaviour with no function. A mod can declare, through the content
/// API, that its item is usable or usable on a limb and that its liquid can be
/// applied to a limb or injected — but a function is not data, so what the content
/// DTO carries is the flag alone, and the game's own call sites do not null-check:
/// <c>Body.UseItem</c> and <c>Body.UseItemInHand</c> run <c>ItemInfo.useAction</c>
/// behind nothing but the item's own <c>usable</c> flag, the wound view runs
/// <c>ItemInfo.useLimbAction</c> behind <c>usableOnLimb</c>, and
/// <c>WaterContainerItem.Drink</c> / <c>ApplyToLimb</c> / <c>Inject</c> run the
/// liquid's <c>onDrink</c> / <c>onHealthUse</c> behind only the liquid's own flags.
/// So what the two content providers BUILD is the contract this suite pins: whatever
/// they hand the game carries the delegate, the delegate resolves the function a mod
/// registered in code through <see cref="IModLiquidEffects"/> /
/// <see cref="IModItemActions"/> and — where none is registered — reports instead of
/// throwing, and an item that declares no usability keeps no invented action. The
/// test project never compile-references GameAdapter, so the two build sites are
/// driven reflectively.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class DeclaredBehaviourContentTests
{
	private const string ItemProviderTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";
	private const string ItemInfoFactoryTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.ModItemInfoFactory";
	private const string ItemActionInvokerTypeName = "CasualtiesUnknownOnline.GameAdapter.ModEffects.ItemActionInvoker";
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

	[Fact]
	public void Item_DeclaringLimbUsability_CarriesTheFlagTheGameGateReads()
	{
		var logger = CreateLogger(ItemProviderTypeName, out _);

		Assert.True(ReadBool(BuildItem("custom_splint", new ModItemDefinition { UsableOnLimb = true }, logger), "usableOnLimb"));
		Assert.False(ReadBool(BuildItem("custom_relic", new ModItemDefinition(), logger), "usableOnLimb"));
	}

	[Fact]
	public void Item_DeclaringLimbUsabilityWithNoBehaviour_IsBuiltWithALimbActionThatReportsInsteadOfThrowing()
	{
		// The limb half of the item contract, and the reason it needs its own case: the
		// wound view calls the delegate behind `usableOnLimb` with no null check, so a
		// declared limb usability with no registered action must be a reporting no-op
		// rather than a null.
		var logger = CreateLogger(ItemProviderTypeName, out var entries);

		var info = BuildItem("custom_poultice", new ModItemDefinition { UsableOnLimb = true }, logger);

		var useLimbAction = ReadDelegate(info, "useLimbAction");
		Assert.NotNull(useLimbAction);

		useLimbAction!.DynamicInvoke(null, null);

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_poultice", StringComparison.Ordinal)
			&& entry.Message.Contains(NoEffectFragment, StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringNoLimbUsability_KeepsNoInventedLimbAction()
	{
		var logger = CreateLogger(ItemProviderTypeName, out _);

		var info = BuildItem("custom_relic", new ModItemDefinition { Usable = true }, logger);

		Assert.Null(ReadDelegate(info, "useLimbAction"));
	}

	[Fact]
	public void Item_WithARegisteredUseAction_ResolvesItPerCallOnTheUseHalf()
	{
		var logger = CreateLogger(ItemProviderTypeName, out var entries);
		var store = new ModItemEffectStore();
		RegisterAction(store, "custom_widget", _ => { }, limbUse: false);

		var info = BuildItem("custom_widget", new ModItemDefinition { Usable = true }, logger, store);

		var useAction = ReadDelegate(info, "useAction");
		Assert.NotNull(useAction);

		// The test host cannot build a live Body or Item, so the invocation takes the
		// invoker's own missing-object branch. What this case discriminates is WHICH
		// branch: the registered one names the owning mod, where the unregistered path
		// names no mod at all — so this is the lookup, per call, that the liquid half
		// had no case for either.
		useAction!.DynamicInvoke(null, null);

		Assert.Contains(entries, entry => entry.Message.Contains("mod.a", StringComparison.Ordinal)
			&& entry.Message.Contains("custom_widget", StringComparison.Ordinal));
		Assert.DoesNotContain(entries, entry => entry.Message.Contains("no mod registered", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_AnActionRegisteredAfterTheBuild_StillResolvesAtCallTime()
	{
		// The case above registers BEFORE the build, which a build-time capture would
		// also pass. This one builds first and registers second, and that is the only
		// shape that tells a per-call lookup from a captured delegate: the vanilla item
		// table is injected when it first exists, which is unrelated to when a mod
		// registers, so the registration order must not be load-bearing.
		var logger = CreateLogger(ItemProviderTypeName, out var entries);
		var store = new ModItemEffectStore();

		var info = BuildItem("custom_widget", new ModItemDefinition { Usable = true }, logger, store);
		var useAction = ReadDelegate(info, "useAction");
		Assert.NotNull(useAction);

		// Nothing is registered yet: the same delegate reports the unregistered path.
		useAction!.DynamicInvoke(null, null);
		Assert.Contains(entries, entry => entry.Message.Contains("no mod registered", StringComparison.Ordinal));

		entries.Clear();
		RegisterAction(store, "custom_widget", _ => { }, limbUse: false);

		// The delegate was built before that registration and still finds it.
		useAction.DynamicInvoke(null, null);
		Assert.DoesNotContain(entries, entry => entry.Message.Contains("no mod registered", StringComparison.Ordinal));
		Assert.Contains(entries, entry => entry.Message.Contains("mod.a", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_WithARegisteredLimbAction_ResolvesItPerCallOnTheLimbHalf()
	{
		var logger = CreateLogger(ItemProviderTypeName, out var entries);
		var store = new ModItemEffectStore();
		RegisterAction(store, "custom_splint", _ => { }, limbUse: true);

		var info = BuildItem("custom_splint", new ModItemDefinition { UsableOnLimb = true }, logger, store);

		var useLimbAction = ReadDelegate(info, "useLimbAction");
		Assert.NotNull(useLimbAction);

		useLimbAction!.DynamicInvoke(null, null);

		Assert.Contains(entries, entry => entry.Message.Contains("mod.a", StringComparison.Ordinal)
			&& entry.Message.Contains("limb-use", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_WithOnlyTheUseHalfRegistered_LeavesTheLimbHalfOnTheReportingPath()
	{
		// The two halves are separate registrations and separate lookups: registering one
		// must not answer for the other, which is the shape the "one flag, one function"
		// report in the provider depends on.
		var logger = CreateLogger(ItemProviderTypeName, out var entries);
		var store = new ModItemEffectStore();
		RegisterAction(store, "custom_widget", _ => { }, limbUse: false);

		var info = BuildItem("custom_widget", new ModItemDefinition { Usable = true, UsableOnLimb = true }, logger, store);

		Assert.NotNull(ReadDelegate(info, "useAction"));

		var useLimbAction = ReadDelegate(info, "useLimbAction");
		Assert.NotNull(useLimbAction);
		useLimbAction!.DynamicInvoke(null, null);

		Assert.Contains(entries, entry => entry.Message.Contains("no mod registered", StringComparison.Ordinal));
	}

	private static object CreateProvider(string typeName, out List<(LogLevel Level, string Message)> entries)
	{
		var providerType = GameAssemblyHost.Adapter.GetType(typeName, throwOnError: true)!;
		var logger = CreateLogger(typeName, out entries);

		// Both providers take a framework seam beside their logger (the delegates they
		// install resolve a mod's registered effect or action through it); the shared
		// factory supplies them by parameter type, so neither build site needs a branch
		// here.
		return GameAdapterProviderFactory.Create(providerType, logger);
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

	private static object BuildItem(string id, ModItemDefinition definition, object logger) =>
		BuildItem(id, definition, logger, new ModItemEffectStore());

	private static object BuildItem(string id, ModItemDefinition definition, object logger, ModItemEffectStore store)
	{
		var factoryType = GameAssemblyHost.Adapter.GetType(ItemInfoFactoryTypeName, throwOnError: true)!;
		return Invoke(null, factoryType, "Build", [id, definition, CreateItemActionInvoker(logger, store)]);
	}

	/// <summary>
	/// The invoker the item build site installs its two delegates from, over the item
	/// action registry each case supplies — empty for the cases about which delegate a
	/// declaration gets, populated for the ones about resolving a registered function —
	/// and the session identity seam.
	/// </summary>
	private static object CreateItemActionInvoker(object logger, ModItemEffectStore? store = null)
	{
		var invokerType = GameAssemblyHost.Adapter.GetType(ItemActionInvokerTypeName, throwOnError: true)!;
		return Activator.CreateInstance(invokerType, [store ?? new ModItemEffectStore(), new FakeSessionControl(), logger])!;
	}

	/// <summary>
	/// Registers one action the way a mod does — through the per-mod adapter, over the
	/// content definition that owns the id — so the case that follows proves the whole
	/// resolution path rather than a hand-filed store entry.
	/// </summary>
	private static void RegisterAction(
		ModItemEffectStore store, string itemId, ModItemAction action, bool limbUse, string modId = "mod.a")
	{
		var contentStore = new ModContentStore();
		contentStore.Add(modId, null, new ModItemDefinition { Id = itemId, Usable = true, UsableOnLimb = true });
		var manifest = new ModManifest(modId, modId, "1.0.0", NetworkMode.Synchronized, null, ModPermission.AuthorEffects);
		var actions = new ModItemActionsAdapter(manifest, contentStore, store, new RecordingLogger<ModItemActionsAdapter>());

		Assert.True(limbUse ? actions.TryRegisterLimbUse(itemId, action) : actions.TryRegisterUse(itemId, action));
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

	private static bool ReadBool(object target, string fieldName) =>
		(bool)target.GetType()
			.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetValue(target)!;
}
