using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The wearable declaration's placement contract. A mod item that declares itself
/// wearable reaches the game's own wear flow, which reads
/// <c>ItemInfo.desiredWearLimb</c> and <c>ItemInfo.wearSlotId</c> with no null
/// check: <c>Body.WearWearable</c> (Body.cs:1493-1494) resolves the limb NAME with
/// <c>LimbByName</c>, which returns null for a name no limb carries, and
/// dereferences the result on the next line. So what the item provider BUILDS is
/// the contract this suite pins — the wearable flag and the placement travel
/// together, or the flag is not installed at all — and the test project never
/// compile-references GameAdapter, so the build site is driven reflectively.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class ItemWearableDeclarationTests
{
	private const string ProviderTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";

	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(ProviderTypeName, throwOnError: true)!;

	[Fact]
	public void Item_DeclaredWearableWithNoPlacement_IsNotMarkedWearableAndIsReported()
	{
		var provider = CreateProvider(out var entries);

		// The declaration a mod writes when it marks an item wearable and names
		// nowhere to hang it — the input the pre-change contract could only express
		// as `Wearable = true`, and the red this case was written against.
		Assert.True(TryBind(provider, new ModItemDefinition { Id = "custom_vest", Wearable = new ModItemWearable() }));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_vest");
		Assert.False(GetBool(info, "wearable")); // the game's own wear flow would dereference the limb its placement does not name
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_vest", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringAPlacement_MapsEveryFieldTheGameReadsToPlaceAndWearIt()
	{
		var provider = CreateProvider(out _);

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_rig",
			Wearable = new ModItemWearable
			{
				Limb = "Head",
				SlotId = "hat",
				CanBeHeld = true,
				Armor = 0.25f,
				Isolation = 3f,
				HitDurabilityLossMultiplier = 0.5f,
				VisualOffset = 7
			}
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_rig");
		Assert.True(GetBool(info, "wearable"));
		Assert.Equal("Head", GetString(info, "desiredWearLimb"));
		Assert.Equal("hat", GetString(info, "wearSlotId"));
		Assert.True(GetBool(info, "wearableCanBeHeld"));
		Assert.Equal(0.25f, GetFloat(info, "wearableArmor"));
		Assert.Equal(3f, GetFloat(info, "wearableIsolation"));
		Assert.Equal(0.5f, GetFloat(info, "wearableHitDurabilityLossMultiplier"));
		Assert.Equal(7, GetInt(info, "wearableVisualOffset"));
	}

	[Fact]
	public void Item_DeclaringANegativeWearableNumber_IsRefused()
	{
		var provider = CreateProvider(out var entries);

		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_plate",
			Wearable = new ModItemWearable { Limb = "Torso", SlotId = "armor", Armor = -1f }
		}));

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_plate", StringComparison.Ordinal)
			&& entry.Message.Contains("Wearable", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringOnlyAPlacement_KeepsTheGamesOwnDefaultsForTheRest()
	{
		var provider = CreateProvider(out _);

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_scarf",
			Wearable = new ModItemWearable { Limb = "Head", SlotId = "scarf" }
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_scarf");
		Assert.True(GetBool(info, "wearable"));
		Assert.False(GetBool(info, "wearableCanBeHeld"));
		Assert.Equal(0f, GetFloat(info, "wearableArmor"));
		Assert.Equal(0f, GetFloat(info, "wearableHitDurabilityLossMultiplier"));
		Assert.Equal(5, GetInt(info, "wearableVisualOffset")); // the game's own field initialiser, not a CUO default
	}

	[Theory]
	[InlineData(new[] { "Head", "Torso", "LeftArm" }, "Head", true, 0)]
	[InlineData(new[] { "Head", "Torso", "LeftArm" }, "LeftArm", true, 2)]
	[InlineData(new[] { "Head", "Torso" }, "head", false, -1)] // the game's own == (Body.cs:1471) folds no case
	[InlineData(new[] { "Head", "Torso" }, "", false, -1)]
	[InlineData(new[] { "Head", "Torso" }, "Tail", false, -1)]
	[InlineData(new[] { "Head", "Head" }, "Head", true, 0)] // the first match wins, exactly as LimbByName's loop does
	public void TheLimbNameRule_MatchesTheGamesOwnComparison(
		string[] limbNames,
		string limbName,
		bool expectedFound,
		int expectedIndex)
	{
		var placement = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameWearPlacement", throwOnError: true)!;
		var method = placement.GetMethod(
			"TryIndexOfLimbName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryIndexOfLimbName not found.");

		object?[] arguments = [limbNames, limbName, null];
		var found = (bool)method.Invoke(null, arguments)!;

		Assert.Equal(expectedFound, found);
		Assert.Equal(expectedIndex, found ? (int)arguments[2]! : -1);
	}

	private static object CreateProvider(out List<(LogLevel Level, string Message)> entries)
	{
		var loggerType = typeof(RecordingLogger<>).MakeGenericType(ProviderType);
		var logger = Activator.CreateInstance(loggerType)!;
		entries = (List<(LogLevel Level, string Message)>)loggerType
			.GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetValue(logger)!;
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, IModContentDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		return (bool)bind.Invoke(provider, [new ModContentRegistration("mod.a", definition)])!;
	}

	private static void PrepareGameTables()
	{
		var itemType = GameAssemblyHost.ResolveType("Item")
			?? throw new InvalidOperationException("Item not found in game assembly.");
		var itemInfoType = GameAssemblyHost.ResolveType("ItemInfo")
			?? throw new InvalidOperationException("ItemInfo not found in game assembly.");
		var itemDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), itemInfoType);
		itemType.GetField("GlobalItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.SetValue(null, Activator.CreateInstance(itemDictType)!);
	}

	private static void InvokeUpdate(object provider)
	{
		var update = provider.GetType().GetMethod(
			"Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Update not found.");
		update.Invoke(provider, null);
	}

	private static object GetItemInfo(string id)
	{
		var itemType = GameAssemblyHost.ResolveType("Item")
			?? throw new InvalidOperationException("Item not found in game assembly.");
		var itemDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), GameAssemblyHost.ResolveType("ItemInfo")!);
		var dict = (IDictionary)itemType.GetField(
			"GlobalItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		return dict[id]!;
	}

	private static bool GetBool(object info, string name) =>
		(bool)info.GetType().GetField(name)!.GetValue(info)!;

	private static int GetInt(object info, string name) =>
		(int)info.GetType().GetField(name)!.GetValue(info)!;

	private static float GetFloat(object info, string name) =>
		(float)info.GetType().GetField(name)!.GetValue(info)!;

	private static string GetString(object info, string name) =>
		(string?)info.GetType().GetField(name)!.GetValue(info) ?? string.Empty;
}
