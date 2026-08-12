using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The test program's one healthy CUO mod — discovered by every TestNode in
/// this process (the test assembly is in AppDomain.GetAssemblies()), so the
/// lifecycle/message tests assert on it through ModService.LoadedMods. All
/// state is instance state (the xunit runner parallelizes test classes, and a
/// shared static would race them). Synchronized = both sides must run it, so
/// the standard handshake setups pass the consistency check.
/// </summary>
[CuoMod("test.echo", "Test Echo", "1.0.0", NetworkMode = NetworkMode.Synchronized)]
public sealed class TestEchoMod : ICuoMod
{
	/// <summary>The lifecycle stages in call order (Bind is a phase of the discovery frame).</summary>
	public List<string> Lifecycle { get; } = [];

	/// <summary>The messages routed to this copy (senderSteamId, payload).</summary>
	public List<(ulong Sender, byte[] Payload)> Received { get; } = [];

	/// <summary>The bind-time context — the snapshot tests read Session from it.</summary>
	public IModContext? Context { get; private set; }

	public int UpdateCount { get; private set; }

	public void Bind(IModContext context)
	{
		Context = context;
		context.Network.MessageReceived += (sender, payload) => Received.Add((sender, payload));
		Lifecycle.Add("Bind");
	}

	public void Initialize() => Lifecycle.Add("Initialize");

	public void Start() => Lifecycle.Add("Start");

	public void Update()
	{
		Lifecycle.Add("Update");
		UpdateCount++;
	}

	public void Stop() => Lifecycle.Add("Stop");

	public void Dispose() => Lifecycle.Add("Dispose");
}
