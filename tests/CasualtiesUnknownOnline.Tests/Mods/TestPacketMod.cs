using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The declared-packet test mod: one declaration per policy shape the matrix
/// has, each chain recording what ran on this copy instead of touching game
/// state, so the packet tests can assert exactly which copies ran, in which
/// stage order, with which sender — and, now that a packet carries a
/// <see cref="ModValue"/>, with which value. All state is instance state
/// (discovery instantiates one copy per node).
///
/// The ordinary value is an integer "step": 0 is the refusal/throw probe, a
/// positive one the ordinary case, and <see cref="RoundTripPacket"/> carries a
/// nested value so the framework's own encoding is exercised end to end.
/// </summary>
[CuoMod("test.packets", "Test Packets", "1.0.0",
	NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage)]
public sealed class TestPacketMod : ICuoMod
{
	/// <summary>HostOnly sender + HostOnly delivery: only the host may start it, and no member copy ever runs it.</summary>
	public const string HostOnlyPacket = "packets.hostonly";

	/// <summary>GuestOnly sender + EveryOtherMember: the host and the other members run it, never the reporter.</summary>
	public const string ReportPacket = "packets.report";

	/// <summary>AnyMember + EveryOtherMember: whoever sends it, the sender's own copy never runs it.</summary>
	public const string BroadcastPacket = "packets.broadcast";

	/// <summary>AnyMember + EveryMember: the sender's own copy runs first, then the host and the other members. Its validate stage refuses a step below 1, so a local refusal is observable here.</summary>
	public const string AllPacket = "packets.all";

	/// <summary>Declared out of stage order (observe, apply, validate, apply) — the run order is the framework's.</summary>
	public const string OrderedPacket = "packets.ordered";

	/// <summary>Validate refuses when the value is integer 0, so the delivery and the relay stop there.</summary>
	public const string RefusingPacket = "packets.refusing";

	/// <summary>Apply throws when the value is integer 0 — isolation must keep the observe stage running.</summary>
	public const string ThrowingPacket = "packets.throwing";

	/// <summary>Validate retires its own declaration mid-chain — the running chain must still finish.</summary>
	public const string SelfRetiringPacket = "packets.selfretire";

	/// <summary>Apply sends this very packet back through the surface — the second local run must be refused instead of recursing.</summary>
	public const string SelfSendPacket = "packets.selfsend";

	/// <summary>
	/// Any value the sender hands in reaches every copy unchanged, and every
	/// handler of one delivery reads the SAME value: a value is immutable, so
	/// there is no per-delivery copy left to protect the wire with.
	/// </summary>
	public const string RoundTripPacket = "packets.roundtrip";

	/// <summary>Every handler run on this copy, in run order: packet id, stage, marker, sender, the value it read.</summary>
	public List<(string PacketId, ModPacketStage Stage, string Marker, ulong Sender, ModValue Value)> Runs { get; } = [];

	public IModContext? Context { get; private set; }

