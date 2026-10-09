using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The example CUO mod — the Phase 4 Mod API's runtime verification target:
/// its lifecycle and every message it receives are logged, and each received
/// payload is echoed (a guest's report reaches the host's copy; the host
/// broadcasts the echo to every member). Synchronized: both sides must run the
/// same version, so the handshake consistency check admits the pair and a
/// missing copy is refused — exactly what the two-process verification proves.
///
/// It shows both network forms side by side: the anonymous tunnel
/// (<see cref="IModNetwork"/>, one value per mod and one callback, echoed by
/// hand) and a DECLARED packet (<see cref="IModPackets"/>, an id the mod owns
/// with the framework doing the relay). The console command
/// <c>exampleecho &lt;text&gt;</c> drives the declared one on a real session.
/// </summary>
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand | ModPermission.ExecuteHostAction
		| ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
{
	/// <summary>The declared echo packet: whoever sends it, the other members run the chain.</summary>
	public const string EchoPacket = "example.echo";

	private IModContext? _context;

	public void Bind(IModContext context)
	{
		_context = context;
		context.Content.TryRegister(new ModItemDefinition
		{
			Id = "example.trophy",
			DisplayName = "Example Trophy",
			Description = "Content registered by the CUO example mod.",
			Category = "nospawn",
		});

		// The anonymous tunnel: one callback for every value this mod receives,
		// and the fan-out written by hand. The value is typed, so the echo wraps
		// what arrived instead of re-encoding text.
		context.Network.MessageReceived += (sender, value) =>
		{
			context.Logger.LogInformation("[Example] echo from {Sender}: {Value}", sender, value);
			if (context.Session.IsHost)
			{
				context.Network.Broadcast(ModValue.Map(("echo", value)));
			}
		};

		// The declared packet: the mod owns the message id and the chain, the
		// framework owns the frame, the routing and the relay.
		context.Packets.Register(new ModPacket(EchoPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				if (!ctx.Value.TryGetText(out var text) || text.Length == 0)
				{
					ctx.Refuse($"an echo carries non-empty text; this value was {ctx.Value}");
				}
			}),
			new ModPacketHandler(ModPacketStage.Apply, ctx =>
			{
				ctx.Value.TryGetText(out var text);
				context.Logger.LogInformation("[Example] {Packet} from {Sender}: {Text}", EchoPacket, ctx.SenderSteamId, text);
			})));

		context.Commands.Register(new ModCommand("echo", c => $"echo:{string.Join(" ", c.Arguments)}"));
		context.Commands.Register(new ModCommand("whoami", c => $"requester:{c.RequesterSteamId}", isHostAction: true));
		context.ConsoleCommands.Register(new ModConsoleCommand(
			"exampleecho",
			"Send the example's declared echo packet",
			"/exampleecho <text>",
			CommandPermission.Anyone,
			[CommandArgumentKind.Text],
			EchoFromConsole));
		context.Ui.Register("example", "CUO Example", window =>
		{
			window.Label($"session active: {context.Session.SessionActive}");
			window.Label($"host: {context.Session.HostSteamId}");
		});
		context.PlayerJoined += id => context.Logger.LogInformation("[Example] player {Id} joined.", id);
		context.PlayerLeft += id => context.Logger.LogInformation("[Example] player {Id} left.", id);
		context.SessionEnded += () => context.Logger.LogInformation("[Example] session ended.");
		context.Logger.LogInformation("[Example] bound (session active: {Active}, host: {Host}).",
			context.Session.SessionActive, context.Session.HostSteamId);
	}

	public void Initialize() => _context?.Logger.LogInformation("[Example] initialized.");

	public void Start() => _context?.Logger.LogInformation("[Example] started.");

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private string? EchoFromConsole(IModConsoleCommandContext ctx)
	{
		if (ctx.Arguments.Count == 0)
		{
			return "usage: /exampleecho <text>";
		}

		var text = string.Join(" ", ctx.Arguments);
		var value = ModValue.Text(text);

		// The packet is declared AnyMember + EveryOtherMember, so the sender's own
		// copy never runs it: a guest reports it (the host and the other members
		// run the chain) and the host broadcasts it to the other members.
		var sent = ctx.Session.IsHost
			? _context!.Packets.Broadcast(EchoPacket, value)
			: _context!.Packets.SendToHost(EchoPacket, value);

		return sent
			? ctx.Session.IsHost
				? $"broadcast \"{text}\" to the other members"
				: $"reported \"{text}\" — the host and the other members run the chain"
			: "the send was refused locally — see the log";
	}
}
