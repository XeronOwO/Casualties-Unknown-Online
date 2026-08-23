using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod entity-spawn test mod (Phase 4 Mod API remainder). Synchronized
/// with SpawnEntity declared, so every TestNode discovers it and the standard
/// handshake admits matching copies; the entity-spawn tests drive
/// <see cref="Context"/>. All state is instance state (the xunit runner
/// parallelizes test classes, and a shared static would race them).
/// </summary>
[CuoMod("test.entityspawn", "Test Entity Spawn", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SpawnEntity)]
public sealed class TestEntitySpawnMod : ICuoMod
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
