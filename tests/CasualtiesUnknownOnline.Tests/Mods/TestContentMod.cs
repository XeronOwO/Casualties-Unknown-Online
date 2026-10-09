using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod content test mod (Phase 4 Mod API remainder). Synchronized with
/// RegisterContent declared, so every TestNode discovers it and the standard
/// handshake admits matching copies; the content tests drive
/// <see cref="Context"/>. All state is instance state (the xunit runner
/// parallelizes test classes, and a shared static would race them).
/// </summary>
[CuoMod("test.content", "Test Content", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.RegisterContent,
	Namespace = "testcontent")]
public sealed class TestContentMod : ICuoMod
{
	public IModContext? Context { get; private set; }

	public bool Registered { get; private set; }

	public void Bind(IModContext context)
	{
		Context = context;
		Registered = context.Content.TryRegister(new ModItemDefinition
		{
			Id = "wooden.sword",
			SchemaVersion = 2,
			DisplayName = "Wooden Sword",
		});
		context.Content.TryRegister(new ModRecipeDefinition
		{
			Id = "healing.recipe",
			ResultItemId = "bandage",
			Ingredients = [new ModRecipeIngredient { ItemId = "cloth" }],
		});
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
