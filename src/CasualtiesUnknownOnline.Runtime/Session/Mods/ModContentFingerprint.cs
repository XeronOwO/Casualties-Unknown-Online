using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// What two peers can honestly compare about their content: the ADDRESS every mod
/// registered — the owning mod id, the entry's canonical id, its kind and its
/// schema version — and nothing else. A definition's other members may COMPUTE
/// their values (decision 251), so a value is not hashable in general: hashing one
/// would report a difference that is not a difference. The address is what the
/// registry keys on, what the handshake can carry and what a saved row names.
///
/// ONE canonical text, two readers: its digest is what the handshake compares per
/// mod and what a cut records in its manifest, so an entry that appears, vanishes
/// or changes kind or schema version moves both.
///
/// The encoding is injective, and that is not decoration: every variable-length
/// field is length-prefixed because the registration rail only requires a KIND to
/// be non-whitespace (<see cref="ModContentPolicy.IsValidKind"/>) and a mod id to
/// be non-blank, so either may contain the separators this text uses — an
/// unescaped rendering would let two different content sets produce one text.
/// </summary>
internal static class ModContentFingerprint
{
	/// <summary>One line per entry, canonical and sorted — the text a digest is taken over.</summary>
	internal static IReadOnlyList<string> Lines(IEnumerable<ModContentRegistration> entries)
	{
		var lines = new List<string>();
		foreach (var entry in entries)
		{
			// The canonical `namespace:path` when the owning mod declared a namespace (the
			// address the world's rows and the resource vocabulary use), the mod-scoped id
			// otherwise — so a namespace that appears or changes is a difference too.
			var address = entry.TryGetCanonicalId(out var canonical) ? canonical.ToString() : entry.Definition.Id;
			lines.Add($"{Field(entry.ModId)}\t{Field(address)}\t{Field(entry.Definition.Kind)}\t{entry.Definition.SchemaVersion.ToString(CultureInfo.InvariantCulture)}");
		}

		lines.Sort(StringComparer.Ordinal);
		return lines;
	}

	/// <summary>Lowercase hex SHA-256 over the canonical text, the form the manifest spells a digest in (§3.2).</summary>
	internal static string Digest(IReadOnlyList<string> lines) =>
		SaveArchiveChecksum.OfBytes(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

	private static string Field(string value) => $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";
}
