using System;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The second example mod — the declared-packet platform's independent
/// consumer, and a real behaviour envelope rather than an echo: a machine
/// whose use is HOST-AUTHORITATIVE.
///
/// It declares two packets and never writes the report-and-fan-out dance by
/// hand:
/// <list type="bullet">
/// <item><c>machine.use</c> — <see cref="ModPacketSender.GuestOnly"/> +
/// <see cref="ModPacketDelivery.HostOnly"/>: a member's use reaches the host's
/// copy and stops there. The validate stage refuses a value that is not one
/// in-range charge step, and the apply stage changes the host's own state and
/// announces it.</item>
/// <item><c>machine.state</c> — <see cref="ModPacketSender.HostOnly"/> +
/// <see cref="ModPacketDelivery.EveryMember"/>: the host's announcement runs on
/// every member, the host's own copy included, which is where a guest's mirror
/// of the machine's state comes from.</item>
/// </list>
///
/// Both packets carry a <see cref="ModValue"/> — an integer, read back with
/// <c>TryGetInteger</c> — which is what a mod's own data looks like on this
/// platform: no serializer, no byte layout of its own, and the framework
/// refuses a value it cannot encode before any handler runs.
///
/// The console command <c>machineuse &lt;delta&gt;</c> drives it on a real
/// session: a guest's line reports the use to the host, the host applies and
/// announces, and every member logs the same charge.
/// </summary>
[CuoMod("cuo.example.machine", "CUO Example Machine", "0.1.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand)]
public sealed class ExampleMachineMod : ICuoMod
{
	/// <summary>The guest → host use report.</summary>
	public const string UsePacket = "machine.use";

	/// <summary>The host → every member state announcement.</summary>
	public const string StatePacket = "machine.state";

	/// <summary>The one charge step a use may ask for; anything else is refused by the validate stage.</summary>
	public const int MaxStep = 10;

	private IModContext? _context;
	private byte _charge;

	public void Bind(IModContext context)
	{
		_context = context;

		context.Packets.Register(new ModPacket(UsePacket, ModPacketSender.GuestOnly, ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				if (!ctx.Value.TryGetInteger(out var delta) || delta is < 1 or > MaxStep)
				{
					ctx.Refuse($"a use carries one charge step in 1..{MaxStep}; this value was {ctx.Value}");
				}
			}),
			new ModPacketHandler(ModPacketStage.Apply, ApplyUse)));

		context.Packets.Register(new ModPacket(StatePacket, ModPacketSender.HostOnly, ModPacketDelivery.EveryMember,
			new ModPacketHandler(ModPacketStage.Apply, ApplyState)));

		context.ConsoleCommands.Register(new ModConsoleCommand(
			"machineuse",
			"Report a use of the example machine to the host",
			"/machineuse <1-10>",
			CommandPermission.Anyone,
			[CommandArgumentKind.Number],
			UseFromConsole));

		context.Logger.LogInformation("[Machine] bound (charge {Charge}, packets {Use}/{State}).",
			_charge, UsePacket, StatePacket);
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

	/// <summary>The host's half of a use: change the authoritative state, then announce it to everyone.</summary>
	private void ApplyUse(IModPacketContext ctx)
	{
		ctx.Value.TryGetInteger(out var delta);
		_charge = (byte)Math.Min(_charge + delta, byte.MaxValue);
		_context!.Logger.LogInformation("[Machine] {Sender} charged the machine by {Delta} — charge is now {Charge}; announcing.",
			ctx.SenderSteamId, delta, _charge);

		// A declared packet sent from inside a chain handler: the announcement
		// runs on every member, this host's own copy included.
		_context.Packets.Broadcast(StatePacket, ModValue.Integer(_charge));
	}

	/// <summary>Every member's half of the announcement — the guest mirror is exactly this.</summary>
	private void ApplyState(IModPacketContext ctx)
	{
		_charge = ctx.Value.TryGetInteger(out var charge) ? (byte)charge : (byte)0;
		_context!.Logger.LogInformation("[Machine] charge {Charge} applied on {Side}.",
			_charge, ctx.IsHost ? "the host" : "a member");
	}

	private string? UseFromConsole(IModConsoleCommandContext ctx)
	{
		if (!int.TryParse(ctx.Arguments.Count > 0 ? ctx.Arguments[0] : string.Empty, out var delta)
			|| delta is < 1 or > MaxStep)
		{
			return $"usage: /machineuse <1-{MaxStep}>";
		}

		if (ctx.Session.IsHost)
		{
			// The declaration is the reason, not a hand-written check: a use is a
			// member's report, so the host's own copy may not start it.
			return $"{UsePacket} is a member's report — run this on a guest.";
		}

		return _context!.Packets.SendToHost(UsePacket, ModValue.Integer(delta))
			? $"machine use {delta} reported to the host (local charge {_charge})."
			: "the use was refused locally — see the log";
	}
}
