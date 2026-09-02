using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The local-only network-mode runtime-data test mod. It cannot declare shared
/// or host-authoritative slots; the data-scope policy tests assert that.
/// </summary>
[CuoMod("test.data.clientonly", "Test ClientOnly Runtime Data", "1.0.0",
	NetworkMode = NetworkMode.ClientOnly)]
public sealed class TestClientOnlyDataMod : ICuoMod
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
