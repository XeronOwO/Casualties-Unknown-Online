using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The other half of the null rule <c>ModPayloadNullCollectionTests</c> pins on
/// the contracts: the REAL content binder driving the REAL GameAdapter content
/// providers, with the collection members each definition can reach explicitly
/// null — its own, and the ones on the behaviour DTOs a case authors (a nested
/// DTO the case omits is simply absent). The definition either binds, or the
/// provider refuses it with its own reason in the log — never with the binder's
/// logged exception, which would leave the mod without the content it declared.
/// The same roster answers the typed refusal: a definition that claims a
/// provider's kind without implementing that kind's contract is refused by every
/// one of the nine. The test project never compile-references GameAdapter, so the provider
/// roster and its loggers are built reflectively.
/// </summary>
[Trait("Category", "Integration")]
public class ModContentNullCollectionBindingTests
{
	private const string ModId = "mod.nulls";

	private const string ItemProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider";
	private const string RecipeProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterRecipeContentProvider";
	private const string LiquidProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidContentProvider";
	private const string LiquidTileProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidTileContentProvider";
	private const string BuildingProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterBuildingContentProvider";
	private const string TileProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterTileContentProvider";
	private const string StatusProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider";
	private const string MoodleProvider = "CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterMoodleContentProvider";

