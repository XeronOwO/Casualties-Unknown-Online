using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The host-command test mod (Phase 4b). Synchronized with
/// RegisterCommand + ExecuteHostAction, so every TestNode discovers it and the
/// handshake admits matching copies. Commands: echo (returns the arguments),
/// hostaction (a host-action command that reports the requester) and fail
/// (throws — the framework returns a failure result instead of leaking the
/// exception through the pump). All state is instance state: the xunit runner
/// parallelizes test classes, and a shared static would race them.
/// </summary>
[CuoMod("test.commands", "Test Commands", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.RegisterCommand | ModPermission.ExecuteHostAction)]
public sealed class TestCommandMod : ICuoMod
{
	public IModContext? Context { get; private set; }

	public List<(string Name, IReadOnlyList<string> Arguments, ulong Requester)> Executions { get; } = [];

	public List<(string Name, IReadOnlyList<string> Arguments, ulong LocalSteamId)> ConsoleExecutions { get; } = [];

	public void Bind(IModContext context)
	{
		Context = context;
		context.Commands.Register(new ModCommand("echo", Echo));
		context.Commands.Register(new ModCommand("hostaction", HostAction, isHostAction: true));
		context.Commands.Register(new ModCommand("fail", _ => throw new InvalidOperationException("test.commands fail always throws")));
		context.Commands.Register(new ModCommand("long", _ => new string('x', 64 * 1024)));
		context.ConsoleCommands.Register(new ModConsoleCommand(
			"cping", "Local console echo", "/cping <text>", CommandPermission.Anyone,
			[CommandArgumentKind.Text], ConsoleEcho));
		context.ConsoleCommands.Register(new ModConsoleCommand(
			"chost", "Local host-only console echo", "/chost", CommandPermission.HostOnly,
			[], ConsoleHost));
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

	private string? Echo(IModCommandContext context)
	{
		Executions.Add((context.Name, [.. context.Arguments], context.RequesterSteamId));
		return string.Join(" ", context.Arguments);
	}

	private string? HostAction(IModCommandContext context)
	{
		Executions.Add((context.Name, [.. context.Arguments], context.RequesterSteamId));
		return $"host:{context.RequesterSteamId}";
	}

	private string? ConsoleEcho(IModConsoleCommandContext context)
	{
		ConsoleExecutions.Add((context.Name, [.. context.Arguments], context.LocalSteamId));
		return $"console:{string.Join(" ", context.Arguments)}";
	}

	private string? ConsoleHost(IModConsoleCommandContext context)
	{
		ConsoleExecutions.Add((context.Name, [.. context.Arguments], context.LocalSteamId));
		return $"host-only:{context.LocalSteamId}";
	}
}
