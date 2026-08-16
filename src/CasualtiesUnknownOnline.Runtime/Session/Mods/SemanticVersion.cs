using System;
using System.Globalization;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// A strict SemVer 2.0.0 parser/comparer (major.minor.patch[-prerelease][+build]).
/// The framework uses it to validate every declared mod version at discovery and
/// to compare state-bearing mod versions in the handshake by PRECEDENCE (build
/// metadata is ignored, as the spec prescribes). The full precedence ordering is
/// implemented so a future compatibility-range policy can reuse it — this round
/// still requires precedence equality for state-bearing modes (no compatibility
/// range is declared by the API yet).
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>
{
	/// <summary>The framework's version-string length cap (a manifest line, not a document).</summary>
	public const int MaxLength = 128;

	private readonly string[] _prerelease;

	private SemanticVersion(string raw, int major, int minor, int patch, string[] prerelease)
	{
		Raw = raw;
		Major = major;
		Minor = minor;
		Patch = patch;
		_prerelease = prerelease;
	}

	public string Raw { get; }

	public int Major { get; }

	public int Minor { get; }

	public int Patch { get; }

	/// <summary>
	/// Parse a strict SemVer string (no leading/trailing whitespace; numeric
	/// identifiers have no leading zeroes; identifier characters are
	/// [0-9A-Za-z-]). Returns false rather than throwing — discovery and the
	/// handshake are fail-closed paths, not exception paths.
	/// </summary>
	public static bool TryParse(string? text, out SemanticVersion? version)
	{
		version = null;
		if (string.IsNullOrEmpty(text) || text!.Length > MaxLength)
		{
			return false;
		}

		var coreAndPre = text!;
		var buildStart = coreAndPre.IndexOf('+');
		if (buildStart >= 0)
		{
			if (buildStart == 0 || buildStart == coreAndPre.Length - 1 || !IsValidIdentifierSequence(coreAndPre, buildStart + 1))
			{
				return false;
			}

			coreAndPre = text!.Substring(0, buildStart);
		}

		var preStart = coreAndPre.IndexOf('-');
		var core = preStart >= 0 ? coreAndPre.Substring(0, preStart) : coreAndPre;
		if (!TryParseCore(core, out var major, out var minor, out var patch))
		{
			return false;
		}

		string[] prerelease = [];
		if (preStart >= 0)
		{
			var preText = coreAndPre.Substring(preStart + 1);
			if (preText.Length == 0 || !TrySplitIdentifiers(preText, out prerelease))
			{
				return false;
			}
		}

		version = new SemanticVersion(text!, major, minor, patch, prerelease);
		return true;
	}

	/// <summary>True when major/minor/patch/prerelease compare equal (build metadata ignored — the handshake policy).</summary>
	public bool PrecedenceEquals(SemanticVersion other) => CompareTo(other) == 0;

	public int CompareTo(SemanticVersion? other)
	{
		if (other is null)
		{
			return 1;
		}

		if (Major != other.Major)
		{
			return Major.CompareTo(other.Major);
		}

		if (Minor != other.Minor)
		{
			return Minor.CompareTo(other.Minor);
		}

		if (Patch != other.Patch)
		{
			return Patch.CompareTo(other.Patch);
		}

		return ComparePrerelease(_prerelease, other._prerelease);
	}

	public override string ToString() => Raw;

	private static bool TryParseCore(string core, out int major, out int minor, out int patch)
	{
		major = 0;
		minor = 0;
		patch = 0;
		var parts = core.Split('.');
		return parts.Length == 3
			&& TryParseNumeric(parts[0], out major)
			&& TryParseNumeric(parts[1], out minor)
			&& TryParseNumeric(parts[2], out patch);
	}

	private static bool TryParseNumeric(string text, out int value)
	{
		value = 0;
		if (text.Length == 0 || (text.Length > 1 && text[0] == '0'))
		{
			return false;
		}

		return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
	}

	private static bool TrySplitIdentifiers(string text, out string[] identifiers)
	{
		var parts = text.Split('.');
		if (parts.Any(p => p.Length == 0 || !IsValidIdentifier(p)))
		{
			identifiers = [];
			return false;
		}

		identifiers = parts;
		return true;
	}

	private static bool IsValidIdentifierSequence(string text, int start) =>
		start < text.Length && TrySplitIdentifiers(text.Substring(start), out _);

	private static bool IsValidIdentifier(string text)
	{
		if (text.Length == 0)
		{
			return false;
		}

		foreach (var c in text)
		{
			if (!IsIdentifierChar(c))
			{
				return false;
			}
		}

		// A purely numeric identifier must not carry leading zeroes (01 is invalid).
		return text.Length == 1 || text[0] != '0' || !IsNumeric(text);
	}

	private static bool IsIdentifierChar(char c) =>
		c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-';

	private static int ComparePrerelease(string[] left, string[] right)
	{
		if (left.Length == 0 && right.Length == 0)
		{
			return 0;
		}

		if (left.Length == 0)
		{
			return 1; // a release version outranks any prerelease
		}

		if (right.Length == 0)
		{
			return -1;
		}

		var count = Math.Min(left.Length, right.Length);
		for (var i = 0; i < count; i++)
		{
			var comparison = CompareIdentifier(left[i], right[i]);
			if (comparison != 0)
			{
				return comparison;
			}
		}

		return left.Length.CompareTo(right.Length);
	}

	private static int CompareIdentifier(string left, string right)
	{
		var leftNumeric = IsNumeric(left);
		var rightNumeric = IsNumeric(right);
		if (leftNumeric && rightNumeric)
		{
			var comparison = left.Length.CompareTo(right.Length);
			return comparison != 0 ? comparison : string.CompareOrdinal(left, right);
		}

		if (leftNumeric)
		{
			return -1; // numeric identifiers outrank? No — numeric sorts BELOW alphanumeric
		}

		if (rightNumeric)
		{
			return 1;
		}

		return string.CompareOrdinal(left, right);
	}

	private static bool IsNumeric(string text)
	{
		foreach (var c in text)
		{
			if (c is < '0' or > '9')
			{
				return false;
			}
		}

		return true;
	}
}
