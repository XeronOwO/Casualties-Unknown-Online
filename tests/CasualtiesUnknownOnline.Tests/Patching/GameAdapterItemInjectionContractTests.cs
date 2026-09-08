using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The accept-vs-inject rule of the item content provider, exercised against the
/// real Game Adapter assembly: a mod definition whose id already exists in the
/// vanilla item table is accepted but NEVER injected, so the vanilla entry (and
/// its <c>cu:&lt;id&gt;</c> resource id) stays authoritative. The pure table →
/// entry mapping is covered by <see cref="VanillaItemResourceLocationSourceContractTests"/>;
/// this test locks the wiring that decides which ids count as injected.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class GameAdapterItemInjectionContractTests
{
	[Fact]
	public void CollidingModItemId_IsAcceptedButNeverInjected_AndVanillaResourceIdSurvives()
	{
		var itemType = GameAssemblyHost.ResolveType("Item")
			?? throw new InvalidOperationException("Item not found in game assembly.");
		var itemInfoType = GameAssemblyHost.ResolveType("ItemInfo")
			?? throw new InvalidOperationException("ItemInfo not found in game assembly.");
		var providerType = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider", throwOnError: true)!;
		var sourceType = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Content.VanillaItemResourceLocationSource", throwOnError: true)!;

		var table = (IDictionary)Activator.CreateInstance(
			typeof(Dictionary<,>).MakeGenericType(typeof(string), itemInfoType))!;
		var vanillaInfo = Activator.CreateInstance(itemInfoType)!;
		itemInfoType.GetField("fullName")!.SetValue(vanillaInfo, "Vanilla Bandage");
		table.Add("bandage", vanillaInfo);
		itemType.GetField("GlobalItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.SetValue(null, table);

		var provider = Activator.CreateInstance(providerType, CreateLogger(providerType))!;
		var payload = new ModItemDefinition { DisplayName = "Mod Bandage" }.ToPayload();
		var registration = new ModContentRegistration(
			"mod.a", new ModContentDefinition("bandage", ModContentKind.Item, payload));

		Assert.True((bool)providerType.GetMethod("TryBind")!.Invoke(provider, [registration])!,
			"a mod definition may be accepted even when its id collides with a vanilla item");
		providerType.GetMethod("Update")!.Invoke(provider, null);

		var injected = (IReadOnlyCollection<string>)providerType
			.GetProperty("InjectedItemIds", BindingFlags.NonPublic | BindingFlags.Instance)!
			.GetValue(provider)!;
		Assert.Empty(injected);

		var source = Activator.CreateInstance(sourceType, provider, CreateLogger(sourceType))!;
		var entry = Assert.Single(((IResourceLocationSource)source).Entries);
		Assert.Equal("cu:bandage", entry.Id.ToString());
		Assert.Equal("Vanilla Bandage", entry.DisplayName);
	}

	private static ILogger CreateLogger(Type forType) =>
		(ILogger)Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(forType))!;
}
