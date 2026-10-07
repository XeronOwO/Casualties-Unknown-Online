using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The end-of-layer choice's message flow: one guest → host request per member
/// click, arbitrated against the kernel run baseline's generation.
/// <para>
/// The choice belongs to the member — the native end-of-layer panel appears on
/// the client whose own body reached the layer's bottom, and that client's
/// "Continue" is its judgment that the layer is finished — while the layer stays
/// the host's to generate: the baseline (RNG state, world-defining fields, rarity
/// multipliers) is captured on the host's boundary, and a member's own
/// regeneration has nothing new to apply, because the params in its hand are the
/// layer it is leaving. The request is therefore an INTENT that drives the host's
/// own advance, and the exactly-once key is the identity every layer-relative
/// world report already carries: the kernel's (run epoch, layer index) generation.
/// </para>
/// <para>
/// Both refusals are conservative and named: a request from a peer that is not a
/// handshaken member of this session, and a request that does not name the
/// generation this host is in (the session already moved, or the requester holds
/// no committed run baseline and cannot be arbitrated at all). What an admitted
/// request means for the live world — driving the native entry, once — is the
/// world domain's decision, not this channel's.
/// </para>
/// </summary>
public sealed class LayerAdvanceRequestChannel(
	ISessionControl session,
	PacketSender sender,
	ItemKernelAuthority kernelAuthority,
	ILogger<LayerAdvanceRequestChannel> log) : ILayerAdvanceControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly KernelWorldGenerationSource _generations = new(kernelAuthority);
	private readonly ILogger<LayerAdvanceRequestChannel> _log = log;

	/// <summary>Host only: a member's choice was admitted (handshaken member, this host's own generation) — the world domain drives the layer's advance.</summary>
	public event Action<ulong>? LayerAdvanceRequested;

	/// <summary>
	/// Any role: report this side's end-of-layer choice to the host. Guest only (the host's and a solo
	/// player's own choice IS the session's advance, so nothing travels), and nothing is sent without a
	/// committed run baseline: an unstamped request names no layer to arbitrate against, and the caller
	/// must be told so it does not suppress a descent nobody was asked about.
	/// </summary>
	public bool TrySendLayerAdvanceRequest()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return false;
		}

		if (_generations.Stamp() is not { } stamp)
		{
			_log.LogDebug("[LayerChoice] no committed run baseline — the end-of-layer choice is not sent: this side cannot name the generation it is asking about.");
			return false;
		}

		_sender.Send(_session.HostSteamId, NetMsg.LayerAdvanceRequest, new LayerAdvanceRequestMsg { Generation = stamp });
		_log.LogInformation("[LayerChoice] sent this side's end-of-layer choice to the host ({Generation}).",
			WorldReportGeneration.Describe(_generations.Current));
		return true;
	}

	/// <summary>Host only: a member's end-of-layer choice arrived — admit it only from a handshaken member and only for the generation this host is in.</summary>
	public void HandleLayerAdvanceRequest(ulong sender, LayerAdvanceRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (!_session.TryGetMember(sender, out var member) || !member.Handshaken)
		{
			_log.LogWarning("[LayerChoice] an end-of-layer choice from {Sender} was refused: not a handshaken member of this session.", sender);
			return;
		}

		var relation = WorldReportGeneration.Relate(_generations.Current, msg.Generation);
		if (relation != WorldGenerationRelation.Current)
		{
			// A second member choosing while the first request is already being driven
			// lands here as Stale once this host's capture has bumped the layer; inside
			// the advance itself the native entry's own re-entrancy clauses refuse the
			// drive, so the layer still moves exactly once.
			_log.LogInformation(
				"[LayerChoice] {Sender}'s choice names {Reported} while this host is at {Current} ({Relation}) — refused, so the layer cannot advance twice.",
				sender, WorldReportGeneration.Describe(msg.Generation), WorldReportGeneration.Describe(_generations.Current), relation);
			return;
		}

		_log.LogInformation("[LayerChoice] {Sender} chose to continue at {Current} — the advance is this host's to drive.",
			sender, WorldReportGeneration.Describe(_generations.Current));
		LayerAdvanceRequested?.Invoke(sender);
	}
}
