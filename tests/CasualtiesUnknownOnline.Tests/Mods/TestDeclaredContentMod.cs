using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The attribute-declared content test mod: every declaration is a class of its
/// own next to the mod, and <see cref="Bind"/> registers NOTHING — the framework
/// discovers the declarations through <see cref="ModContentAttribute"/>, decides
/// each one's kind from the contract it implements, instantiates it and hands it
/// to the same registry the code path uses.
///
/// One declaration throws from a member getter on purpose. It is refused BY NAME
/// while its siblings still bind, which is the per-declaration isolation the code
/// path cannot give: its per-entry catch wraps the binder, not a provider's own
/// loop. The refusal is logged once per process, and every TestNode's census also
/// reports it, which is the behaviour these fixtures lock.
/// </summary>
[CuoMod("test.declared", "Test Declared Content", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.RegisterContent,
	Namespace = "testdeclared")]
public sealed class TestDeclaredContentMod : ICuoMod
{
	/// <summary>The item the attribute path registers — asserted to be THIS class, not a framework DTO.</summary>
	public const string ItemId = "declared.item";

	/// <summary>The recipe the attribute path registers.</summary>
	public const string RecipeId = "declared.recipe";

	/// <summary>The declaration refused for throwing while its members were read.</summary>
	public const string ThrowingItemId = "declared.throwing";

	public IModContext? Context { get; private set; }

	/// <summary>True when this mod's own <see cref="Bind"/> already saw the declarations the scan registered — the ordering the lifecycle promises, and the proof that the declared half is not a fallback for the code half.</summary>
	public bool DeclarationsVisibleAtBind { get; private set; }

	public void Bind(IModContext context)
	{
		Context = context;
		DeclarationsVisibleAtBind = context.Content.IsRegistered(ItemId)
			&& context.Content.IsRegistered(RecipeId)
			&& !context.Content.IsRegistered(ThrowingItemId);
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

	/// <summary>A mod-authored item: a class implementing the contract, with the values it cares about and the shape's defaults for the rest.</summary>
	[ModContent]
	public sealed class DeclaredItem : IModItemDefinition
	{
		public string Id => ItemId;

		public string Kind => ModContentKind.Item;

		public int SchemaVersion => 1;

		/// <summary>Computed from another member rather than stored, so the value proves the framework reads the mod's own implementation.</summary>
		public string DisplayName => $"Declared Item (schema {SchemaVersion})";

		public string Description => "Declared by attribute, registered with no call.";

		public string Category => "nospawn";

		public float Weight => 1.5f;

		public int Value => 7;

		public bool Usable => false;

		public bool UsableWithLmb => false;

		public IModItemWearable? Wearable => null;

		public bool DestroyAtZeroCondition => false;

		public string Tags => "";

		public int SpawnFrequency => 1;

		public string TemplateId => "";

		public List<string> SpawnComponents => [];

		public Dictionary<string, string> CustomData => [];

		public float? WorldSpawnPerChunk => null;

		public ModItemDropSource? DropSources => null;

		public IModItemContainer? Container => null;

		public IModItemBattery? Battery => null;

		public IModItemLight? Light => null;

		public IModItemTool? Tool => null;

		public IModItemGun? Gun => null;

		public float DecayMinutes => 0f;

		public IModItemVisual? Visual => null;

		public List<IModCraftingQuality> Qualities => [];
	}

	/// <summary>A mod-authored recipe beside the item, same path.</summary>
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

		public List<IModRecipeIngredient> Ingredients => [new ModRecipeIngredient { ItemId = "cloth" }];
	}

	/// <summary>The declaration the scan refuses by name: reading its members throws, and its sibling above still binds.</summary>
	[ModContent]
	public sealed class ThrowingItem : IModItemDefinition
	{
		public string Id => ThrowingItemId;

		public string Kind => ModContentKind.Item;

		public int SchemaVersion => 1;

		public string DisplayName => "Throwing Item";

		public string Description => "";

		public string Category => "nospawn";

		/// <summary>The refusal: a member that computes its value and fails.</summary>
		public float Weight => throw new InvalidOperationException("this declaration cannot compute its weight");

		public int Value => 0;

		public bool Usable => false;

		public bool UsableWithLmb => false;

		public IModItemWearable? Wearable => null;

		public bool DestroyAtZeroCondition => false;

		public string Tags => "";

		public int SpawnFrequency => 1;

		public string TemplateId => "";

		public List<string> SpawnComponents => [];

		public Dictionary<string, string> CustomData => [];

		public float? WorldSpawnPerChunk => null;

		public ModItemDropSource? DropSources => null;

		public IModItemContainer? Container => null;

		public IModItemBattery? Battery => null;

		public IModItemLight? Light => null;

		public IModItemTool? Tool => null;

		public IModItemGun? Gun => null;

		public float DecayMinutes => 0f;

		public IModItemVisual? Visual => null;

		public List<IModCraftingQuality> Qualities => [];
	}
}
