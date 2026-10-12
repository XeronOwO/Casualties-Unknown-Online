using System;
using System.Collections;
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
/// The item declaration surface's reach: what a mod can put into the game's own
/// item table. <c>ModItemInfoFactory.Build</c> is the only <c>new ItemInfo</c>
/// under <c>src/</c>, and the game reads more of that type than the declaration
/// could write — including <c>ItemInfo.rec</c>, whose own field initialiser asks
/// for INT 2 and whose value <c>PlayerCamera.ItemHoverDescription</c>
/// (PlayerCamera.cs:1043-1055) prints at the player while replacing the item's
/// name with "unknown object". The test project never compile-references
/// GameAdapter, so the build site is driven reflectively, through the real
/// provider.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class ItemDeclarationParityTests
{
	private const string ProviderTypeName = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";

	/// <summary>The floor the field census cannot fall below: the game's own type carries more than this, and a scan that silently finds nothing must not pass.</summary>
	private const int ItemInfoFieldFloor = 30;

	/// <summary>The floor the decay-flag census cannot fall below: the game declares four flags.</summary>
	private const int DecayFlagFloor = 4;

	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(ProviderTypeName, throwOnError: true)!;

	[Fact]
	public void Item_DeclaringNoRecognitionGate_IsRecognizableByAnyPlayer()
	{
		var provider = CreateProvider(out _);

		// A definition that declares nothing about recognition: the author wrote a
		// name and a description and asked for no gate. What the game's own data
		// class leaves behind — `public Recognition rec = new Recognition(2)`
		// (ItemInfo.cs:181) — is not that author's intent, and every one of the
		// game's own 218 item definitions overwrites it.
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_bandage",
			DisplayName = "Field Bandage",
			Description = "A dressing the example declares."
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		Assert.Equal(0, RecognitionMinimum(GetItemInfo("custom_bandage")));
	}

	[Fact]
	public void Item_DeclaringEveryField_ReachesEveryPublicFieldTheGameReads()
	{
		var provider = CreateProvider(out _);

		// Probe A declares every member to a value the game's own type does not
		// already carry. Battery is the one exception and probe B carries it:
		// declaring it forces destroyAtZeroCondition false (so that field would
		// read as unreached) and it ORs the framework-owned decay bit in.
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_everything",
			DisplayName = "Everything",
			Description = "Declared to the brim.",
			Category = "tool",
			Weight = 3.5f,
			Value = 12,
			Usable = true,
			UsableWithLmb = true,
			UsableOnLimb = true,
			DestroyAtZeroCondition = true,
			Tags = "tool,medicine",
			RecognitionMinimum = 3,
			OnlyHoldInHands = true,
			Combineable = true,
			IgnoreDepression = true,
			ScaleWeightWithCondition = true,
			AutoAttack = true,
			JumpHeightMultiplierChange = 0.44f,
			SlotRotation = 42f,
			DecayMinutes = 30f,
			DecayConditions = ModItemDecayCondition.OnlyWhenWorn | ModItemDecayCondition.OnlyWhenMoving,
			Wearable = new ModItemWearable
			{
				Limb = "Head",
				SlotId = "hat",
				CanBeHeld = true,
				Armor = 0.25f,
				Isolation = 3f,
				HitDurabilityLossMultiplier = 0.5f,
				VisualOffset = 7
			},
			Qualities = [new ModCraftingQuality { Id = "dressing" }]
		}));
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_everything_battery",
			DecayMinutes = 30f,
			Battery = new ModItemBattery { StartCharge = 1f }
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var reached = ReachedFields(GetItemInfo("custom_everything"))
			.Concat(ReachedFields(GetItemInfo("custom_everything_battery")))
			.ToHashSet(StringComparer.Ordinal);
		var declared = PublicItemInfoFields();
		var unreached = declared.Where(name => !reached.Contains(name)).ToArray();

		Assert.True(declared.Length >= ItemInfoFieldFloor,
			$"the field census found {declared.Length} public ItemInfo fields; the scan surface is not the game's own type");
		Assert.True(unreached.Length == 0,
			$"no declaration reaches {string.Join(", ", unreached)} (reached {reached.Count} of {declared.Length} public fields)");
	}

	[Fact]
	public void TheDecayVocabulary_ReachesEveryFlagTheGameReads()
	{
		var provider = CreateProvider(out _);

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_decay_conditions",
			DecayMinutes = 10f,
			DecayConditions = ModItemDecayCondition.All
		}));
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_decay_battery",
			DecayMinutes = 10f,
			Battery = new ModItemBattery { StartCharge = 1f }
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var reached = GetDecayFlags("custom_decay_conditions") | GetDecayFlags("custom_decay_battery");
		var declared = DeclaredDecayFlags();

		Assert.True(declared.Length >= DecayFlagFloor,
			$"the decay census found {declared.Length} ItemInfo.DecayType flags; the scan surface is not the game's own enum");
		Assert.Empty(declared
			.Where(flag => (reached & flag) == 0)
			.Select(flag => $"ItemInfo.DecayType.{flag}"));

		// Reachability alone would survive a swap: renumber the CUO vocabulary while the
		// game keeps its own values and every flag is still reached, with `OnlyWhenWorn`
		// silently meaning "only while moving". So each constant is pinned to the game's
		// flag BY THE GAME'S OWN NAME.
		Assert.Equal((int)ModItemDecayCondition.OnlyWithContents, GameDecayFlag("NoDecayWithoutContainerItem"));
		Assert.Equal((int)ModItemDecayCondition.OnlyWhenWorn, GameDecayFlag("NoDecayWhenNotWorn"));
		Assert.Equal((int)ModItemDecayCondition.OnlyWhenMoving, GameDecayFlag("NoDecayWhenStill"));
	}

	[Fact]
	public void Item_DeclaringBatteryAndACondition_BuildsTheShapeTheGamesOwnContentDeclares()
	{
		var provider = CreateProvider(out _);

		// The game's own `gravbag` declares `decayInfo = 17` — the battery bit AND the
		// "only while it holds items" bit — so the combination is one the game's own
		// content uses. The framework ORs a declaration's condition bits into the same byte
		// the Battery slice sets its own bit in, and the validator must not read the pair as
		// a value outside the vocabulary.
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_gravbag",
			DecayMinutes = 100f,
			DecayConditions = ModItemDecayCondition.OnlyWithContents,
			Battery = new ModItemBattery { StartCharge = 1f }
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		Assert.Equal(17, GetDecayFlags("custom_gravbag"));
	}

	[Fact]
	public void Item_DeclaringNothing_KeepsTheGamesOwnValuesForEverythingElse()
	{
		var provider = CreateProvider(out _);

		Assert.True(TryBind(provider, new ModItemDefinition { Id = "custom_bare" }));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_bare");
		Assert.Equal(0, RecognitionMinimum(info)); // the one intended change: the author's name is shown
		Assert.Equal(0, GetByte(info, "decayInfo"));
		Assert.Equal(5, GetInt(info, "wearableVisualOffset")); // the game's own field initialiser, not a CUO default
		Assert.False(GetBool(info, "onlyHoldInHands"));
		Assert.False(GetBool(info, "combineable"));
		Assert.False(GetBool(info, "ignoreDepression"));
		Assert.False(GetBool(info, "scaleWeightWithCondition"));
		Assert.False(GetBool(info, "autoAttack"));
		Assert.False(GetBool(info, "wearable"));
		Assert.Equal(0f, GetFloat(info, "jumpHeightMultChange"));
		Assert.Equal(0f, GetFloat(info, "slotRotation"));
	}

	[Fact]
	public void TheDerivedTagArray_IsTheOnlyFieldTheTypeKeepsPrivate_AndTheFactoryFillsIt()
	{
		// The durable half of the defect this class's two tag cases pin. The parity
		// case above enumerates PUBLIC fields, and the derived tag array is not one:
		// `private string[] actualTags` sits behind `GetTags` / `HasTag`, so a
		// tagless definition left it null with every public-field assertion green —
		// and the game's own `HasTag` then threw once per frame on the hand slot.
		// The census keeps the surface honest WITHIN ITS OWN REACH: it counts private
		// FIELDS, so a game update that adds a second one fails here and that field
		// gets its own rule instead of silently joining the class of fields nothing
		// populates. A new private PROPERTY or METHOD, or a non-private field, is
		// outside this assertion — the class of fields is what this type keeps to
		// itself, and the count below is what makes the claim equal to the check.
		var declared = GameAssemblyHost.ResolveType("ItemInfo")!
			.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(field => field.IsPrivate)
			.Select(field => field.Name)
			.ToArray();

		Assert.Equal(["actualTags"], declared);

		var provider = CreateProvider(out _);
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_derived_tags",
			Tags = "tool,medicine"
		}));
		Assert.True(TryBind(provider, new ModItemDefinition { Id = "custom_derived_tags_bare" }));

		PrepareGameTables();
		InvokeUpdate(provider);

		// Read through the game's own accessor rather than the field: an array that
		// exists but was filled from the wrong source would pass a null check.
		Assert.Equal(["tool", "medicine"], GetTags(GetItemInfo("custom_derived_tags")));
		Assert.Equal([""], GetTags(GetItemInfo("custom_derived_tags_bare")));
	}

	[Fact]
	public void Item_DeclaringTags_AnswersTheGamesOwnHasTag()
	{
		var provider = CreateProvider(out _);

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_labelled",
			Tags = "tool,medicine"
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_labelled");
		Assert.True(HasTag(info, "tool"));
		Assert.True(HasTag(info, "medicine"));
		Assert.False(HasTag(info, "gun"));
	}

	[Fact]
	public void Item_DeclaringNoTags_StillAnswersTheGamesOwnHasTag()
	{
		var provider = CreateProvider(out _);

		// The definition the batch's fixture carried: a declared flag with no
		// function, and no `Tags` at all — the common case, and the data class's own
		// default. The game's table pass derives `actualTags` for every entry it
		// ships (`Item.cs`'s `SetTags()` over `Item.GlobalItems`), and CUO injects
		// after it, so the array has to exist even when it is empty: `SetTags()`
		// splits the empty string into `[""]`, and `Enumerable.Contains` over THAT
		// answers false. Left at its CLR default it is null, and the game's own
		// `HasTag` throws `ArgumentNullException` — once on creation through
		// `Item.Start`, and once per frame through `PlayerCamera.HandleGunMenu`
		// while the item sits in the hand slot (batch `20261011-k` measured both).
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_untagged",
			DisplayName = "Placebo"
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_untagged");
		Assert.False(HasTag(info, "gun"));
		Assert.False(HasTag(info, "medicine"));

		// What the game's own builder leaves behind for a tagless entry, recorded from
		// the SOURCE rather than observed in an execution: `SetTags` is
		// `tags.Split(',', 0)` (`ItemInfo.cs:72`, the netstandard2.1
		// `Split(char, StringSplitOptions.None)` overload), and a split that finds no
		// separator returns the whole string as one element — so the empty string
		// yields ONE empty element, the array is present, and `HasTag("")` answers
		// true, exactly as it does for a vanilla item that declares no tags. The
		// game's own `SetTags` never runs in this suite — `PrepareGameTables` builds a
		// fresh `Item.GlobalItems`, so the only writer exercised here is CUO's — which
		// is why the equivalence is a reading of that source line and not a result.
		// Nothing in the game asks for the empty tag; a case asserting the opposite
		// would be describing a shape this build site does not produce.
		Assert.True(HasTag(info, ""));
	}

	[Fact]
	public void Item_DeclaringANegativeRecognitionMinimum_IsRefused()
	{
		var provider = CreateProvider(out var entries);

		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_negative_recognition",
			RecognitionMinimum = -1
		}));

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_negative_recognition", StringComparison.Ordinal)
			&& entry.Message.Contains("RecognitionMinimum", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(-1.5f, false)]
	[InlineData(-1f, false)] // the factor the game computes is `value + 1`: zero here, negative below
	[InlineData(-0.1f, true)] // the game's own penalty value
	[InlineData(0.44f, true)] // the game's own booster value
	public void Item_DeclaringAJumpHeightChange_IsRefusedWhenTheFactorIsNotPositive(float change, bool expected)
	{
		var provider = CreateProvider(out var entries);

		var bound = TryBind(provider, new ModItemDefinition
		{
			Id = "custom_boots",
			JumpHeightMultiplierChange = change
		});

		Assert.Equal(expected, bound);
		if (!expected)
		{
			Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
				&& entry.Message.Contains("custom_boots", StringComparison.Ordinal)
				&& entry.Message.Contains("JumpHeightMultiplierChange", StringComparison.Ordinal));
		}
	}

	[Theory]
	[InlineData(16)] // the battery bit: the game's decay tick drains the component behind it (Item.cs:198-201)
	[InlineData(8)]
	public void Item_DeclaringADecayBitOutsideTheVocabulary_IsRefused(int bits)
	{
		var provider = CreateProvider(out var entries);

		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_decay_unknown",
			DecayMinutes = 10f,
			DecayConditions = (ModItemDecayCondition)bits
		}));

		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_decay_unknown", StringComparison.Ordinal)
			&& entry.Message.Contains("DecayConditions", StringComparison.Ordinal));
	}

	[Fact]
	public void Item_DeclaringDecayConditionsWithNoDecayTime_IsRegisteredAndReported()
	{
		var provider = CreateProvider(out var entries);

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_inert_decay",
			DecayConditions = ModItemDecayCondition.OnlyWhenWorn
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		// The item exists with the flag its declaration asked for; what is reported is
		// that nothing can act on it, because only DecayMinutes starts a decay at all.
		Assert.Equal((byte)ModItemDecayCondition.OnlyWhenWorn, GetByte(GetItemInfo("custom_inert_decay"), "decayInfo"));
		Assert.Contains(entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("custom_inert_decay", StringComparison.Ordinal)
			&& entry.Message.Contains("DecayConditions", StringComparison.Ordinal)
			&& entry.Message.Contains("DecayMinutes", StringComparison.Ordinal));
	}

	/// <summary>The public instance fields of the game's own item type, by name.</summary>
	private static string[] PublicItemInfoFields() =>
		GameAssemblyHost.ResolveType("ItemInfo")!
			.GetFields(BindingFlags.Public | BindingFlags.Instance)
			.Select(field => field.Name)
			.ToArray();

	/// <summary>The flags the game's own decay enum declares, as its own bit values.</summary>
	private static int[] DeclaredDecayFlags() =>
		Enum.GetValues(GameAssemblyHost.ResolveType("ItemInfo+DecayType")!)
			.Cast<object>()
			.Select(Convert.ToInt32)
			.ToArray();

	/// <summary>
	/// Which of the item type's public fields the built item carries a value of its own
	/// for, compared against a freshly constructed instance of the same type. The
	/// comparison is per KIND rather than per reference, because a reference comparison
	/// would report every object-typed field as reached whatever the factory did:
	/// <c>Recognition</c> by its gate, a list by its length, everything else by equality.
	/// </summary>
	private static IEnumerable<string> ReachedFields(object info)
	{
		var baseline = Activator.CreateInstance(info.GetType())!;
		foreach (var field in info.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (Differs(field, field.GetValue(info), field.GetValue(baseline)))
			{
				yield return field.Name;
			}
		}
	}

	private static bool Differs(FieldInfo field, object? value, object? baseline) =>
		field.FieldType.Name == "Recognition" ? RecognitionGate(value) != RecognitionGate(baseline)
		: typeof(IList).IsAssignableFrom(field.FieldType) ? ListCount(value) != ListCount(baseline)
		: !Equals(value, baseline);

	private static int? ListCount(object? value) => value is IList list ? list.Count : null;

	/// <summary>One flag of the game's own decay enum, by the name the game gave it.</summary>
	private static int GameDecayFlag(string name) =>
		Convert.ToInt32(Enum.Parse(GameAssemblyHost.ResolveType("ItemInfo+DecayType")!, name));

	/// <summary>The decay byte's flags, as the game's own values.</summary>
	private static int GetDecayFlags(string id) => GetByte(GetItemInfo(id), "decayInfo");

	private static object CreateProvider(out List<(LogLevel Level, string Message)> entries)
	{
		var loggerType = typeof(RecordingLogger<>).MakeGenericType(ProviderType);
		var logger = Activator.CreateInstance(loggerType)!;
		entries = (List<(LogLevel Level, string Message)>)loggerType
			.GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetValue(logger)!;

		return GameAdapterProviderFactory.Create(ProviderType, logger);
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

	/// <summary>The derived array itself, read through the game's own accessor — null when nothing filled it, which is the state the game then throws over.</summary>
	private static string[]? GetTags(object info) =>
		(string[]?)info.GetType().GetMethod("GetTags")!.Invoke(info, null);

	/// <summary>
	/// The game's own tag query, run against a built entry. It is the call the game's
	/// item paths make (<c>Item.Start</c>, <c>Body.HandleVisuals</c>,
	/// <c>PlayerCamera.HandleGunMenu</c>), and it reads the derived array the table
	/// pass fills — so asking it is the only way to prove the array exists:
	/// <c>Enumerable.Contains</c> throws on a null source instead of answering.
	/// </summary>
	private static bool HasTag(object info, string tag) =>
		(bool)info.GetType().GetMethod("HasTag")!.Invoke(info, [tag])!;

	/// <summary>The gate the game's own hover and HUD read: <c>Recognition.recognizable</c> is <c>min &lt;= INT</c>; null when the item carries no gate at all.</summary>
	private static int? RecognitionMinimum(object info) =>
		RecognitionGate(info.GetType().GetField("rec")?.GetValue(info));

	/// <summary>The INT level one <c>Recognition</c> value asks for, or null when there is no such value.</summary>
	private static int? RecognitionGate(object? recognition) =>
		recognition is null ? null : (int)recognition.GetType().GetField("min")!.GetValue(recognition)!;

	private static bool GetBool(object info, string name) => (bool)info.GetType().GetField(name)!.GetValue(info)!;

	private static int GetInt(object info, string name) => (int)info.GetType().GetField(name)!.GetValue(info)!;

	private static byte GetByte(object info, string name) => (byte)info.GetType().GetField(name)!.GetValue(info)!;

	private static float GetFloat(object info, string name) => (float)info.GetType().GetField(name)!.GetValue(info)!;
}