	[Fact]
	public void Item_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModItemDefinition
		{
			Id = "null.item",
			DisplayName = "Null-tolerated shard",
			SpawnComponents = null!,
			CustomData = null!,
			Qualities = null!,
			Visual = new ModItemVisual { MultiWornSprites = null! }
		}));

		run.AssertBound(ModContentKind.Item, "null.item");
	}

	[Fact]
	public void Item_AnimationWithoutFrames_IsRefusedByItsOwnRule()
	{
		var run = Run(Registration(new ModItemDefinition
		{
			Id = "frameless.item",
			DisplayName = "Frameless shard",
			Visual = new ModItemVisual
			{
				BaseSpriteAnimation = new ModItemSpriteAnimation { FramePaths = null!, FramesPerSecond = 12f }
			}
		}));

		// A sprite animation with no frames is genuinely required content: the
		// refusal names the reason instead of skipping the definition silently.
		run.AssertRefusedByProvider(ModContentKind.Item, "frameless.item", "invalid base sprite animation");
	}

	[Fact]
	public void Recipe_WithoutIngredients_IsRefusedByItsOwnRule()
	{
		var run = Run(Registration(new ModRecipeDefinition
		{
			Id = "empty.recipe",
			ResultItemId = "log",
			Ingredients = null!
		}));

		run.AssertRefusedByProvider(ModContentKind.Recipe, "empty.recipe", "has no ingredients");
	}

	[Fact]
	public void Liquid_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModLiquidDefinition
		{
			Id = "null.liquid",
			DisplayName = "Null-tolerated liquid",
			Qualities = null!
		}));

		run.AssertBound(ModContentKind.Liquid, "null.liquid");
	}

	[Fact]
	public void LiquidTile_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModLiquidTileDefinition
		{
			Id = "null.liquidtile",
			LiquidId = "water",
			SpawnLayers = ModLiquidTileDefinition.AllSpawnLayers,
			MaxFloodFill = 128,
			CustomData = null!
		}));

		run.AssertBound(ModContentKind.LiquidTile, "null.liquidtile");
	}

	[Fact]
	public void Building_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModBuildingDefinition
		{
			Id = "null.building",
			TemplateId = "wall",
			DropOnDestroy = null!,
			AlwaysDrop = null!,
			ItemCategoriesToAdd = null!,
			SpawnComponents = null!,
			CustomData = null!
		}));

		run.AssertBound(ModContentKind.Building, "null.building");
	}

	[Fact]
	public void Tile_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModTileDefinition
		{
			Id = "null.tile",
			TemplateTileIndex = 1,
			Drops = null!,
			CustomData = null!
		}));

		run.AssertBound(ModContentKind.Tile, "null.tile");
	}

	[Fact]
	public void Structure_NullCollections_AreRefusedOrBoundByTheirOwnRule()
	{
		var run = Run(
			Registration(new ModStructureDefinition
			{
				Id = "rowless.structure",
				Width = 1,
				Height = 1,
				Rows = null!,
				VanillaBlocks = null!,
				TileIds = null!,
				SpawnCounts = null!,
				CustomData = null!
			}),
			Registration(new ModStructureDefinition
			{
				Id = "marker.structure",
				Width = 1,
				Height = 1,
				Rows = ["#"],
				TileIds = new Dictionary<string, string> { ["#"] = "custom_tile" },
				VanillaBlocks = null!,
				SpawnCounts = null!,
				CustomData = null!
			}));

		// An absent grid is a genuinely required member: the refusal reports the
		// row count it got. Every other collection being null is fine.
		run.AssertRefusedByProvider(ModContentKind.Structure, "rowless.structure", "declares 1 rows but supplied 0");
		run.AssertBound(ModContentKind.Structure, "marker.structure");
	}

	[Fact]
	public void Status_EveryCollectionNull_Binds()
	{
		var run = Run(Registration(new ModStatusDefinition
		{
			Id = "null.status",
			DisplayName = "Null-tolerated status",
			Scope = ModStatusScope.Limb,
			ShowPerLimbMoodles = true,
			CustomData = null!,
			LimbMoodles = null!
		}));

		run.AssertBound(ModContentKind.Status, "null.status");
	}

	[Fact]
	public void Moodle_NullCollections_AreRefusedOrBoundByTheirOwnRule()
	{
		var run = Run(
			Registration(new ModMoodleDefinition
			{
				Id = "null.moodle",
				IconId = "icons.lead",
				CustomData = null!
			}),
			Registration(new ModMoodleDefinition
			{
				Id = "frameless.moodle",
				IconId = "icons.lead",
				IconAnimation = new ModMoodleAnimation { FramePaths = null!, FramesPerSecond = 12f }
			}));

		run.AssertBound(ModContentKind.Moodle, "null.moodle");
		run.AssertRefusedByProvider(ModContentKind.Moodle, "frameless.moodle", "invalid icon animation");
	}

	/// <summary>
	/// The typed registry decodes no payload any more, so the refusal a provider
	/// can still reach is a definition that claims its kind without being the DTO
	/// it reads — a definition a mod wrote itself, or another DTO type. The cast is
	/// now the only thing between a claimed kind and a materialized entry, and the
	/// shape is identical nine times, so every provider is driven here: one that
	/// quietly accepted a foreign definition would bind content nothing can read.
	/// </summary>
	[Fact]
	public void EveryProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind()
	{
		var run = Run(
			Registration(new StubContentDefinition("wrong.item", ModContentKind.Item)),
			Registration(new StubContentDefinition("wrong.recipe", ModContentKind.Recipe)),
			Registration(new StubContentDefinition("wrong.liquid", ModContentKind.Liquid)),
			Registration(new StubContentDefinition("wrong.liquidtile", ModContentKind.LiquidTile)),
			Registration(new StubContentDefinition("wrong.tile", ModContentKind.Tile)),
			Registration(new StubContentDefinition("wrong.building", ModContentKind.Building)),
			Registration(new StubContentDefinition("wrong.structure", ModContentKind.Structure)),
			Registration(new StubContentDefinition("wrong.status", ModContentKind.Status)),
			Registration(new StubContentDefinition("wrong.moodle", ModContentKind.Moodle)));

		run.AssertRefusedByProvider(
			ModContentKind.Item, "wrong.item", "claims kind item but is a StubContentDefinition, not an IModItemDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Recipe, "wrong.recipe", "claims kind recipe but is a StubContentDefinition, not an IModRecipeDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Liquid, "wrong.liquid", "claims kind liquid but is a StubContentDefinition, not an IModLiquidDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.LiquidTile, "wrong.liquidtile",
			"claims kind liquidtile but is a StubContentDefinition, not an IModLiquidTileDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Tile, "wrong.tile", "claims kind tile but is a StubContentDefinition, not an IModTileDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Building, "wrong.building",
			"claims kind building but is a StubContentDefinition, not an IModBuildingDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Structure, "wrong.structure",
			"claims kind structure but is a StubContentDefinition, not an IModStructureDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Status, "wrong.status", "claims kind status but is a StubContentDefinition, not an IModStatusDefinition");
		run.AssertRefusedByProvider(
			ModContentKind.Moodle, "wrong.moodle", "claims kind moodle but is a StubContentDefinition, not an IModMoodleDefinition");
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
		AddProvider("CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStructureContentProvider", []);
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

	private sealed record BindRun(
		IReadOnlyList<(LogLevel Level, string Message)> BinderLog,
		Dictionary<string, List<(LogLevel Level, string Message)>> ProviderLogs)
	{
		/// <summary>
		/// The provider accepted the definition, and no provider threw on the way:
		/// an exception is the binder's error entry and the one outcome this rule
		/// exists to remove.
		/// </summary>
		internal void AssertBound(string kind, string id)
		{
			AssertNoError();
			Assert.DoesNotContain(BinderLog, entry => entry.Message.Contains(id, StringComparison.Ordinal));
			Assert.Contains(ProviderLogs[kind], entry =>
				entry.Level == LogLevel.Information && entry.Message.Contains($"accepted {ModId}/{id}", StringComparison.Ordinal));
		}

		/// <summary>
		/// The provider refused the definition with a reason of its own — a
		/// warning that names it — and the binder did not have to catch anything.
		/// </summary>
		internal void AssertRefusedByProvider(string kind, string id, string reason)
		{
			AssertNoError();
			Assert.Contains(BinderLog, entry =>
				entry.Level == LogLevel.Warning
				&& entry.Message.Contains($"refused {ModId}/{id}", StringComparison.Ordinal));
			Assert.Contains(ProviderLogs[kind], entry =>
				entry.Level == LogLevel.Warning && entry.Message.Contains(reason, StringComparison.Ordinal));
		}

		private void AssertNoError() =>
			Assert.DoesNotContain(BinderLog, entry => entry.Level == LogLevel.Error);
	}

	private sealed class EntriesControl(params ModContentRegistration[] entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}

	private sealed class ModsControl : IModsControl
	{
		public IReadOnlyList<ModManifest> CurrentModManifests { get; } =
			[new ModManifest(ModId, "Null toleration", "1.0.0", NetworkMode.Synchronized, null)];

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
}
