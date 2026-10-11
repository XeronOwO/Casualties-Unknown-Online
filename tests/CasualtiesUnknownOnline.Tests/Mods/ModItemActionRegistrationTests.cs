using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod-authored item action registration rails. An action is a FUNCTION keyed by
/// a content id, so the rails are what keeps a function bound to the data it belongs
/// to: the permission, the ownership of the id, the one-function-per-half rule — and
/// the item side's own reachability rail, which has no counterpart on the liquid side
/// because both native item calls sit behind a flag on the definition. These cases
/// drive the per-mod adapter directly over one store and one content store, because
/// the claims under test are the refusals and the scoping — the discovery path that
/// builds one adapter per mod is the lifecycle tests' own.
/// </summary>
public class ModItemActionRegistrationTests
{
	private const string ItemId = "widget";
	private const string OtherModId = "mod.b";

	[Fact]
	public void Register_WithoutThePermission_IsRefusedAndLogged()
	{
		var (actions, entries) = AdapterFor("mod.a", ModPermission.RegisterContent, registerItem: true);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("AuthorEffects", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForAnItemThisModNeverRegistered_IsRefused()
	{
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("item content", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForAnItemAnotherModRegistered_IsRefused()
	{
		var contentStore = new ModContentStore();
		contentStore.Add(OtherModId, null, UsableItem(ItemId));
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("item content", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForANonItemDefinitionWithTheSameId_IsRefused()
	{
		// The kind is part of the rail: a liquid carrying the id is not the item, and a
		// check that read only the id would accept it and bind the function to content
		// of a different kind.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModLiquidDefinition { Id = ItemId });
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("item content", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_ForAValidItem_IsAcceptedPerHalfAndRefusesTheDuplicate()
	{
		var store = new ModItemEffectStore();
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: true, effectStore: store);

		Assert.True(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.True(actions.TryRegisterLimbUse(ItemId, _ => { }));
		Assert.Equal(2, store.Count);

		// A second function for the SAME half is refused rather than replaced, so two
		// call sites can never disagree about which one runs.
		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.False(actions.TryRegisterLimbUse(ItemId, _ => { }));
		Assert.Equal(2, store.Count);
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("the second is refused", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_WithANullActionOrAnInvalidId_IsRefused()
	{
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: true);

		Assert.False(actions.TryRegisterUse(ItemId, null!));
		Assert.False(actions.TryRegisterUse("Not A Valid Id", _ => { }));
		Assert.False(actions.TryRegisterUse(string.Empty, _ => { }));

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("invalid item id", StringComparison.Ordinal));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("null", StringComparison.Ordinal));
	}

	[Fact]
	public void UseAction_ForAnItemThatDeclaresNoUsability_IsRefused()
	{
		// Reachability: the game invokes `useAction` at exactly two sites — Body.UseItem
		// behind `if (item.Stats.usable)` (Body.cs:2477) and Body.UseItemInHand behind
		// `conscious && HoldingItem && usable && usableWithLMB` (Body.cs:2452) — so an
		// action registered for an item that declares no flag at all is code no gesture
		// can ever run.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModItemDefinition { Id = ItemId });
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Usable", StringComparison.Ordinal));
	}

	[Fact]
	public void UseAction_ForAnItemThatDeclaresOnlyUsableWithLmb_IsRefused()
	{
		// `usable` is NECESSARY, not one of two: both of the game's use call sites
		// require it, and `usableWithLMB` alone reaches neither, so a registration for
		// this shape would succeed, log success and be dead — while the load-time
		// report stayed silent because the store DOES hold it. The rail is what keeps
		// those two answers from disagreeing.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModItemDefinition { Id = ItemId, UsableWithLmb = true });
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("UsableWithLmb only adds", StringComparison.Ordinal));
	}

	[Fact]
	public void LimbAction_ForAnItemThatDeclaresNoLimbUsability_IsRefused()
	{
		// The same rail on the limb half, whose flag is a different one: the wound view
		// runs useLimbAction behind `usableOnLimb`, so an item that declares only
		// Usable can carry a use action but never a limb one.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModItemDefinition { Id = ItemId, Usable = true });
		var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.False(actions.TryRegisterLimbUse(ItemId, _ => { }));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("UsableOnLimb", StringComparison.Ordinal));
	}

	[Fact]
	public void LimbAction_ForAnItemThatDeclaresLimbUsability_IsAccepted()
	{
		// The positive half of the rail above: the flag the game's own gate reads is
		// what makes the registration reachable, and declaring it is the mod's own say.
		var contentStore = new ModContentStore();
		contentStore.Add("mod.a", null, new ModItemDefinition { Id = ItemId, UsableOnLimb = true });
		var (actions, _) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

		Assert.True(actions.TryRegisterLimbUse(ItemId, _ => { }));
		Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
	}

	[Fact]
	public void UseAction_ForAnItemWithItsOwnToolOrGunBehaviour_IsRefused()
	{
		// One item, one use action. The definition's own Tool/Gun behaviour is what the
		// game would run, so a registered action beside it would be a second claimant
		// decided by registration order — the shape the entries of a gesture keep out.
		foreach (var definition in new IModItemDefinition[]
		{
			new ModItemDefinition { Id = ItemId, Tool = new ModItemTool() },
			new ModItemDefinition { Id = ItemId, Gun = new ModItemGun() },
		})
		{
			var contentStore = new ModContentStore();
			contentStore.Add("mod.a", null, definition);
			var (actions, entries) = AdapterFor("mod.a", AuthorEffects, registerItem: false, contentStore: contentStore);

			Assert.False(actions.TryRegisterUse(ItemId, _ => { }));
			Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Tool or Gun", StringComparison.Ordinal));
		}
	}

	[Fact]
	public void Store_ResolvesTheHalfThatWasRegistered_AndNothingElse()
	{
		var store = new ModItemEffectStore();
		var (actions, _) = AdapterFor("mod.a", AuthorEffects, registerItem: true, effectStore: store);
		Assert.True(actions.TryRegisterUse(ItemId, _ => { }));

		Assert.NotNull(store.FindUse(ItemId));
		Assert.Null(store.FindLimbUse(ItemId));
		Assert.Null(store.FindUse("never_registered"));

		// The load-time diagnostic asks about each half SEPARATELY, and this is why: a
		// definition that declares usability while its mod registered only the limb
		// action would pass a general "is this item registered" question, and the player
		// would use an item that does nothing with no line saying so.
		Assert.True(store.HasUse(ItemId));
		Assert.False(store.HasLimbUse(ItemId));
		Assert.True(actions.TryRegisterLimbUse(ItemId, _ => { }));
		Assert.True(store.HasLimbUse(ItemId));
		Assert.False(store.HasLimbUse("never_registered"));
		Assert.Equal("mod.a", store.FindUse(ItemId)!.ModId);
	}

	[Fact]
	public void RemoveMod_TakesBackBothHalvesOfThatModAndLeavesTheOtherModsAlone()
	{
		// A mod registers before it can fail to load, so a failed load has to withdraw
		// its slice: the Game Adapter's resolver reads this store, and an entry left
		// behind would run the code of a mod the process never loaded.
		var store = new ModItemEffectStore();
		var (first, _) = AdapterFor("mod.a", AuthorEffects, registerItem: true, effectStore: store);
		Assert.True(first.TryRegisterUse(ItemId, _ => { }));
		Assert.True(first.TryRegisterLimbUse(ItemId, _ => { }));

		var (second, _) = AdapterFor(OtherModId, AuthorEffects, registerItem: true, effectStore: store, itemId: "b_widget");
		Assert.True(second.TryRegisterUse("b_widget", _ => { }));

		Assert.Equal(2, store.RemoveMod("mod.a"));
		Assert.Null(store.FindUse(ItemId));
		Assert.Null(store.FindLimbUse(ItemId));
		Assert.NotNull(store.FindUse("b_widget"));
		Assert.Equal(0, store.RemoveMod("mod.a"));
	}

	[Fact]
	public void Register_ReportsTheHalfItFiled()
	{
		var store = new ModItemEffectStore();
		var (actions, _) = AdapterFor("mod.a", AuthorEffects, registerItem: true, effectStore: store);
		Assert.True(actions.TryRegisterLimbUse(ItemId, _ => { }));

		Assert.True(store.Registrations[0].IsLimbUse);
		Assert.Equal(ItemId, store.Registrations[0].ItemId);
	}

	private const ModPermission AuthorEffects = ModPermission.AuthorEffects;

	/// <summary>An item that declares BOTH halves' flags, so a case that is about another rail is not stopped by this one.</summary>
	private static ModItemDefinition UsableItem(string id) => new() { Id = id, Usable = true, UsableOnLimb = true };

	private static (ModItemActionsAdapter Actions, List<(LogLevel Level, string Message)> Entries) AdapterFor(
		string modId,
		ModPermission permissions,
		bool registerItem,
		ModContentStore? contentStore = null,
		ModItemEffectStore? effectStore = null,
		string itemId = ItemId)
	{
		contentStore ??= new ModContentStore();
		if (registerItem)
		{
			contentStore.Add(modId, null, UsableItem(itemId));
		}

		var log = new RecordingLogger<ModItemActionsAdapter>();
		var manifest = new ModManifest(modId, modId, "1.0.0", NetworkMode.Synchronized, null, permissions);
		return (new ModItemActionsAdapter(manifest, contentStore, effectStore ?? new ModItemEffectStore(), log), log.Entries);
	}
}
