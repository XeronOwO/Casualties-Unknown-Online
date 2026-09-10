using CasualtiesUnknownOnline.Runtime.Networking;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The transport identity the save layer reads: which key space a run is in, the
/// local peer's id and the display name it advertises. A mutable test double so
/// one test can flip the mode without rebuilding the service.
/// </summary>
internal sealed class FakeTransportIdentity : ITransportIdentity
{
	public bool IsIpDirect { get; set; }

	public ulong LocalPeerId { get; set; } = 1001UL;

	public string LocalDisplayName { get; set; } = "Host";
}
