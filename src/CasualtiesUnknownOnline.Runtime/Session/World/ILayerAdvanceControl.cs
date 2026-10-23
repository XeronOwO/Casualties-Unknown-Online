using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The end-of-layer choice's control surface packet handlers operate on —
/// implemented by <see cref="LayerAdvanceRequestChannel"/>. Separate from
/// <see cref="IWorldControl"/> for the reason the world-time control is: the world
/// service is at its size gate, and this domain — one request per member click —
/// stays independently testable.
/// <para>
/// The choice is the member's own (the native end-of-layer panel appears on the
/// client whose own body is at the layer's bottom), while the layer itself stays
/// the host's to generate: the baseline is captured on the host's boundary. The
/// request therefore drives the host's own advance, and the exactly-once key is
/// the kernel generation every layer-relative report already carries.
/// </para>
/// </summary>
public interface ILayerAdvanceControl
{
	/// <summary>
	/// Any role: report this side's end-of-layer choice to the host. Returns true when a request went out:
	/// a guest in a live session WITH a committed run baseline (the stamp the host arbitrates against).
	/// False means nothing was sent — the host's and a solo player's own choice IS the session's advance, and
	/// a peer that cannot name its generation has nothing to ask about — and the caller must then NOT
	/// suppress its own descent, because a suppressed descent nobody was asked about moves neither the
	/// session nor the member.
	/// <para>
	/// <paramref name="twice"/> carries the drill pod's own two-layer step, so the host drives the step the
	/// requester's descent would have taken rather than the panel's one-layer advance.
	/// </para>
	/// </summary>
	bool TrySendLayerAdvanceRequest(bool twice);

	/// <summary>Host only: a member's end-of-layer choice arrived. It is admitted only from a handshaken member and only when it names the generation this host is in; what it then means for the live world — driving the native end-of-layer entry, exactly once — is the world domain's decision.</summary>
	void HandleLayerAdvanceRequest(ulong sender, LayerAdvanceRequestMsg msg);

	/// <summary>
	/// Host only: the admitted request could not be driven (no live world, or a regeneration already running).
	/// The requester suppressed its own descent when it asked, so this answer is the only way it learns the
	/// layer did not move. Sent to that requester only.
	/// </summary>
	void RefuseLayerAdvance(ulong requester, string reason);

	/// <summary>Guest: the host answered that it could not take this side's request — the refusal line, carrying the host's own reason.</summary>
	void HandleLayerAdvanceRefused(ulong sender, LayerAdvanceRefusedMsg msg);

	/// <summary>Guest: the host's refusal of this side's layer-advance request, with the host's own reason sentence.</summary>
	event Action<string>? LayerAdvanceRefused;

	/// <summary>Host only: a member's end-of-layer choice was admitted for the layer this host is in — the world's next step is the host's to take, at the step (one or two layers) the requester's descent carries.</summary>
	event Action<ulong, bool>? LayerAdvanceRequested;
}
