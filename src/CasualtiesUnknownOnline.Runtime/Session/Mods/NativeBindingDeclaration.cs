using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The declared native binding's one normalization/equality rule, shared by
/// discovery (<see cref="ModRegistry"/>) and the handshake's parity check
/// (<c>HandshakeHandler</c>): a blank declaration (empty or whitespace-only) is a
/// typo for "none" rather than a declaration, and the declared name is trimmed,
/// so two spellings of the same binding compare equal. A declared fact, never a
/// grant — nothing here rejects anything; only the host's configured parity
/// policy can, and it does so on a mismatch between two declarations.
/// </summary>
internal static class NativeBindingDeclaration
{
	/// <summary>Trims the declaration; blank becomes null (the undeclared state).</summary>
	internal static string? Normalize(string? declared)
	{
		var trimmed = declared?.Trim();
		return string.IsNullOrEmpty(trimmed) ? null : trimmed;
	}

	/// <summary>
	/// Both sides declared the same binding, or both declared none — ordinal
	/// string equality after normalization. A declaration missing on one side of a
	/// comparison is a difference (an undeclared binding must not read as "no
	/// opinion" and slip past a parity policy).
	/// </summary>
	internal static bool Matches(string? hostDeclared, string? memberDeclared) =>
		string.Equals(Normalize(hostDeclared), Normalize(memberDeclared), StringComparison.Ordinal);
}
