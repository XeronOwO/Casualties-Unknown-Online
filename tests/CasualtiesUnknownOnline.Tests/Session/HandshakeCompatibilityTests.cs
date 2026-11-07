using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The identity half of the structured handshake refusal (ticket
/// `handshake-identity-and-refusal-report.md`): each dimension a peer can differ
/// on answers with its own name, so the guest's status line can say which thing
/// was incompatible instead of retrying forever. The wire protocol stays the
/// strict boundary (decisions 137/188); a difference in the others still refuses.
/// </summary>
[Trait("Category", "Unit")]
public class HandshakeCompatibilityTests
{
	private static readonly LocalPeerIdentity Local = new("1.0.2", "0.1.0+abc1234", 1);

	private static HandshakeMsg Peer(int protocol, string gameBuild, string cuoBuild, int saveSchema) =>
		new() { Protocol = protocol, GameBuild = gameBuild, CuoBuild = cuoBuild, SaveSchema = saveSchema };

	[Fact]
	public void AMatchingIdentity_IsAdmitted() =>
		Assert.Null(HandshakeCompatibility.Evaluate(Local, Peer(ProtocolVersion.Current, "1.0.2", "0.1.0+abc1234", 1)));

	[Fact]
	public void AProtocolMismatch_RefusesByProtocol()
	{
		var refusal = HandshakeCompatibility.Evaluate(Local, Peer(ProtocolVersion.Current + 1, "1.0.2", "0.1.0+abc1234", 1));

		Assert.NotNull(refusal);
		Assert.Equal(HandshakeDimension.Protocol, refusal!.Dimension);
	}

	[Fact]
	public void AGameBuildMismatch_RefusesByGameBuildWithBothValues()
	{
		var refusal = HandshakeCompatibility.Evaluate(Local, Peer(ProtocolVersion.Current, "1.0.3", "0.1.0+abc1234", 1));

		Assert.NotNull(refusal);
		Assert.Equal(HandshakeDimension.GameBuild, refusal!.Dimension);
		Assert.Equal("1.0.2", refusal.HostValue);
		Assert.Equal("1.0.3", refusal.GuestValue);
	}

	[Fact]
	public void ACuoBuildMismatch_RefusesByCuoBuild()
	{
		var refusal = HandshakeCompatibility.Evaluate(Local, Peer(ProtocolVersion.Current, "1.0.2", "0.1.0+def5678", 1));

		Assert.NotNull(refusal);
		Assert.Equal(HandshakeDimension.CuoBuild, refusal!.Dimension);
	}

	[Fact]
	public void ASaveSchemaMismatch_RefusesBySaveSchema()
	{
		var refusal = HandshakeCompatibility.Evaluate(Local, Peer(ProtocolVersion.Current, "1.0.2", "0.1.0+abc1234", 2));

		Assert.NotNull(refusal);
		Assert.Equal(HandshakeDimension.SaveSchema, refusal!.Dimension);
	}

	[Fact]
	public void APeerWithoutTheIdentityBlock_IsRefusedByTheFirstDimensionItFails()
	{
		// A stale client sends the protocol but no identity block: it is speaking a wire
		// this build does not have, and the refusal names the dimension instead of the
		// host silently dropping the handshake and the guest retrying forever.
		var refusal = HandshakeCompatibility.Evaluate(Local, new HandshakeMsg { Protocol = ProtocolVersion.Current });

		Assert.NotNull(refusal);
		Assert.Equal(HandshakeDimension.GameBuild, refusal!.Dimension);
		Assert.Equal(string.Empty, refusal.GuestValue);
	}
}
