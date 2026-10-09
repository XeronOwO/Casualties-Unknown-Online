using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The <see cref="ModContentAttribute"/> scan as a judge over a type list: which
/// mod owns a declaration, which contract fixes its kind, what the framework
/// refuses and what it leaves alone.
///
/// The fixtures live inside a PRIVATE fixture mod, which the production registry
/// never discovers — so the refusal family is driven here without every TestNode's
/// own discovery logging it — except for the two cases that only exist OUTSIDE a
/// mod (an ownerless declaration and a mod class that declares content on
/// itself), which the test assembly's census reports once per discovery, exactly
/// as the malformed <c>[CuoMod]</c> fixtures in <c>ModDiscoveryTests</c> are
/// reported. That report is itself asserted below.
/// </summary>
public class ModContentDeclarationScannerTests
{
	private static readonly Assembly TestAssembly = typeof(ModContentDeclarationScannerTests).Assembly;

	private static RecordingLogger<ModContentDeclarationScanner> CreateLog() => new();

	// ---- Ownership: which mod a declaration belongs to ----

	[Fact]
	public void Plan_OwnsADeclarationNestedInsideItsMod()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(
			TestAssembly, [typeof(ScanFixtureMod), typeof(TestEchoMod), typeof(ScanFixtureMod.DeclaredItem)], log);

