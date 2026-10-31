using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The example mod's console commands, in one place: the host-relayed echo (a command that runs on the host and answers the member who typed it), the local echo
/// that sends the mod's declared packet, and the weight dial for the attribute-declared dressing.
/// </summary>
/// <remarks>
/// Its own file because the console half is what a mod author copies first, and reading it should not mean scrolling past the item, liquid, tile and recipe
/// declarations. The registrations are still the framework's contract: a name, a description, a handler, parameters, an optional scope/aliases/usage — nothing the
/// mod invents.
/// </remarks>
internal static class ExampleConsoleCommands
{
	/// <summary>The weight the attribute-declared Example Dressing reports; the console dial below moves it.</summary>
	internal static float DeclaredWeight { get; private set; } = 1.5f;

	private static IModContext? s_context;

	/// <summary>Registers the mod's console surface. Called once from <c>Bind</c>.</summary>
	internal static void Register(IModContext context)
	{
		s_context = context;

		// The mixed half: a ModCommand is the flat delegate form and a ConsoleCommandDefinition carries the contract
		// (scope, parameters, aliases, usage). A mod may use either; the definition is the one that can be completed.
		context.Commands.Register(new ModCommand("whoami", c => $"requester:{c.RequesterSteamId}", isHostAction: true));

		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"examplehost", "Ask the HOST to echo a line — the answer names who asked", HostEchoFromConsole,
			scope: CommandScope.HostRelayed,
			parameters: [new ConsoleCommandParameter("text")],
			usage: "/examplehost <text>"));

		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"exampleecho", "Send the example's declared echo packet", EchoFromConsole,
			parameters: [new ConsoleCommandParameter("text")], usage: "/exampleecho <text>"));

		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"exampleweight", "Set the weight the attribute-declared Example Dressing reports", SetDeclaredWeightFromConsole,
			parameters: [new ConsoleCommandParameter("weight", new DelegateCompleter(_ => [new CommandCompletion("0.5"), new CommandCompletion("1")]))],
			aliases: ["eweight"], usage: "/exampleweight <weight>"));
	}

	private static ConsoleAnswer HostEchoFromConsole(ICommandInvocation ctx)
	{
		var text = string.Join(" ", ctx.Arguments);
		s_context?.Logger.LogInformation("[Example] host echo for {Requester}: {Text}", ctx.RequesterSteamId, text);

		// The answer travels as a CATALOGUE KEY plus its arguments, so the member reads it in the member's own language.
		return ConsoleAnswer.Ok($"host echo for {ctx.RequesterSteamId}: {text}", "cuo.example.hostecho.answer", ctx.RequesterSteamId.ToString(), text);
	}

	private static ConsoleAnswer EchoFromConsole(ICommandInvocation ctx)
	{
		if (ctx.Arguments.Count == 0)
		{
			return ConsoleAnswer.Refused("usage: /exampleecho <text>");
		}

		var text = string.Join(" ", ctx.Arguments);
		var value = ModValue.Text(text);

		// The packet is declared AnyMember + EveryOtherMember, so the sender's own copy never runs it: a guest reports it (the host and the other members run the
		// chain) and the host broadcasts it to the other members.
		var sent = ctx.Session.IsHost
			? s_context!.Packets.Broadcast(ExampleMod.EchoPacket, value)
			: s_context!.Packets.SendToHost(ExampleMod.EchoPacket, value);

		return sent
			? ctx.Session.IsHost
				? ConsoleAnswer.Ok($"broadcast \"{text}\" to the other members")
				: ConsoleAnswer.Ok($"reported \"{text}\" — the host and the other members run the chain")
			: ConsoleAnswer.Refused("the send was refused locally — see the log");
	}

	private static ConsoleAnswer SetDeclaredWeightFromConsole(ICommandInvocation ctx)
	{
		if (ctx.Arguments.Count == 0
			|| !float.TryParse(ctx.Arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
			|| weight < 0f)
		{
			return ConsoleAnswer.Refused("usage: /exampleweight <weight>");
		}

		DeclaredWeight = weight;
		s_context!.Logger.LogInformation(
			"[Example] the declared dressing now reports weight {Weight}; the next item-table rebuild carries it.", weight);
		return ConsoleAnswer.Ok($"the declared dressing reports weight {weight} from the next item-table rebuild");
	}
}
