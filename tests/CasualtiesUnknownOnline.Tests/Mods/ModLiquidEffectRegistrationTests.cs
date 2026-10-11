using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod-authored liquid effect registration rails. An effect is a FUNCTION keyed
/// by a content id, so the rails are what keeps a function bound to the data it
/// belongs to: the permission, the ownership of the id, and the one-function-per-half
/// rule. These cases drive the per-mod adapter directly over one store and one
/// content store, because the claims under test are the refusals and the scoping —
/// the discovery path that builds one adapter per mod is the lifecycle tests' own.
/// </summary>
public class ModLiquidEffectRegistrationTests
{
	private const string LiquidId = "solvent";
	private const string OtherModId = "mod.b";

	[Fact]
	public void Register_WithoutThePermission_IsRefusedAndLogged()
	{
		var (effects, entries) = AdapterFor("mod.a", ModPermission.RegisterContent, registerLiquid: true);

		Assert.False(effects.TryRegisterDrink(LiquidId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("AuthorEffects", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForALiquidThisModNeverRegistered_IsRefused()
	{
		var (effects, entries) = AdapterFor("mod.a", AuthorEffects, registerLiquid: false);

		Assert.False(effects.TryRegisterDrink(LiquidId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("liquid content", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForALiquidAnotherModRegistered_IsRefused()
	{
		var contentStore = new ModContentStore();
		contentStore.Add(OtherModId, null, new ModLiquidDefinition { Id = LiquidId });
		var (effects, entries) = AdapterFor("mod.a", AuthorEffects, registerLiquid: false, contentStore: contentStore);

		Assert.False(effects.TryRegisterDrink(LiquidId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("liquid content", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForANonLiquidDefinitionWithTheSameId_IsRefused()
	{
		// The kind is part of the rail: a liquid TILE carrying the id is not the liquid,
		// and a check that read only the id would accept it and bind the function to
		// content of a different kind.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModLiquidTileDefinition { Id = LiquidId });
		var (effects, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: false, contentStore: contentStore);

		Assert.False(effects.TryRegisterDrink(LiquidId, _ => { }));
	}

	[Fact]
	public void Register_ForAValidLiquid_IsAcceptedPerHalfAndRefusesTheDuplicate()
	{
		var store = new ModLiquidEffectStore();
		var (effects, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: true, effectStore: store);

		Assert.True(effects.TryRegisterDrink(LiquidId, _ => { }));
		Assert.True(effects.TryRegisterLimbUse(LiquidId, _ => { }));
		Assert.Equal(2, store.Count);

		// A second function for the SAME half is refused rather than replaced, so two
		// call sites can never disagree about which one runs.
		Assert.False(effects.TryRegisterDrink(LiquidId, _ => { }));
		Assert.False(effects.TryRegisterLimbUse(LiquidId, _ => { }));
		Assert.Equal(2, store.Count);
	}

	[Fact]
	public void Register_WithANullEffectOrAnInvalidId_IsRefused()
	{
		var (effects, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: true);

		Assert.False(effects.TryRegisterDrink(LiquidId, null!));
		Assert.False(effects.TryRegisterDrink("Not A Valid Id", _ => { }));
		Assert.False(effects.TryRegisterDrink(string.Empty, _ => { }));
	}

	[Fact]
	public void Store_ResolvesTheHalfThatWasRegistered_AndNothingElse()
	{
		var store = new ModLiquidEffectStore();
		var (effects, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: true, effectStore: store);
		Assert.True(effects.TryRegisterDrink(LiquidId, _ => { }));

		Assert.NotNull(store.FindDrink(LiquidId));
		Assert.Null(store.FindLimbUse(LiquidId));
		Assert.Null(store.FindDrink("never_registered"));

		// The load-time diagnostic asks about the LIMB-USE half specifically, and this is
		// why: the two flags that gate the native limb paths gate onHealthUse and nothing
		// else, so a definition declaring one while its mod registered only the drink half
		// must still be reported. "Is this liquid registered at all" would answer true.
		Assert.False(store.HasLimbUse(LiquidId));
		Assert.True(effects.TryRegisterLimbUse(LiquidId, _ => { }));
		Assert.True(store.HasLimbUse(LiquidId));
		Assert.False(store.HasLimbUse("never_registered"));
		Assert.Equal("mod.a", store.FindDrink(LiquidId)!.ModId);
	}

	[Fact]
	public void RemoveMod_TakesBackBothHalvesOfThatModAndLeavesTheOtherModsAlone()
	{
		// A mod registers before it can fail to load, so a failed load has to withdraw
		// its slice: the Game Adapter's resolver reads this store, and an entry left
		// behind would run the code of a mod the process never loaded.
		var store = new ModLiquidEffectStore();
		var (first, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: true, effectStore: store);
		Assert.True(first.TryRegisterDrink(LiquidId, _ => { }));
		Assert.True(first.TryRegisterLimbUse(LiquidId, _ => { }));

		var (second, _) = AdapterFor(OtherModId, AuthorEffects, registerLiquid: true, effectStore: store, liquidId: "b_solvent");
		Assert.True(second.TryRegisterDrink("b_solvent", _ => { }));

		Assert.Equal(2, store.RemoveMod("mod.a"));
		Assert.Null(store.FindDrink(LiquidId));
		Assert.Null(store.FindLimbUse(LiquidId));
		Assert.NotNull(store.FindDrink("b_solvent"));
		Assert.Equal(0, store.RemoveMod("mod.a"));
	}

	[Fact]
	public void Register_ReportsTheHalfItFiled()
	{
		var store = new ModLiquidEffectStore();
		var (effects, _) = AdapterFor("mod.a", AuthorEffects, registerLiquid: true, effectStore: store);
		Assert.True(effects.TryRegisterLimbUse(LiquidId, _ => { }));

		Assert.True(store.Registrations[0].IsLimbUse);
		Assert.Equal(LiquidId, store.Registrations[0].LiquidId);
	}

	private const ModPermission AuthorEffects = ModPermission.AuthorEffects;

	private static (ModLiquidEffectsAdapter Effects, List<(LogLevel Level, string Message)> Entries) AdapterFor(
		string modId,
		ModPermission permissions,
		bool registerLiquid,
		ModContentStore? contentStore = null,
		ModLiquidEffectStore? effectStore = null,
		string liquidId = LiquidId)
	{
		contentStore ??= new ModContentStore();
		if (registerLiquid)
		{
			contentStore.Add(modId, null, new ModLiquidDefinition { Id = liquidId });
		}

		var log = new RecordingLogger<ModLiquidEffectsAdapter>();
		var manifest = new ModManifest(modId, modId, "1.0.0", NetworkMode.Synchronized, null, permissions);
		return (new ModLiquidEffectsAdapter(manifest, contentStore, effectStore ?? new ModLiquidEffectStore(), log), log.Entries);
	}
}
