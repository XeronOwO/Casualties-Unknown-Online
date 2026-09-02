using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The runtime mod-data test mod. Synchronized with SendNetworkMessage declared,
/// so it can declare shared slots and the standard handshake admits matching
/// copies; the data tests drive <see cref="Context"/>. All state is instance
/// state (the xunit runner parallelizes test classes, and a shared static would
/// race them).
/// </summary>
[CuoMod("test.data", "Test Runtime Data", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage)]
public sealed class TestDataMod : ICuoMod
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