		Assert.Contains(typeof(ScanFixtureMod.DeclaredItem), plan[typeof(ScanFixtureMod)]);
		Assert.False(plan.ContainsKey(typeof(TestEchoMod)));
		Assert.Empty(log.Entries);
	}

	[Fact]
	public void Plan_OwnsATopLevelDeclaration_WhenTheAssemblyDeclaresOneMod()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(TestAssembly, [typeof(TestEchoMod), typeof(OwnerlessItem)], log);

		Assert.Contains(typeof(OwnerlessItem), plan[typeof(TestEchoMod)]);
		Assert.Empty(log.Entries);
	}

	[Fact]
	public void Plan_RefusesATopLevelDeclaration_WhenSeveralModsShareTheAssembly()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(
			TestAssembly, [typeof(TestEchoMod), typeof(TestDataMod), typeof(OwnerlessItem)], log);

		Assert.Empty(plan);
		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains(nameof(OwnerlessItem), warning.Message, StringComparison.Ordinal);
		Assert.Contains("declares 2 mods", warning.Message, StringComparison.Ordinal);
		Assert.Contains("no single mod owns it", warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_ReportsAnAssemblyThatDeclaresContentButNoMod()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(TestAssembly, [typeof(ScanFixtureMod.DeclaredItem)], log);

		Assert.Empty(plan);
		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains("no [CuoMod] mod", warning.Message, StringComparison.Ordinal);
		Assert.Contains("nothing registers them", warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_RefusesAModClassThatDeclaresContentOnItself()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(
			TestAssembly, [typeof(BothAttributesMod), typeof(TestEchoMod)], log);

		Assert.False(plan.ContainsKey(typeof(BothAttributesMod)));
		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains(nameof(BothAttributesMod), warning.Message, StringComparison.Ordinal);
		Assert.Contains("the content half is refused", warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_RefusesDeclarationsOwnedByAModDiscoveryNeverLoads()
	{
		var log = CreateLog();

		var plan = ModContentDeclarationScanner.Plan(
			TestAssembly, [typeof(UnloadableMod), typeof(OwnerlessItem)], log);

		// The assembly's only declared mod cannot be loaded (private and abstract),
		// so the declaration it owns would otherwise be dropped in silence: the
		// registry's candidate filter never even reaches a skip line for that class.
		Assert.Empty(plan);
		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains(nameof(UnloadableMod), warning.Message, StringComparison.Ordinal);
		Assert.Contains("discovery never loads", warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Census_ReportsEachAssemblyLevelRefusalOnce()
	{
		var log = CreateLog();
		var scanner = new ModContentDeclarationScanner(log);

		scanner.Census([TestAssembly]);
		scanner.Census([TestAssembly]);

		Assert.Single(log.Entries, entry => entry.Message.Contains(nameof(OwnerlessItem), StringComparison.Ordinal));
		Assert.Single(log.Entries, entry => entry.Message.Contains(nameof(BothAttributesMod), StringComparison.Ordinal));
	}

	// ---- Registration: what the scan hands to the registry ----

	[Fact]
	public void Register_RegistersTheModsOwnClasses_AndNothingItRefused()
	{
		var log = CreateLog();
		var scanner = new ModContentDeclarationScanner(log);
		var content = new RecordingContent();

		var registered = scanner.Register(typeof(ScanFixtureMod), content);

		Assert.Equal(2, registered);
		Assert.Equal(2, content.Count);

		// The declaration the mod wrote is what the registry holds — not a
		// framework DTO built from it — and its computed member is the mod's.
		var item = Assert.IsType<ScanFixtureMod.DeclaredItem>(
			content.Definitions.Single(definition => definition.Id == ScanFixtureMod.ItemId));
		Assert.Equal(ModContentKind.Item, item.Kind);
		Assert.Equal(1, item.SchemaVersion);
		Assert.Equal("Scan Item (schema 1)", item.DisplayName);
		Assert.IsType<ScanFixtureMod.DeclaredRecipe>(
			content.Definitions.Single(definition => definition.Id == ScanFixtureMod.RecipeId));
	}

	[Fact]
	public void Register_RefusesTwoKindContracts_ByName()
	{
		var log = CreateLog();
		var content = new RecordingContent();

		_ = new ModContentDeclarationScanner(log).Register(typeof(ScanFixtureMod), content);

		Assert.False(content.IsRegistered("scan.two.kinds"));
		var warning = Assert.Single(log.Entries, entry => entry.Message.Contains(nameof(ScanFixtureMod.TwoKindsItem), StringComparison.Ordinal));
		Assert.Contains("implements 2 kind contracts", warning.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(IModItemDefinition), warning.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(IModRecipeDefinition), warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Register_RefusesAKindThatDisagreesWithItsContract()
	{
		var log = CreateLog();
		var content = new RecordingContent();

		_ = new ModContentDeclarationScanner(log).Register(typeof(ScanFixtureMod), content);

		Assert.False(content.IsRegistered("scan.wrong.kind"));
		Assert.Contains(log.Entries, entry =>
			entry.Message.Contains(nameof(ScanFixtureMod.WrongKindItem), StringComparison.Ordinal)
			&& entry.Message.Contains($"reports kind '{ModContentKind.Recipe}'", StringComparison.Ordinal)
			&& entry.Message.Contains($"fixes kind '{ModContentKind.Item}'", StringComparison.Ordinal));
	}

	[Fact]
	public void Register_RefusesAThrowingMember_ByName_AndKeepsItsSiblings()
	{
		var log = CreateLog();
		var content = new RecordingContent();

		var registered = new ModContentDeclarationScanner(log).Register(typeof(ScanFixtureMod), content);

		Assert.Equal(2, registered);
		Assert.False(content.IsRegistered("scan.throwing"));
		Assert.Contains(log.Entries, entry =>
			entry.Level == LogLevel.Warning
			&& entry.Message.Contains($"{nameof(ScanFixtureMod.ThrowingItem)}.Weight", StringComparison.Ordinal)
			&& entry.Message.Contains("the other declarations still bind", StringComparison.Ordinal));
	}

	[Theory]
	[MemberData(nameof(RefusedDeclarations))]
	public void Register_RefusesADeclarationItCannotDiscover(Type declaration, string reason)
	{
		var log = CreateLog();
		var content = new RecordingContent();

		_ = new ModContentDeclarationScanner(log).Register(typeof(ScanFixtureMod), content);

		Assert.DoesNotContain(content.Definitions, definition => definition.GetType() == declaration);
		Assert.Contains(log.Entries, entry =>
			entry.Level == LogLevel.Warning
			&& entry.Message.Contains(declaration.FullName!, StringComparison.Ordinal)
			&& entry.Message.Contains(reason, StringComparison.Ordinal));
	}

	public static IEnumerable<object[]> RefusedDeclarations =>
	[
		[typeof(ScanFixtureMod.NotPublicItem), "is not public"],
		[typeof(ScanFixtureMod.AbstractItem), "is abstract"],
		[typeof(ScanFixtureMod.NoDefaultConstructorItem), "has no public parameterless constructor"],
		[typeof(ScanFixtureMod.OpenGenericItem<>), "is an open generic type"],
		[typeof(ScanFixtureMod.NoKindItem), "implements no kind contract"],
		[typeof(ScanFixtureMod.ThrowingConstructorItem), "threw while it was constructed"],
	];

	// ---- Fixtures: the mod whose declarations the cases drive ----

	/// <summary>
	/// The scan's own fixture mod: PUBLIC, because the ownership rule only sees a mod
	/// discovery could consider — and with the default <see cref="NetworkMode.Unspecified"/>,
	/// which the registry refuses before anything else, so no TestNode ever loads it and
	/// its declarations are driven only by the cases that name it. That is what keeps the
	/// refusal family below out of every other node's log.
	/// </summary>
	[CuoMod("test.scan.fixture", "Scan Fixture", "1.0.0",
		Permissions = ModPermission.RegisterContent)]
	public sealed class ScanFixtureMod : ICuoMod
	{
		internal const string ItemId = "scan.item";

		internal const string RecipeId = "scan.recipe";

		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}

		/// <summary>The declaration the scan registers: one kind contract, every member its own.</summary>
		[ModContent]
		public sealed class DeclaredItem : ItemStub
		{
			public override string Id => ItemId;

			public override string DisplayName => $"Scan Item (schema {SchemaVersion})";
		}

		/// <summary>The declaration the scan registers beside the item.</summary>
		[ModContent]
		public sealed class DeclaredRecipe : IModRecipeDefinition
		{
			public string Id => RecipeId;

			public string Kind => ModContentKind.Recipe;

			public int SchemaVersion => 1;

			public string ResultItemId => ItemId;

			public bool ResultIsLiquid => false;

			public int ResultAmount => 1;

			public float ResultCondition => 1f;

			public bool DontDrainResultLiquid => false;

			public int Intelligence => 0;

			public string Category => "nospawn";

			public bool IsRepair => false;

			public List<ModRecipeIngredient> Ingredients => [];
		}

		/// <summary>Refused: the kind is never guessed, and this one reaches two contracts.</summary>
		[ModContent]
		public sealed class TwoKindsItem : ItemStub, IModRecipeDefinition
		{
			public override string Id => "scan.two.kinds";

			public string ResultItemId => "cloth";

			public bool ResultIsLiquid => false;

			public int ResultAmount => 1;

			public float ResultCondition => 1f;

			public bool DontDrainResultLiquid => false;

			public int Intelligence => 0;

			public bool IsRepair => false;

			public List<ModRecipeIngredient> Ingredients => [];
		}

		/// <summary>Refused: the contract fixes the kind, and this member disagrees with it.</summary>
		[ModContent]
		public sealed class WrongKindItem : ItemStub
		{
			public override string Id => "scan.wrong.kind";

			public override string Kind => ModContentKind.Recipe;
		}

		/// <summary>Refused by name: reading this declaration's members throws.</summary>
		[ModContent]
		public sealed class ThrowingItem : ItemStub
		{
			public override string Id => "scan.throwing";

			public override float Weight => throw new InvalidOperationException("this declaration cannot compute its weight");
		}

		/// <summary>Refused by name: this declaration cannot even be CONSTRUCTED, and the guard has to keep it from the mod's own load.</summary>
		[ModContent]
		public sealed class ThrowingConstructorItem : ItemStub
		{
			public ThrowingConstructorItem()
			{
				throw new InvalidOperationException("this declaration cannot be constructed");
			}

			public override string Id => "scan.throwing.constructor";
		}

		/// <summary>Refused: the address members alone are not a kind — a kind of one's own is the code path's, not the scan's.</summary>
		[ModContent]
		public sealed class NoKindItem : IModContentDefinition
		{
			public string Id => "scan.no.kind";

			public string Kind => "scan";

			public int SchemaVersion => 1;
		}

		/// <summary>Refused: the framework instantiates a declaration at discovery, and a type that is not public cannot be one.</summary>
		[ModContent]
		internal sealed class NotPublicItem : ItemStub
		{
			public override string Id => "scan.not.public";
		}

		/// <summary>Refused: an abstract declaration cannot be instantiated.</summary>
		[ModContent]
		public abstract class AbstractItem : ItemStub
		{
			public override string Id => "scan.abstract";
		}

		/// <summary>Refused: no public parameterless constructor to instantiate it with.</summary>
		[ModContent]
		public sealed class NoDefaultConstructorItem(string id) : ItemStub
		{
			public override string Id { get; } = id;
		}

		/// <summary>Refused: an open generic type has no instance to read.</summary>
		[ModContent]
		public sealed class OpenGenericItem<T> : ItemStub
		{
			public override string Id => "scan.open.generic";
		}
	}

	/// <summary>
	/// A mod declaration discovery never loads, and carries no content of its own: the
	/// case it exists for is a declaration OWNED by such a mod, which the scan has to
	/// refuse by name because no other line reports it — the registry's candidate
	/// filter drops this class before it can skip it with a message.
	/// </summary>
	[CuoMod("test.scan.unloadable", "Scan Unloadable", "1.0.0")]
	private abstract class UnloadableMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	/// <summary>
	/// The item shape a mod author writes when the values are its own: one member
	/// per contract member, each overridable so a case varies the single member it
	/// is about. It carries no attribute and is no mod, so the scan never sees it.
	/// </summary>
	public abstract class ItemStub : IModItemDefinition
	{
		public virtual string Id => "scan.stub";

		public virtual string Kind => ModContentKind.Item;

		public virtual int SchemaVersion => 1;

		public virtual string DisplayName => "";

		public virtual string Description => "";

		public virtual string Category => "nospawn";

		public virtual float Weight => 0f;

		public virtual int Value => 0;

		public virtual bool Usable => false;

		public virtual bool UsableWithLmb => false;

		public virtual bool Wearable => false;

		public virtual bool DestroyAtZeroCondition => false;

		public virtual string Tags => "";

		public virtual int SpawnFrequency => 1;

		public virtual string TemplateId => "";

		public virtual List<string> SpawnComponents => [];

		public virtual Dictionary<string, string> CustomData => [];

		public virtual float? WorldSpawnPerChunk => null;

		public virtual ModItemDropSource? DropSources => null;

		public virtual ModItemContainer? Container => null;

		public virtual ModItemBattery? Battery => null;

		public virtual ModItemLight? Light => null;

		public virtual ModItemTool? Tool => null;

		public virtual ModItemGun? Gun => null;

		public virtual float DecayMinutes => 0f;

		public virtual ModItemVisual? Visual => null;

		public virtual List<ModCraftingQuality> Qualities => [];
	}

	/// <summary>
	/// Deliberately ownerless: nested in this test class rather than in a mod, while
	/// the test assembly declares many — so every discovery's census reports it once
	/// and nothing ever registers it. That report is the case
	/// <see cref="Plan_RefusesATopLevelDeclaration_WhenSeveralModsShareTheAssembly"/>
	/// and <see cref="Census_ReportsEachAssemblyLevelRefusalOnce"/> assert.
	/// </summary>
	[ModContent]
	private sealed class OwnerlessItem : ItemStub
	{
		public override string Id => "scan.ownerless";
	}

	/// <summary>
	/// A class that is a mod AND declares content on itself: the content half has no
	/// owner, because a mod's own instance is not a declaration the scan may build a
	/// second copy of. Reported once per discovery, like the ownerless fixture.
	/// </summary>
	[CuoMod("test.scan.both", "Scan Both", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	[ModContent]
	private sealed class BothAttributesMod : ItemStub, ICuoMod
	{
		public override string Id => "scan.both";

		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	/// <summary>The registry stand-in: what the scan asked it to take, in the order it asked.</summary>
	private sealed class RecordingContent : IModContent
	{
		internal List<IModContentDefinition> Registered { get; } = [];

		public bool CanRegister => true;

		public int Count => Registered.Count;

		public IReadOnlyCollection<IModContentDefinition> Definitions => Registered;

		public bool TryRegister(IModContentDefinition definition)
		{
			Registered.Add(definition);
			return true;
		}

		public bool TryUnregister(string id) => false;

		public bool IsRegistered(string id) => Registered.Any(definition => definition.Id == id);
	}
}
