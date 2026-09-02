using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status transport test mod. It declares the same
/// <see cref="NetworkMode.Synchronized"/> + <see cref="ModPermission.SendNetworkMessage"/>
/// contract as the other message tests and routes every inbound mod-message
/// frame to <see cref="IModStatusTransport.TryHandleStatusPayload"/> so a
/// host-originated status update is applied to the local mirror automatically
/// in tests (a production mod may route non-status frames alongside it).
/// All state is instance state (the xunit runner parallelizes test classes,
/// and a shared static would race them).
/// </summary>
[CuoMod("test.status.sync", "Test Status Sync", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage)]
public sealed class TestStatusSyncMod : ICuoMod
{
	public IModContext? Context { get; private set; }

	/// <summary>Every inbound frame and whether the status transport consumed it as a typed status update.</summary>
	public List<(ulong Sender, bool Consumed)> Received { get; } = [];

	public void Bind(IModContext context)
	{
		Context = context;
		context.Network.MessageReceived += (sender, payload) =>
		{
			var consumed = context.StatusTransport.TryHandleStatusPayload(sender, payload);
			Received.Add((sender, consumed));
		};
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
