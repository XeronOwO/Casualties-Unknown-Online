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
	Permissions = ModPermission.RegisterCommand | ModPermission.ExecuteHostAction | ModPermission.RegisterLocalCommand)]
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
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"cping", "Local console echo", ConsoleEcho,
			parameters: [new ConsoleCommandParameter("text")], usage: "/cping <text>"));
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"chost", "Local host-only console echo", ConsoleHost,
			permission: CommandPermission.HostOnly, usage: "/chost"));
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"cresource", "Local resource-location echo", ConsoleResource,
			parameters: [new ConsoleCommandParameter("location", kind: CommandArgumentKind.ResourceLocation)], usage: "/cresource <location>"));
		// A mod parameter that declares a FRAMEWORK source instead of a completer of its own: the selector offers the @ forms and the members who are
		// reachable, exactly as a built-in selector parameter does (stage 2 of the console contract).
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"cselect", "Local selector echo", invocation => ConsoleAnswer.Ok(string.Join(" ", invocation.Arguments)),
			parameters: [new ConsoleCommandParameter("target", kind: CommandArgumentKind.Selector)], usage: "/cselect <target>"));
		// A mod command that REFUSES: the console must print its answer with the error kind, which is what the
		// typed-result contract exists for (a mod cannot forget to mark a refusal).
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"crefuse", "Local console refusal", _ => ConsoleAnswer.Refused("refused by the test mod"),
			usage: "/crefuse"));
		// A definition's own completer answers its parameter, and the second parameter sees the first one's value — the two facts a per-kind source cannot state.
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"cmode", "Pick a mode, then one of its levels", invocation => ConsoleAnswer.Ok("mode " + string.Join(" ", invocation.Arguments)),
			parameters:
			[
				new ConsoleCommandParameter("mode", new FixedChoiceCompleter("survivor", "spectator")),
				new ConsoleCommandParameter("level", new DelegateCompleter(Levels)),
			],
			aliases: ["cm"],
			usage: "/cmode <mode> <level>"));
		// A completer that throws must be isolated: the console logs it and offers nothing instead of taking the overlay down.
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"cthrow", "A completer that fails on purpose", _ => ConsoleAnswer.Silent,
			parameters:
			[
				new ConsoleCommandParameter(
					"anything",
					new DelegateCompleter(_ => throw new InvalidOperationException("completer failure, on purpose"))),
			],
			usage: "/cthrow <anything>"));
	}

	private static IReadOnlyList<CommandCompletion> Levels(ICompletionContext context) =>
		context.Arguments.Count == 0
			? []
			: context.Arguments[0] switch
			{
				"survivor" => [new CommandCompletion("calm"), new CommandCompletion("rough")],
				"spectator" => [new CommandCompletion("fast")],
				_ => [],
			};

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

	private ConsoleAnswer ConsoleEcho(ICommandInvocation context)
	{
		ConsoleExecutions.Add((context.Name, [.. context.Arguments], context.LocalSteamId));
		return ConsoleAnswer.Ok($"console:{string.Join(" ", context.Arguments)}");
	}

	private ConsoleAnswer ConsoleHost(ICommandInvocation context)
	{
		ConsoleExecutions.Add((context.Name, [.. context.Arguments], context.LocalSteamId));
		return ConsoleAnswer.Ok($"host-only:{context.LocalSteamId}");
	}

	private ConsoleAnswer ConsoleResource(ICommandInvocation context)
	{
		ConsoleExecutions.Add((context.Name, [.. context.Arguments], context.LocalSteamId));
		return ConsoleAnswer.Ok($"resource:{string.Join(" ", context.Arguments)}");
	}
}