	public void Bind(IModContext context)
	{
		Context = context;

		Declare(new ModPacket(HostOnlyPacket, ModPacketSender.HostOnly, ModPacketDelivery.HostOnly,
			Step(HostOnlyPacket, ModPacketStage.Validate, "validate"),
			Step(HostOnlyPacket, ModPacketStage.Apply, "apply")));

		Declare(new ModPacket(ReportPacket, ModPacketSender.GuestOnly, ModPacketDelivery.EveryOtherMember,
			Step(ReportPacket, ModPacketStage.Validate, "validate"),
			Step(ReportPacket, ModPacketStage.Apply, "apply"),
			Step(ReportPacket, ModPacketStage.Observe, "observe")));

		Declare(new ModPacket(AllPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryMember,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				Record(AllPacket, ModPacketStage.Validate, "validate")(ctx);
				if (!ctx.Value.TryGetInteger(out var step) || step < 1)
				{
					ctx.Refuse($"an all-member packet carries a positive step; this value was {ctx.Value}");
				}
			}),
			Step(AllPacket, ModPacketStage.Apply, "apply")));

		Declare(new ModPacket(BroadcastPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
			Step(BroadcastPacket, ModPacketStage.Validate, "validate"),
			Step(BroadcastPacket, ModPacketStage.Apply, "apply")));

		// Declared backwards on purpose: the framework runs the stages in its own
		// order and keeps the declared order inside a stage.
		Declare(new ModPacket(OrderedPacket, ModPacketSender.AnyMember, ModPacketDelivery.HostOnly,
			Step(OrderedPacket, ModPacketStage.Observe, "o1"),
			Step(OrderedPacket, ModPacketStage.Apply, "a1"),
			Step(OrderedPacket, ModPacketStage.Validate, "v1"),
			Step(OrderedPacket, ModPacketStage.Apply, "a2")));

		Declare(new ModPacket(RefusingPacket, ModPacketSender.GuestOnly, ModPacketDelivery.EveryOtherMember,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				Record(RefusingPacket, ModPacketStage.Validate, "validate")(ctx);
				if (!ctx.Value.TryGetInteger(out var step) || step == 0)
				{
					ctx.Refuse("test refusal");
				}
			}),
			Step(RefusingPacket, ModPacketStage.Apply, "apply")));

		Declare(new ModPacket(ThrowingPacket, ModPacketSender.AnyMember, ModPacketDelivery.HostOnly,
			Step(ThrowingPacket, ModPacketStage.Validate, "validate"),
			new ModPacketHandler(ModPacketStage.Apply, ctx =>
			{
				Record(ThrowingPacket, ModPacketStage.Apply, "apply")(ctx);
				if (!ctx.Value.TryGetInteger(out var step) || step == 0)
				{
					throw new InvalidOperationException("test.packets throwing handler");
				}
			}),
			Step(ThrowingPacket, ModPacketStage.Observe, "observe")));

		Declare(new ModPacket(SelfRetiringPacket, ModPacketSender.AnyMember, ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				Record(SelfRetiringPacket, ModPacketStage.Validate, "validate")(ctx);
				Context!.Packets.Unregister(SelfRetiringPacket);
			}),
			Step(SelfRetiringPacket, ModPacketStage.Apply, "apply")));

		Declare(new ModPacket(SelfSendPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryMember,
			Step(SelfSendPacket, ModPacketStage.Validate, "validate"),
			new ModPacketHandler(ModPacketStage.Apply, ctx =>
			{
				Record(SelfSendPacket, ModPacketStage.Apply, "apply")(ctx);
				Context!.Packets.SendToHost(SelfSendPacket, ctx.Value);
			})));

		Declare(new ModPacket(RoundTripPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryMember,
			Step(RoundTripPacket, ModPacketStage.Validate, "validate"),
			Step(RoundTripPacket, ModPacketStage.Apply, "apply")));

		// A declaration failure is a broken test fixture, not a scenario: fail the
		// bind loudly instead of leaving the packet tests to assert on nothing.
		void Declare(ModPacket packet)
		{
			if (!context.Packets.Register(packet))
			{
				throw new InvalidOperationException($"test.packets could not declare {packet.Id}");
			}
		}
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

	/// <summary>The whole run history of one packet id, as "stage:marker" rows.</summary>
	public List<string> RunsOf(string packetId)
	{
		var rows = new List<string>();
		foreach (var run in Runs)
		{
			if (run.PacketId == packetId)
			{
				rows.Add($"{run.Stage}:{run.Marker}");
			}
		}

		return rows;
	}

	/// <summary>The value each run of one packet read, in run order.</summary>
	public List<ModValue> ValuesOf(string packetId)
	{
		var values = new List<ModValue>();
		foreach (var run in Runs)
		{
			if (run.PacketId == packetId)
			{
				values.Add(run.Value);
			}
		}

		return values;
	}

	private ModPacketHandler Step(string packetId, ModPacketStage stage, string marker) =>
		new(stage, Record(packetId, stage, marker));

	private Action<IModPacketContext> Record(string packetId, ModPacketStage stage, string marker) =>
		ctx => Runs.Add((packetId, stage, marker, ctx.SenderSteamId, ctx.Value));
}
