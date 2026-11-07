using System;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The identity half of the handshake: compare the peer's declared block with
/// this build's own and answer with the one dimension that failed, or admit.
///
/// <para>
/// Equality is the rule on every dimension because a difference means a
/// different wire semantics — a game build's native calls, a CUO build's
/// message handling and a save schema's archive layout all change what the
/// other side would do with the same bytes. The handshake is the compatibility
/// boundary (decisions 137/188), and it stays strictly equal; this class only
/// makes the refusal NAME the difference. The mod list keeps its own policy
/// (<see cref="HandshakeModAdmission"/>), and the optional-capability
/// negotiation belongs to the adapter capability catalog's stage 2.
/// </para>
///
/// <para>
/// An absent block (empty strings and a zero schema) is refused by the first
/// dimension it fails, never admitted: a peer without the block is speaking a
/// wire this build does not have. Pre-release every client comes from this tree
/// (decision 241), so the only peer that sends no block is a stale one, and the
/// refusal is what tells its player to update.
/// </para>
/// </summary>
public static class HandshakeCompatibility
{
	public static HandshakeRefusal? Evaluate(ILocalPeerIdentity local, HandshakeMsg remote)
	{
		if (remote.Protocol != ProtocolVersion.Current)
		{
			return new HandshakeRefusal(
				HandshakeDimension.Protocol,
				string.Empty,
				ProtocolVersion.Current.ToString(CultureInfo.InvariantCulture),
				remote.Protocol.ToString(CultureInfo.InvariantCulture));
		}

		if (!string.Equals(remote.GameBuild, local.GameBuild, StringComparison.Ordinal))
		{
			return new HandshakeRefusal(HandshakeDimension.GameBuild, string.Empty, local.GameBuild, remote.GameBuild);
		}

		if (!string.Equals(remote.CuoBuild, local.CuoBuild, StringComparison.Ordinal))
		{
			return new HandshakeRefusal(HandshakeDimension.CuoBuild, string.Empty, local.CuoBuild, remote.CuoBuild);
		}

		if (remote.SaveSchema != local.SaveSchema)
		{
			return new HandshakeRefusal(
				HandshakeDimension.SaveSchema,
				string.Empty,
				local.SaveSchema.ToString(CultureInfo.InvariantCulture),
				remote.SaveSchema.ToString(CultureInfo.InvariantCulture));
		}

		return null;
	}
}
