using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The test mod for the local mod UI surface. It registers one immediate-mode
/// window in Bind so the ModUiTests can assert registration, the control list,
/// and the exact draw call sequence through a fake <see cref="IModUiWindow"/>.
/// ClientOnly keeps it out of state-bearing handshake obligations.
/// </summary>
[CuoMod("test.ui", "Test UI", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
public sealed class TestUiMod : ICuoMod
{
	/// <summary>The bind-time context (the UI tests read Ui from it).</summary>
	public IModContext? Context { get; private set; }

	/// <summary>True when the window registration succeeded during Bind.</summary>
	public bool Registered { get; private set; }

	public void Bind(IModContext context)
	{
		Context = context;
		Registered = context.Ui.Register("status", "Test Status", window =>
		{
			window.Label("hello");
			window.Separator();
			window.Button("click");
			window.TextField("seed", 16);
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
