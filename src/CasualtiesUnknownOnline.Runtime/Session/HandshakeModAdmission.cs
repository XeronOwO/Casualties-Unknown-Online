using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The mod half of the handshake's policy, split out of `HandshakeHandler` so a
/// test can judge it without a session: the guest's declared list against the
/// host's discovered manifests, under the existing NetworkMode contract
/// (`docs/en/reference/mod-api.md#handshake-consistency`). The judgment is
/// unchanged — this class only returns it as a value (a named dimension plus a
/// detail) instead of logging and returning false at each site.
///
/// <para>
/// The refusal's detail is the mod id for every mod-dimension answer, and the two
/// values carry what each side declared (a missing mod reads as "none"). A
/// malformed list (empty or duplicated id, an invalid mode or permissions)
/// refuses as <see cref="HandshakeDimension.ModSet"/> with whatever id was
/// readable. A content fingerprint difference and a warn-parity native binding
/// are NOTES, never refusals (their comparison cannot be complete — decision 254).
/// </para>
/// </summary>
public static class HandshakeModAdmission
{
	/// <summary>
	/// Compare the member's list with the host's. A null refusal admits; a note is
	/// reported without a verdict (the content fingerprint and the warn-parity
	/// binding), never a refusal cause of its own.
	/// </summary>
	public static ModAdmissionResult Evaluate(
		IReadOnlyList<ModManifest> hostMods,
		IReadOnlyList<ModInfoMsg> guestMods,
		IReadOnlyDictionary<string, string> hostContent,
		NativeBindingParity parity)
	{
		// Shape: the member's list must be well-formed before any pair is compared.
		var guestIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var info in guestMods)
		{
			if (string.IsNullOrWhiteSpace(info.Id) || !guestIds.Add(info.Id))
			{
				return Refuse(info.Id ?? string.Empty);
			}

			if (info.NetworkMode == NetworkMode.Unspecified || !Enum.IsDefined(typeof(NetworkMode), info.NetworkMode))
			{
				return Refuse(info.Id);
			}

			if (!ModPermissionPolicy.IsValidFor(info.NetworkMode, info.Permissions))
			{
				return Refuse(info.Id);
			}
		}

		var notes = new List<ModAdmissionNote>();
		foreach (var hostMod in hostMods)
		{
			var guestInfo = guestMods.FirstOrDefault(g => string.Equals(g.Id, hostMod.Id, StringComparison.Ordinal));
			if (guestInfo is null)
			{
				if (IsStateBearing(hostMod.NetworkMode))
				{
					return Refuse(hostMod.Id, HandshakeDimension.ModSet, hostMod.NetworkMode.ToString(), "none");
				}

				continue;
			}

			var hostStateBearing = IsStateBearing(hostMod.NetworkMode);
			if ((hostStateBearing || IsStateBearing(guestInfo.NetworkMode)) && guestInfo.NetworkMode != hostMod.NetworkMode)
			{
				return Refuse(hostMod.Id, HandshakeDimension.ModSet, hostMod.NetworkMode.ToString(), guestInfo.NetworkMode.ToString());
			}

			if (hostStateBearing)
			{
				if (!SemanticVersion.TryParse(guestInfo.Version, out var guestVersion)
					|| !SemanticVersion.TryParse(hostMod.Version, out var hostVersion)
					|| !guestVersion!.PrecedenceEquals(hostVersion!))
				{
					return Refuse(hostMod.Id, HandshakeDimension.ModVersion, hostMod.Version, guestInfo.Version);
				}

				if (guestInfo.Permissions != hostMod.Permissions)
				{
					return Refuse(hostMod.Id, HandshakeDimension.ModSet, hostMod.Permissions.ToString(), guestInfo.Permissions.ToString());
				}
			}

			var hostFingerprint = hostContent.TryGetValue(hostMod.Id, out var fingerprint) ? fingerprint : null;
			if (!string.Equals(hostFingerprint, guestInfo.ContentFingerprint, StringComparison.Ordinal))
			{
				notes.Add(new ModAdmissionNote(
					ModAdmissionNoteKind.ContentParity,
					hostMod.Id,
					Declared(guestInfo.ContentFingerprint),
					Declared(hostFingerprint),
					hostMod.NetworkMode));
			}

			var hostBinding = NativeBindingDeclaration.Normalize(hostMod.NativeBinding);
			var guestBinding = NativeBindingDeclaration.Normalize(guestInfo.NativeBinding);
			if (NativeBindingDeclaration.Matches(hostBinding, guestBinding))
			{
				continue;
			}

			if (parity == NativeBindingParity.Require)
			{
				return Refuse(hostMod.Id, HandshakeDimension.ModSet, Declared(hostBinding), Declared(guestBinding));
			}

			if (parity == NativeBindingParity.Warn)
			{
				notes.Add(new ModAdmissionNote(
					ModAdmissionNoteKind.BindingWarn,
					hostMod.Id,
					Declared(guestBinding),
					Declared(hostBinding),
					hostMod.NetworkMode));
			}
		}

		// A member claiming a state-bearing mod the host does not run: the host
		// cannot arbitrate it, so it cannot be admitted.
		foreach (var guestInfo in guestMods)
		{
			if (hostMods.All(h => !string.Equals(h.Id, guestInfo.Id, StringComparison.Ordinal))
				&& IsStateBearing(guestInfo.NetworkMode))
			{
				return Refuse(guestInfo.Id, HandshakeDimension.ModSet, "none", guestInfo.NetworkMode.ToString());
			}
		}

		return new ModAdmissionResult(null, notes);
	}

	private static ModAdmissionResult Refuse(
		string detail,
		HandshakeDimension dimension = HandshakeDimension.ModSet,
		string hostValue = "",
		string guestValue = "") =>
		new(new HandshakeRefusal(dimension, detail, hostValue, guestValue), []);

	private static string Declared(string? value) => value ?? "none";

	private static bool IsStateBearing(NetworkMode mode) =>
		mode is NetworkMode.RequiresAllPlayers or NetworkMode.Synchronized or NetworkMode.Authoritative;
}
