using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// <para>
/// The seam between a mod's declaration and the Game Adapter provider that
/// materializes it used to be the framework's own sealed data class: every
/// provider opened with <c>if (registration.Definition is not ModItemDefinition
/// definition)</c>, so a mod-authored class that implemented the kind contract
/// itself was refused by all nine — even though that contract declares exactly
/// the members the provider reads, and composition (implement the interface,
/// hand back a filled default for every member you do not touch) is the route
/// the contract prescribes for a value the mod computes. The seam is now the
/// INTERFACE (<c>is not IModItemDefinition definition</c>), and this file holds
/// it there.
/// </para>
/// <para>
/// What it proves: for all nine kinds (<c>item</c>, <c>recipe</c>,
/// <c>liquid</c>, <c>liquidtile</c>, <c>tile</c>, <c>building</c>,
/// <c>structure</c>, <c>status</c>, <c>moodle</c>) a class implementing ONLY
/// that kind's interface is accepted by the REAL Game Adapter provider for that
/// kind, driven by the REAL content binder. Each fixture composes one filled
/// framework data class for the members it delegates and never registers a
/// framework instance as the object — the registered object is the mod-authored
/// class, which is what the accepted-registration case reads back. The test
/// project never compile-references GameAdapter, so the provider roster, its
/// constructor arguments and its loggers are built reflectively.
/// </para>
/// <para>
/// Why it exists: ticket
/// <c>docs/backlog/todo/mod-content-attribute-declarations.md</c> discovers a
/// mod's own class and reads its members through this same seam, so a provider
/// that still demanded the framework's class would make that ticket's rows (a
/// computed weight, a mod-authored nested implementation) unreachable.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ModAuthoredDefinitionBindingTests
{
	private const string ModId = "mod.authored";

	private const string ItemProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";
	private const string RecipeProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterRecipeContentProvider";
	private const string LiquidProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidContentProvider";
	private const string LiquidTileProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidTileContentProvider";
	private const string BuildingProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterBuildingContentProvider";
	private const string TileProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterTileContentProvider";
	private const string StructureProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStructureContentProvider";
	private const string StatusProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider";
	private const string MoodleProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterMoodleContentProvider";

	/// <summary>
	/// Every kind in one binder run: nine registrations, one per fixture, and each
	/// fixture implements only its own kind interface while it composes the
	/// framework default underneath. A provider that still read the framework's
	/// class would refuse its entry — the binder would log that refusal and no
	/// Information line would exist for it — so nine <c>accepted</c> lines are the
	/// evidence that the seam is the interface for all nine kinds, and the binder
	/// catching nothing is the evidence that none of them got as far as a throw.
	/// </summary>
	[Fact]
	public void EveryKind_AModAuthoredImplementationOfItsInterface_IsAcceptedByTheRealProvider()
	{
		var run = Run(
			Registration(new ModAuthoredItem(FilledItem("authored.item"), " Mk II", 1f)),
			Registration(new ModAuthoredRecipe(FilledRecipe("authored.recipe"), 3)),
			Registration(new ModAuthoredLiquid(FilledLiquid("authored.liquid"), 2f)),
			Registration(new ModAuthoredLiquidTile(FilledLiquidTile("authored.liquidtile"), 2f)),
			Registration(new ModAuthoredTile(FilledTile("authored.tile"), 2f)),
			Registration(new ModAuthoredBuilding(FilledBuilding("authored.building"), 2f)),
			Registration(new ModAuthoredStructure(FilledStructure("authored.structure"), " Room")),
			Registration(new ModAuthoredStatus(FilledStatus("authored.status"), ".limb")),
			Registration(new ModAuthoredMoodle(FilledMoodle("authored.moodle"), 2f)));

		run.AssertBound(ModContentKind.Item, "authored.item");
		run.AssertBound(ModContentKind.Recipe, "authored.recipe");
		run.AssertBound(ModContentKind.Liquid, "authored.liquid");
		run.AssertBound(ModContentKind.LiquidTile, "authored.liquidtile");
		run.AssertBound(ModContentKind.Tile, "authored.tile");
		run.AssertBound(ModContentKind.Building, "authored.building");
		run.AssertBound(ModContentKind.Structure, "authored.structure");
		run.AssertBound(ModContentKind.Status, "authored.status");
		run.AssertBound(ModContentKind.Moodle, "authored.moodle");
	}

	/// <summary>
	/// The computed member is what the framework reads, not the default the
	/// fixture delegates. This fixture computes both of the item's player-facing
	/// numbers from its own fields, and both differ from the framework default it
	/// composes, so an accepted-with-the-defaults outcome (a provider that read
	/// the class rather than the interface, or one that rebuilt the definition as
	/// a framework instance) could not produce these values.
	/// </summary>
	[Fact]
	public void Item_TheAcceptedRegistrationExposesTheComputedMembersNotTheDelegatedDefault()
	{
		var defaults = FilledItem("authored.computed.item");
		defaults.DisplayName = "Delegated default";
		defaults.Weight = 4f;
		var fixture = new ModAuthoredItem(defaults, " Mk II", 1.5f);
		var registration = Registration(fixture);

		var run = Run(registration);

		run.AssertBound(ModContentKind.Item, "authored.computed.item");

		// The registered object IS the mod-authored fixture — the framework stores
		// the definition it was handed — so reading the registration back reads the
		// computed members the provider accepted.
		var bound = Assert.IsAssignableFrom<IModItemDefinition>(registration.Definition);
		Assert.Same(fixture, bound);
		Assert.Equal("Delegated default Mk II", bound.DisplayName);
		Assert.Equal(5.5f, bound.Weight);
		Assert.NotEqual(defaults.DisplayName, bound.DisplayName);
		Assert.NotEqual(defaults.Weight, bound.Weight);

		// A member this fixture delegates still comes from the composed default.
		Assert.Equal(defaults.Description, bound.Description);
	}

	private static ModContentRegistration Registration(IModContentDefinition definition) =>
		new(ModId, definition);

	/// <summary>
	/// One binder run: the real providers, a recording binder logger and one
	/// recording logger per provider, so a case can assert both what the binder
	/// did and what the provider said about it.
	/// </summary>
	private static BindRun Run(params ModContentRegistration[] entries)
	{
		var providerLogs = new Dictionary<string, List<(LogLevel Level, string Message)>>(StringComparer.Ordinal);
		var providers = new List<IContentBindingProvider>();

		AddProvider(ItemProvider, []);
		AddProvider(RecipeProvider, [Array.CreateInstance(
			GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Content.ICraftingQualitySource", throwOnError: true)!,
			0)]);
		AddProvider(LiquidProvider, []);
		AddProvider(LiquidTileProvider, []);
		AddProvider(BuildingProvider, [new ModBuildingRuntimeStore(NullLogger<ModBuildingRuntimeStore>.Instance)]);
		AddProvider(TileProvider, []);
		AddProvider(StructureProvider, []);
		AddProvider(StatusProvider, []);
		AddProvider(MoodleProvider, []);

		var binderLog = new RecordingLogger<ModContentBinder>();
		var binder = new ModContentBinder(
			new EntriesControl(entries),
			new ModsControl(),
			providers,
			binderLog);
		binder.Update();

		return new BindRun(binderLog.Entries, providerLogs);

		void AddProvider(string typeName, object[] extraArguments)
		{
			var providerType = GameAssemblyHost.Adapter.GetType(typeName, throwOnError: true)!;
			var sink = new List<(LogLevel Level, string Message)>();
			var logger = Activator.CreateInstance(typeof(ProviderLogSink<>).MakeGenericType(providerType), [sink])!;
			var arguments = new object[extraArguments.Length + 1];
			arguments[0] = logger;
			Array.Copy(extraArguments, 0, arguments, 1, extraArguments.Length);

			var provider = (IContentBindingProvider)Activator.CreateInstance(providerType, arguments)!;
			providers.Add(provider);
			providerLogs[provider.Kind] = sink;
		}
	}

	private static ModItemDefinition FilledItem(string id) => new()
	{
		Id = id,
		DisplayName = "Authored item",
		Description = "Composed default description",
		Weight = 4f
	};

	private static ModRecipeDefinition FilledRecipe(string id) => new()
	{
		Id = id,
		ResultItemId = "log",
		ResultAmount = 1,
		Ingredients = [new ModRecipeIngredient { ItemId = "log" }]
	};

	private static ModLiquidDefinition FilledLiquid(string id) => new()
	{
		Id = id,
		DisplayName = "Authored liquid",
		ValuePerLiter = 1.5f
	};

	private static ModLiquidTileDefinition FilledLiquidTile(string id) => new()
	{
		Id = id,
		DisplayName = "Authored liquid tile",
		LiquidId = "water",
		Buoyancy = 0.5f
	};

	private static ModTileDefinition FilledTile(string id) => new()
	{
		Id = id,
		DisplayName = "Authored tile",
		TemplateTileIndex = 1,
		Health = 100f
	};

	private static ModBuildingDefinition FilledBuilding(string id) => new()
	{
		Id = id,
		DisplayName = "Authored building",
		TemplateId = "wall",
		Health = 100f
	};

	private static ModStructureDefinition FilledStructure(string id) => new()
	{
		Id = id,
		DisplayName = "Authored structure",
		Width = 1,
		Height = 1,
		Rows = ["#"],
		VanillaBlocks = new Dictionary<string, int> { ["#"] = 1 }
	};

	private static ModStatusDefinition FilledStatus(string id) => new()
	{
		Id = id,
		DisplayName = "Authored status",
		Scope = ModStatusScope.Limb,
		ShowPerLimbMoodles = true,
		MoodleId = "moodle.authored"
	};

	private static ModMoodleDefinition FilledMoodle(string id) => new()
	{
		Id = id,
		DisplayName = "Authored moodle",
		IconId = "icons.authored",
		HoldSeconds = 0.5f
	};

	private sealed record BindRun(
		IReadOnlyList<(LogLevel Level, string Message)> BinderLog,
		Dictionary<string, List<(LogLevel Level, string Message)>> ProviderLogs)
	{
		/// <summary>
		/// The provider accepted the definition, and no provider threw on the way:
		/// an exception is the binder's error entry and the one outcome this seam
		/// exists to remove.
		/// </summary>
		internal void AssertBound(string kind, string id)
		{
			Assert.DoesNotContain(BinderLog, entry => entry.Level == LogLevel.Error);
			Assert.DoesNotContain(BinderLog, entry => entry.Message.Contains(id, StringComparison.Ordinal));
			Assert.Contains(ProviderLogs[kind], entry =>
				entry.Level == LogLevel.Information && entry.Message.Contains($"accepted {ModId}/{id}", StringComparison.Ordinal));
		}
	}

	private sealed class EntriesControl(params ModContentRegistration[] entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}

	private sealed class ModsControl : IModsControl
	{
		public IReadOnlyList<ModManifest> CurrentModManifests { get; } =
			[new ModManifest(ModId, "Authored definitions", "1.0.0", NetworkMode.Synchronized, null)];

		public bool IsDiscoveryComplete => true;

		public void FireModMessageReceived(ulong sender, ModMessageMsg msg)
		{
		}

		public void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg)
		{
		}

		public void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg)
		{
		}
	}

	/// <summary>
	/// The providers are GameAdapter types, so their <c>ILogger&lt;T&gt;</c> has to be
	/// built at runtime; this sink is what the cases read back.
	/// </summary>
	public sealed class ProviderLogSink<T>(List<(LogLevel Level, string Message)> entries) : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			entries.Add((logLevel, formatter(state, exception)));
	}

	/// <summary>
	/// The mod-authored item: it implements the kind interface itself, computes
	/// <see cref="DisplayName"/> and <see cref="Weight"/> from its own fields, and
	/// hands every other member back from the filled framework default it composes.
	/// </summary>
	private sealed class ModAuthoredItem(ModItemDefinition defaults, string nameSuffix, float extraWeight)
		: IModItemDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public string DisplayName => defaults.DisplayName + nameSuffix;

		public float Weight => defaults.Weight + extraWeight;

		public string Description => defaults.Description;

		public string Category => defaults.Category;

		public int Value => defaults.Value;

		public bool Usable => defaults.Usable;

		public bool UsableWithLmb => defaults.UsableWithLmb;

		public bool Wearable => defaults.Wearable;

		public bool DestroyAtZeroCondition => defaults.DestroyAtZeroCondition;

		public string Tags => defaults.Tags;

		public int SpawnFrequency => defaults.SpawnFrequency;

		public string TemplateId => defaults.TemplateId;

		public List<string> SpawnComponents => defaults.SpawnComponents;

		public Dictionary<string, string> CustomData => defaults.CustomData;

		public float? WorldSpawnPerChunk => defaults.WorldSpawnPerChunk;

		public ModItemDropSource? DropSources => defaults.DropSources;

		public ModItemContainer? Container => defaults.Container;

		public ModItemBattery? Battery => defaults.Battery;

		public ModItemLight? Light => defaults.Light;

		public ModItemTool? Tool => defaults.Tool;

		public ModItemGun? Gun => defaults.Gun;

		public float DecayMinutes => defaults.DecayMinutes;

		public ModItemVisual? Visual => defaults.Visual;

		public List<ModCraftingQuality> Qualities => defaults.Qualities;
	}

	/// <summary>The mod-authored recipe: it computes <see cref="ResultAmount"/> from its own batch size.</summary>
	private sealed class ModAuthoredRecipe(ModRecipeDefinition defaults, int batchSize) : IModRecipeDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public int ResultAmount => defaults.ResultAmount * batchSize;

		public string ResultItemId => defaults.ResultItemId;

		public bool ResultIsLiquid => defaults.ResultIsLiquid;

		public float ResultCondition => defaults.ResultCondition;

		public bool DontDrainResultLiquid => defaults.DontDrainResultLiquid;

		public int Intelligence => defaults.Intelligence;

		public string Category => defaults.Category;

		public bool IsRepair => defaults.IsRepair;

		public List<ModRecipeIngredient> Ingredients => defaults.Ingredients;
	}

	/// <summary>The mod-authored liquid: it computes <see cref="ValuePerLiter"/> from its own purity factor.</summary>
	private sealed class ModAuthoredLiquid(ModLiquidDefinition defaults, float purity) : IModLiquidDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public float ValuePerLiter => defaults.ValuePerLiter * purity;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public float ColorR => defaults.ColorR;

		public float ColorG => defaults.ColorG;

		public float ColorB => defaults.ColorB;

		public float ColorA => defaults.ColorA;

		public bool HealthUsable => defaults.HealthUsable;

		public bool Injectable => defaults.Injectable;

		public float InjectionSickness => defaults.InjectionSickness;

		public bool LocaleFromItem => defaults.LocaleFromItem;

		public List<ModCraftingQuality> Qualities => defaults.Qualities;
	}

	/// <summary>The mod-authored liquid tile: it computes <see cref="Buoyancy"/> from its own scale factor.</summary>
	private sealed class ModAuthoredLiquidTile(ModLiquidTileDefinition defaults, float buoyancyScale) : IModLiquidTileDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public float Buoyancy => defaults.Buoyancy * buoyancyScale;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public string LiquidId => defaults.LiquidId;

		public string FillLiquidId => defaults.FillLiquidId;

		public float Drag => defaults.Drag;

		public bool PushBodies => defaults.PushBodies;

		public float WetnessPerSecond => defaults.WetnessPerSecond;

		public float TemperaturePerSecond => defaults.TemperaturePerSecond;

		public float SicknessPerSecond => defaults.SicknessPerSecond;

		public float DirtynessPerSecond => defaults.DirtynessPerSecond;

		public float DisinfectPerSecond => defaults.DisinfectPerSecond;

		public float SlipPerSecond => defaults.SlipPerSecond;

		public float RagdollBarDrainPerSecond => defaults.RagdollBarDrainPerSecond;

		public ModLiquidTileVisualMode VisualMode => defaults.VisualMode;

		public int VisualLiquidByte => defaults.VisualLiquidByte;

		public float TintR => defaults.TintR;

		public float TintG => defaults.TintG;

		public float TintB => defaults.TintB;

		public float TintA => defaults.TintA;

		public string VisualAssetPath => defaults.VisualAssetPath;

		public float SpawnAmount => defaults.SpawnAmount;

		public int SpawnLayers => defaults.SpawnLayers;

		public int MaxFloodFill => defaults.MaxFloodFill;

		public bool ConsumeOnDrink => defaults.ConsumeOnDrink;

		public bool ConsumeOnFill => defaults.ConsumeOnFill;

		public Dictionary<string, string> CustomData => defaults.CustomData;
	}

	/// <summary>The mod-authored tile: it computes <see cref="Health"/> from its own hardness factor.</summary>
	private sealed class ModAuthoredTile(ModTileDefinition defaults, float hardness) : IModTileDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public float Health => defaults.Health * hardness;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public int? TemplateTileIndex => defaults.TemplateTileIndex;

		public string SpritePath => defaults.SpritePath;

		public string TileName => defaults.TileName;

		public string HitSound => defaults.HitSound;

		public string StepSound => defaults.StepSound;

		public ModTileSleepQuality SleepQuality => defaults.SleepQuality;

		public bool NoVariation => defaults.NoVariation;

		public bool Metallic => defaults.Metallic;

		public float Toxicity => defaults.Toxicity;

		public bool Slippery => defaults.Slippery;

		public float ColorR => defaults.ColorR;

		public float ColorG => defaults.ColorG;

		public float ColorB => defaults.ColorB;

		public float ColorA => defaults.ColorA;

		public ModTileColliderType ColliderType => defaults.ColliderType;

		public Dictionary<string, string> CustomData => defaults.CustomData;

		public float SpawnAmount => defaults.SpawnAmount;

		public int SpawnLayers => defaults.SpawnLayers;

		public ModTileGenerationStyle GenerationStyle => defaults.GenerationStyle;

		public List<ModTileDrop> Drops => defaults.Drops;
	}

	/// <summary>The mod-authored building: it computes <see cref="Health"/> from its own reinforcement factor.</summary>
	private sealed class ModAuthoredBuilding(ModBuildingDefinition defaults, float reinforcement) : IModBuildingDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public float? Health => defaults.Health is { } health ? health * reinforcement : null;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public string TemplateId => defaults.TemplateId;

		public bool? RequireGround => defaults.RequireGround;

		public bool? Animal => defaults.Animal;

		public bool? CantHit => defaults.CantHit;

		public bool? Metallic => defaults.Metallic;

		public bool? IgnoreBodyOptimize => defaults.IgnoreBodyOptimize;

		public float? DropChanceMultiplier => defaults.DropChanceMultiplier;

		public int? GuaranteedDropAmount => defaults.GuaranteedDropAmount;

		public List<string> SpawnComponents => defaults.SpawnComponents;

		public Dictionary<string, string> CustomData => defaults.CustomData;

		public List<ModBuildingDrop> DropOnDestroy => defaults.DropOnDestroy;

		public List<ModBuildingDrop> AlwaysDrop => defaults.AlwaysDrop;

		public List<string> ItemCategoriesToAdd => defaults.ItemCategoriesToAdd;

		public float? SpawnMinPerChunk => defaults.SpawnMinPerChunk;

		public float? SpawnMaxPerChunk => defaults.SpawnMaxPerChunk;

		public int SpawnLayers => defaults.SpawnLayers;

		public ModBuildingGenerationStyle GenerationStyle => defaults.GenerationStyle;

		public ModBuildingPlacement Placement => defaults.Placement;

		public bool SpawnInGround => defaults.SpawnInGround;

		public float? SurfaceOffset => defaults.SurfaceOffset;

		public bool? RandomFlip => defaults.RandomFlip;
	}

	/// <summary>The mod-authored structure: it computes <see cref="DisplayName"/> from its own label suffix.</summary>
	private sealed class ModAuthoredStructure(ModStructureDefinition defaults, string labelSuffix) : IModStructureDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public string DisplayName => defaults.DisplayName + labelSuffix;

		public string Description => defaults.Description;

		public int Width => defaults.Width;

		public int Height => defaults.Height;

		public List<string> Rows => defaults.Rows;

		public Dictionary<string, int> VanillaBlocks => defaults.VanillaBlocks;

		public Dictionary<string, string> TileIds => defaults.TileIds;

		public List<int> SpawnCounts => defaults.SpawnCounts;

		public Dictionary<string, string> CustomData => defaults.CustomData;
	}

	/// <summary>The mod-authored status: it computes <see cref="MoodleId"/> from its own moodle suffix.</summary>
	private sealed class ModAuthoredStatus(ModStatusDefinition defaults, string moodleSuffix) : IModStatusDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public string MoodleId => defaults.MoodleId + moodleSuffix;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public ModStatusScope Scope => defaults.Scope;

		public bool SaveEnabled => defaults.SaveEnabled;

		public Dictionary<string, string> CustomData => defaults.CustomData;

		public bool ShowPerLimbMoodles => defaults.ShowPerLimbMoodles;

		public List<ModLimbMoodleBinding> LimbMoodles => defaults.LimbMoodles;
	}

	/// <summary>The mod-authored moodle: it computes <see cref="HoldSeconds"/> from its own hold factor.</summary>
	private sealed class ModAuthoredMoodle(ModMoodleDefinition defaults, float holdScale) : IModMoodleDefinition
	{
		public string Id => defaults.Id;

		public string Kind => defaults.Kind;

		public int SchemaVersion => defaults.SchemaVersion;

		public float HoldSeconds => defaults.HoldSeconds * holdScale;

		public string DisplayName => defaults.DisplayName;

		public string Description => defaults.Description;

		public int Intensity => defaults.Intensity;

		public string IconId => defaults.IconId;

		public bool Critical => defaults.Critical;

		public bool ChippedOnly => defaults.ChippedOnly;

		public bool Important => defaults.Important;

		public Dictionary<string, string> CustomData => defaults.CustomData;

		public ModMoodleAnimation? IconAnimation => defaults.IconAnimation;

		public string LimbDisplayNameFormat => defaults.LimbDisplayNameFormat;

		public string LimbDescriptionFormat => defaults.LimbDescriptionFormat;
	}
}
