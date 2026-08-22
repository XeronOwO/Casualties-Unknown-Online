using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod-state test mod (Phase 4 Mod API remainder). Synchronized with
/// WriteGameState declared, so every TestNode discovers it and the standard
/// handshake admits matching copies; the state tests drive <see cref="Context"/>.
/// All state is instance state: the xunit runner parallelizes test classes, and
/// a shared static would race them.
/// </summary>
[CuoMod("test.state", "Test State", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.WriteGameState)]
public sealed class TestStateMod : ICuoMod
{
	public IModContext? Context { get; private set; }

	public void Bind(IModContext context) => Context = context;

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
