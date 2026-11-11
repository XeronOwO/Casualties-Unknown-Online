using System;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The transport's link-loss edge: raised (on the main thread) when the active connection to one
/// peer ends. The transport reports the fact; what it means for the session — drop that member,
/// end the session — is the session layer's decision.
/// </summary>
internal interface IPeerDisconnectSource
{
	/// <summary>Raised when the transport stopped reading and writing one peer's connection.</summary>
	event Action<ulong>? PeerDisconnected;
}
