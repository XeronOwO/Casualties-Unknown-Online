using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The crafting-quality label contract across the three content providers: a mod
/// item's declared qualities must reach the field the game's own recipe matcher
/// reads (<c>ItemInfo.qualities</c> via <c>Item.GetQualityThatMeetsCriteria</c>),
/// a label the game could not match must be refused at bind, and a recipe
/// ingredient whose label no provider in ITS OWN direction can satisfy must be
/// refused instead of injected as a recipe that can never be crafted — the game's
/// matcher is direction-selected (an item ingredient is matched against item
/// qualities, a liquid one against liquid qualities) and amount-aware. The test
/// project never compile-references GameAdapter, so the whole contract is driven
/// reflectively.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class CraftingQualityLabelTests
{
	private const string ItemProviderType = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";
	private const string RecipeProviderType = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterRecipeContentProvider";
	private const string LiquidProviderType = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidContentProvider";
	private const string QualitySourceType = "CasualtiesUnknownOnline.GameAdapter.Content.ICraftingQualitySource";

	private static object CreateItemProvider() => CreateProvider(ItemProviderType, []);

	private static object CreateLiquidProvider() => CreateProvider(LiquidProviderType, []);

	private static object CreateRecipeProvider(params object[] qualitySources)
	{
		var sourceType = GameAssemblyHost.Adapter.GetType(QualitySourceType, throwOnError: true)!;
		var sources = Array.CreateInstance(sourceType, qualitySources.Length);
		for (var i = 0; i < qualitySources.Length; i++)
		{
			sources.SetValue(qualitySources[i], i);
		}

		return CreateProvider(RecipeProviderType, [sources]);
	}

	private static object CreateProvider(string typeName, object[] extraArguments)
	{
		var providerType = GameAssemblyHost.Adapter.GetType(typeName, throwOnError: true)!;
		var loggerType = typeof(NullLogger<>).MakeGenericType(providerType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		var arguments = new object[extraArguments.Length + 1];
		arguments[0] = logger;
		Array.Copy(extraArguments, 0, arguments, 1, extraArguments.Length);
		return Activator.CreateInstance(providerType, arguments)!;
	}

	private static bool TryBind(object provider, IModContentDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration("mod.a", definition);
		return (bool)bind.Invoke(provider, [registration])!;
	}

	private static void InvokeUpdate(object provider)
	{
		var update = provider.GetType().GetMethod(
			"Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Update not found.");
		update.Invoke(provider, null);
	}

	/// <summary>
	/// The three tables the providers read, plus the locale the liquid provider
	/// writes on injection. <c>Item.GlobalItems</c> and <c>Recipes.recipes</c> are
	/// built together by <c>WorldGeneration.Awake</c> (items first) and
	/// <c>Liquids.Registry</c>'s only assignment is its own static constructor, so
	/// a real session always has all three by the time a recipe is built; this test
	/// builds them the same way so the providers see the composition they see in
	/// game. <c>Locale.currentLang</c> is installed because the provider's first
	/// locale step is <c>Locale.LoadLanguage()</c>, which reads Unity's data path
	/// and throws outside the game — the game's own <c>Language()</c> builds every
	/// dictionary the provider touches.
	/// </summary>
	private static void PrepareGameTables()
	{
		SetStaticField("Item", "GlobalItems", NewGameTable("ItemInfo"));
		SetStaticField("Liquids", "Registry", NewGameTable("LiquidType"));
		SetStaticField("Recipes", "recipes", NewGameList("Recipe"));
		SetStaticField("Locale", "currentLang", Activator.CreateInstance(GameType("Language"))!);
	}

	private static void AddVanillaItem(string id, params (string Id, float Amount)[] qualities)
	{
		var info = Activator.CreateInstance(GameType("ItemInfo"))!;
		info.GetType().GetField("qualities")!.SetValue(info, BuildQualityList(qualities));
		((IDictionary)GetStaticField("Item", "GlobalItems"))[id] = info;
	}

	private static void AddVanillaLiquid(string id, params (string Id, float Amount)[] qualities)
	{
		var liquid = Activator.CreateInstance(GameType("LiquidType"))!;
		liquid.GetType().GetField("qualities")!.SetValue(liquid, BuildQualityList(qualities));
		((IDictionary)GetStaticField("Liquids", "Registry"))[id] = liquid;
	}

	private static object BuildQualityList((string Id, float Amount)[] qualities)
	{
		var qualityType = GameType("CraftingQuality");
		var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(qualityType))!;
		foreach (var quality in qualities)
		{
			list.Add(Activator.CreateInstance(qualityType, [quality.Id, quality.Amount])!);
		}

		return list;
	}

	private static IList GetQualities(string tableTypeName, string tableField, string id)
	{
		var entry = ((IDictionary)GetStaticField(tableTypeName, tableField))[id]!;
		return (IList)entry.GetType().GetField("qualities")!.GetValue(entry)!;
	}

	private static IList GetItemQualities(string id) => GetQualities("Item", "GlobalItems", id);

	private static IList GetLiquidQualities(string id) => GetQualities("Liquids", "Registry", id);

	private static IList GetInjectedRecipes() => (IList)GetStaticField("Recipes", "recipes");

	private static List<string> GetInjectedResultIds() =>
		[.. GetInjectedRecipes().Cast<object>().Select(RecipeResultId)];

	/// <summary>
	/// The game's own match, not a re-read of the DTO: the matcher answers with the
	/// quality it accepted (null when none does), asking <c>q.amount &gt;=
	/// target.amount</c>.
	/// </summary>
	private static object? MatchQuality(string qualityId, float amount, IList qualities)
	{
		var target = Activator.CreateInstance(GameType("CraftingQuality"), [qualityId, amount])!;
		var match = GameType("Item").GetMethod(
			"GetQualityThatMeetsCriteria", BindingFlags.Public | BindingFlags.Static)!;
		return match.Invoke(null, [target, qualities]);
	}

	private static string QualityId(object quality) =>
		(string?)quality.GetType().GetField("id")!.GetValue(quality) ?? string.Empty;

	private static float QualityAmount(object quality) =>
		(float)quality.GetType().GetField("amount")!.GetValue(quality)!;

	private static string RecipeResultId(object recipe)
	{
		var result = recipe.GetType().GetField("result")!.GetValue(recipe)!;
		return (string?)result.GetType().GetField("id")!.GetValue(result) ?? string.Empty;
	}

	private static ModItemDefinition ItemWithQuality(string id, string qualityId, float amount) => new()
	{
		Id = id,
		DisplayName = "Mod weave",
		Qualities = [new ModCraftingQuality { Id = qualityId, Amount = amount }]
	};

	private static ModRecipeDefinition RecipeByQuality(
		string id,
		string resultItemId,
		string qualityId,
		float amount = 1f,
		bool isLiquid = false) => new()
		{
			Id = id,
			ResultItemId = resultItemId,
			Ingredients = [new ModRecipeIngredient { Quality = qualityId, QualityAmount = amount, IsLiquid = isLiquid }]
		};

	private static Type GameType(string name) =>
		GameAssemblyHost.ResolveType(name) ?? throw new InvalidOperationException($"{name} not found in the game assembly.");

	private static object GetStaticField(string typeName, string fieldName) =>
		GameType(typeName).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.GetValue(null)!;

	private static void SetStaticField(string typeName, string fieldName, object value) =>
		GameType(typeName).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.SetValue(null, value);

	private static object NewGameTable(string elementTypeName)
	{
		var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), GameType(elementTypeName));
		return Activator.CreateInstance(dictionaryType)!;
	}

	private static object NewGameList(string elementTypeName)
	{
		var listType = typeof(List<>).MakeGenericType(GameType(elementTypeName));
		return Activator.CreateInstance(listType)!;
	}

	[Fact]
	public void ItemQualities_SatisfyTheVanillaRecipeMatcher()
	{
		var provider = CreateItemProvider();
		Assert.True(TryBind(provider, ItemWithQuality("custom_weave", "dressing", 2f)));
		Assert.True(TryBind(provider, ItemWithQuality("custom_bundle", "mymod:material", 0f)));

		PrepareGameTables();
		InvokeUpdate(provider);

		var weave = GetItemQualities("custom_weave");
		Assert.Single(weave);
		Assert.Equal("dressing", QualityId(weave[0]!));
		Assert.Equal(2f, QualityAmount(weave[0]!));

		var accepted = MatchQuality("dressing", 1f, weave);
		Assert.NotNull(accepted);
		Assert.Equal(2f, QualityAmount(accepted!));
		Assert.Null(MatchQuality("rippable", 1f, weave));

		// A non-positive declared amount is normalised to 1, the same rule the
		// liquid provider applies, so the label is still matchable.
		var bundle = GetItemQualities("custom_bundle");
		Assert.Equal(1f, QualityAmount(bundle[0]!));
		Assert.NotNull(MatchQuality("mymod:material", 1f, bundle));
	}

	[Fact]
	public void TryBind_RefusesAQualityLabelThatIsNotCanonical()
	{
		var provider = CreateItemProvider();

		// The two accepted forms: a vanilla-style bare label, and a canonical
		// namespaced content id.
		Assert.True(TryBind(provider, ItemWithQuality("bare", "rippable", 1f)));
		Assert.True(TryBind(provider, ItemWithQuality("namespaced", "mymod:material", 1f)));
		Assert.True(TryBind(provider, ItemWithQuality("underscored", "a1_b2", 1f)));

		// Everything the game's ordinal comparison could never match.
		Assert.False(TryBind(provider, ItemWithQuality("empty", string.Empty, 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("padded", " rippable", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("upper_bare", "Rippable", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("upper_namespaced", "mymod:Material", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("two_separators", "mymod:sub:material", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("no_path", "mymod:", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("no_namespace", ":material", 1f)));
		Assert.False(TryBind(provider, ItemWithQuality("spaced_path", "mymod:mat erial", 1f)));
	}

	[Fact]
	public void LiquidQualities_AreValidatedAndMaterialized()
	{
		var provider = CreateLiquidProvider();
		Assert.True(TryBind(provider, new ModLiquidDefinition
		{
			Id = "custom_solvent",
			DisplayName = "Mod solvent",
			Qualities = [new ModCraftingQuality { Id = "mymod:solvent", Amount = 0f }]
		}));
		Assert.False(TryBind(provider, new ModLiquidDefinition
		{
			Id = "bad_solvent",
			Qualities = [new ModCraftingQuality { Id = "mymod:Solvent" }]
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		// The same label rule and the same amount normalisation as the item side.
		var qualities = GetLiquidQualities("custom_solvent");
		Assert.Single(qualities);
		Assert.Equal("mymod:solvent", QualityId(qualities[0]!));
		Assert.Equal(1f, QualityAmount(qualities[0]!));
	}

	[Fact]
	public void TryBind_TreatsAnExplicitNullQualityListAsNoQualities()
	{
		var itemProvider = CreateItemProvider();
		Assert.True(TryBind(itemProvider, new ModItemDefinition
		{
			Id = "no_qualities",
			DisplayName = "Plain",
			Qualities = null!
		}));

		var liquidProvider = CreateLiquidProvider();
		Assert.True(TryBind(liquidProvider, new ModLiquidDefinition
		{
			Id = "no_qualities_liquid",
			DisplayName = "Plain liquid",
			Qualities = null!
		}));

		// A recipe with no ingredients is refused for that reason, not with the
		// binder's logged exception.
		var recipeProvider = CreateRecipeProvider();
		Assert.False(TryBind(recipeProvider, new ModRecipeDefinition
		{
			Id = "no_ingredients",
			ResultItemId = "log",
			Ingredients = null!
		}));
	}

	[Fact]
	public void Recipe_IsRefusedWhenNoProviderInItsOwnDirectionCarriesTheLabel()
	{
		var itemProvider = CreateItemProvider();
		Assert.True(TryBind(itemProvider, ItemWithQuality("custom_weave", "mymod:material", 1f)));
		Assert.True(TryBind(itemProvider, new ModItemDefinition { Id = "custom_plain", DisplayName = "Plain" }));

		var recipeProvider = CreateRecipeProvider(itemProvider);
		Assert.True(TryBind(recipeProvider, RecipeByQuality("weavable", "custom_weave", "mymod:material")));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("dead", "custom_plain", "mymod:absent")));

		PrepareGameTables();
		InvokeUpdate(itemProvider);
		InvokeUpdate(recipeProvider);

		Assert.Equal(new[] { "custom_weave" }, GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsInjectedFromAModDeclarationBeforeTheItemIsMaterialized()
	{
		// The source-first order, isolated: the mod item is bound but NEVER
		// injected into the table, so nothing but the provider's own declaration
		// can answer for its label.
		var itemProvider = CreateItemProvider();
		Assert.True(TryBind(itemProvider, ItemWithQuality("custom_weave", "mymod:material", 1f)));

		var recipeProvider = CreateRecipeProvider(itemProvider);
		Assert.True(TryBind(recipeProvider, RecipeByQuality("weavable", "log", "mymod:material")));

		PrepareGameTables();
		AddVanillaItem("log");
		InvokeUpdate(recipeProvider);

		Assert.Equal(new[] { "log" }, GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsInjectedFromAModLiquidDeclarationBeforeTheLiquidIsMaterialized()
	{
		// The load-bearing half of the source-first order: the liquid provider is
		// registered AFTER the recipe provider, so a liquid bound in this frame is
		// still missing from Liquids.Registry when the recipe is built.
		var liquidProvider = CreateLiquidProvider();
		Assert.True(TryBind(liquidProvider, new ModLiquidDefinition
		{
			Id = "custom_solvent",
			Qualities = [new ModCraftingQuality { Id = "mymod:solvent", Amount = 0.5f }]
		}));

		var recipeProvider = CreateRecipeProvider(liquidProvider);
		Assert.True(TryBind(recipeProvider, RecipeByQuality("solvent_recipe", "log", "mymod:solvent", 0f, isLiquid: true)));

		PrepareGameTables();
		AddVanillaItem("log");
		InvokeUpdate(recipeProvider);

		Assert.Equal(new[] { "log" }, GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsInjectedWhenAVanillaItemProvidesItsQuality()
	{
		var recipeProvider = CreateRecipeProvider();
		Assert.True(TryBind(recipeProvider, RecipeByQuality("choppable", "log", "cutting", 4f)));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("unprovided", "log", "flogiston")));

		PrepareGameTables();
		AddVanillaItem("log", ("cutting", 200f));
		InvokeUpdate(recipeProvider);

		Assert.Equal(new[] { "log" }, GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsInjectedWhenAVanillaLiquidProvidesItsQuality()
	{
		var recipeProvider = CreateRecipeProvider();
		Assert.True(TryBind(recipeProvider, RecipeByQuality("drinkable", "log", "water", 50f, isLiquid: true)));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("unprovided", "plank", "flogiston", 50f, isLiquid: true)));

		PrepareGameTables();
		AddVanillaItem("log");
		AddVanillaItem("plank");
		AddVanillaLiquid("cleanwater", ("water", 0.5f));
		InvokeUpdate(recipeProvider);

		// Nothing but the liquid table carries 'water', so the liquid half of the
		// vocabulary is what injected this recipe.
		Assert.Equal(new[] { "log" }, GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsRefusedWhenTheLabelComesFromTheOtherDirection()
	{
		// The matcher is direction-selected: an item ingredient is matched against
		// item qualities and a liquid one against liquid qualities, and the two
		// vanilla vocabularies do not overlap. Each recipe below finds its label in
		// the OTHER direction's table only, so neither can ever be crafted.
		var recipeProvider = CreateRecipeProvider();
		Assert.True(TryBind(recipeProvider, RecipeByQuality("item_asking_for_water", "log", "water")));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("liquid_asking_for_cutting", "plank", "cutting", 1f, isLiquid: true)));

		PrepareGameTables();
		AddVanillaItem("log", ("cutting", 200f));
		AddVanillaItem("plank");
		AddVanillaLiquid("cleanwater", ("water", 0.5f));
		InvokeUpdate(recipeProvider);

		Assert.Empty(GetInjectedResultIds());
	}

	[Fact]
	public void Recipe_IsRefusedWhenNoProviderReachesItsAmount()
	{
		// The matcher asks `q.amount >= target.amount` against a FIXED declared
		// amount in the item direction, so presence alone is not matchability.
		var itemProvider = CreateItemProvider();
		Assert.True(TryBind(itemProvider, ItemWithQuality("custom_weave", "mymod:material", 1f)));

		var recipeProvider = CreateRecipeProvider(itemProvider);
		Assert.True(TryBind(recipeProvider, RecipeByQuality("reachable", "log", "mymod:material", 1f)));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("mod_short", "plank", "mymod:material", 5f)));
		Assert.True(TryBind(recipeProvider, RecipeByQuality("vanilla_short", "beam", "cutting", 4f)));

		PrepareGameTables();
		AddVanillaItem("log");
		AddVanillaItem("plank");
		AddVanillaItem("beam");
		AddVanillaItem("axe", ("cutting", 1f));
		InvokeUpdate(itemProvider);
		InvokeUpdate(recipeProvider);

		Assert.Equal(new[] { "log" }, GetInjectedResultIds());
	}
}
