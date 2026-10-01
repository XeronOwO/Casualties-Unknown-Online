using System;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The transport's refusal-escalation edge: raised (on the main thread, from
/// the transport's frame tick) once per congestion episode when a peer has
/// refused every send for the stall bound, and reset when the session it
/// belonged to ends. The transport reports the fact; what it means for the
/// session — drop that member, keep playing — is the session layer's decision.
/// </summary>
internal interface ISendStallSource
{
	/// <summary>Raised once per episode for a peer whose queue has refused everything past the stall bound.</summary>
	event Action<ulong>? PeerSendStalled;

	/// <summary>The session ended: no refusal episode, report window or suppression count
	/// from it may be reported or escalated in the next one.</summary>
	void ResetRefusals();
}
