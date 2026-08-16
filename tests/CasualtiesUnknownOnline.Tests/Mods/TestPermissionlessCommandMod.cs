using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// A synchronized mod with NO declared permissions. It attempts to register
/// an ordinary command and a host-action command during Bind; both must be
/// refused by the permission model (the registration booleans are asserted by
/// ModCommandTests). Its presence in the shared test assembly also proves a
/// permissionless synchronized mod still loads and handshakes — the missing
/// capabilities simply deny the operations, they do not break the mod.
/// </summary>
[CuoMod("test.commands.noperm", "No-Permission Commands", "1.0.0",
	NetworkMode = NetworkMode.Synchronized)]
public sealed class TestPermissionlessCommandMod : ICuoMod
{
	public IModContext? Context { get; private set; }

	public bool OrdinaryRegistration { get; private set; }

	public bool HostActionRegistration { get; private set; }

	public List<(ulong Sender, byte[] Payload)> Received { get; } = [];

	public void Bind(IModContext context)
	{
		Context = context;
		OrdinaryRegistration = context.Commands.Register(new ModCommand("ordinary", _ => null));
		HostActionRegistration = context.Commands.Register(new ModCommand("hostaction", _ => null, isHostAction: true));
		context.Network.MessageReceived += (sender, payload) => Received.Add((sender, payload));
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
