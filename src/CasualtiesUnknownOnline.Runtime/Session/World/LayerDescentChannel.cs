using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's own layer advance as a shared edge: one host → member message sent at
/// the start of the host's regeneration, accepted by a member only for the generation
/// it is in.
/// <para>
/// Split out of <see cref="WorldStateMessageService"/> because it is one message
/// family with one decision (the stamp relation) and one side effect on the receiver
/// (drop the baseline for the layer being left, so the generation boundary holds for
/// the host's new one). The message and the start gate's expectation are ONE edge —
/// the facade pairs them — but only the send half belongs here.
/// </para>
/// </summary>
internal sealed class LayerDescentChannel(
	ISessionControl session,
	PacketSender sender,
	KernelWorldGenerationSource generations,
	Action dropHeldBaseline,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly KernelWorldGenerationSource _generations = generations;
	private readonly Action _dropHeldBaseline = dropHeldBaseline;
	private readonly ILogger _log = log;

	/// <summary>Guest: the host's descent was accepted for this side's generation — the run surface starts the local generation.</summary>
	public event Action<bool>? Descended;

	/// <summary>
	/// Host only: send the edge to every handshaken member currently in the world, stamped with the
	/// generation the host is in. Returns the members it went to, so the caller can record them as
	/// expected back on the start gate; empty means nothing was sent.
	/// </summary>
	public IReadOnlyList<ulong> Send(bool twice)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return [];
		}

		if (_generations.Stamp() is not { } stamp)
		{
			_log.LogWarning("[LayerDescent] this host cannot name the layer it is advancing (no committed run baseline) — no member is asked to follow it.");
			return [];
		}

		var members = _session.Members
			.Where(member => member.Handshaken && member.SteamId != _session.LocalSteamId && member.InWorld)
			.Select(member => member.SteamId)
			.ToArray();
		if (members.Length == 0)
		{
			_log.LogDebug("[LayerDescent] no member is in this host's world — the descent has nobody to follow it.");
			return [];
		}

		_sender.SendToAll(members, NetMsg.LayerDescent, new LayerDescentMsg { Twice = twice, Generation = stamp }, reliable: true);
		_log.LogInformation(
			"[LayerDescent] this host is advancing a layer (twice: {Twice}, {Generation}) — {Count} member(s) told to generate their own new layer from this host's baseline.",
			twice, WorldReportGeneration.Describe(_generations.Current), members.Length);
		return members;
	}

	/// <summary>
	/// Guest: the host announced its own layer advance. Refused unless it names the generation this side
	/// is in — a descent for another layer would generate a world the session is not in. The accepted
	/// edge drops the baseline this side holds BEFORE the run surface is told, so the generation hold at
	/// the boundary really holds until the kernel layer-advance brings the new one (the existing hold
	/// skips a params instance that is already the applied one).
	/// </summary>
	public void Receive(bool twice, WorldGenerationMsg? generation)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var relation = WorldReportGeneration.Relate(_generations.Current, generation);
		if (relation != WorldGenerationRelation.Current)
		{
			_log.LogInformation(
				"[LayerDescent] the host's descent names {Reported} while this member is at {Current} ({Relation}) — refused, so this member never generates a layer the session is not in.",
				WorldReportGeneration.Describe(generation), WorldReportGeneration.Describe(_generations.Current), relation);
			return;
		}

		_dropHeldBaseline();
		_log.LogInformation(
			"[LayerDescent] the host is advancing a layer now (twice: {Twice}, {Generation}) — this member follows with its own generation; the held baseline for the layer being left was dropped.",
			twice, WorldReportGeneration.Describe(_generations.Current));
		Descended?.Invoke(twice);
	}
}
