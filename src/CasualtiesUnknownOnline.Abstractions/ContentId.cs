using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The canonical CUO content id — <c>namespace:path</c> — used by the console
/// resource vocabulary, mod content registration and any future content search.
/// The namespace identifies the owner (the built-in game content uses
/// <see cref="BuiltInNamespace"/>; a mod declares its own namespace through
/// <see cref="CuoModAttribute.Namespace"/>); the path is the content's bare id
/// inside that namespace.
///
/// The grammar is deliberately narrow so an id can never be ambiguous:
/// <list type="bullet">
/// <item><description>namespace: <c>[a-z][a-z0-9_]{0,31}</c></description></item>
/// <item><description>path: <c>[a-z0-9][a-z0-9_.-]{0,94}</c></description></item>
/// </list>
/// Parsing lower-cases its input, so <c>cu:Fentanyl</c> and <c>cu:fentanyl</c>
/// are the same id and <see cref="ToString"/> always returns the canonical
/// lower-case form.
/// </summary>
public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
{
	/// <summary>The namespace/path separator.</summary>
	public const char Separator = ':';

	/// <summary>The reserved built-in namespace for CUO/game content.</summary>
	public const string BuiltInNamespace = "cu";

	/// <summary>Maximum namespace length (32).</summary>
	public const int MaxNamespaceLength = 32;

	/// <summary>Maximum path length (95) — <c>32 + 1 + 95 = 128</c> keeps the canonical id within the mod-content id cap.</summary>
	public const int MaxPathLength = 95;

	/// <summary>Maximum canonical id length.</summary>
	public const int MaxLength = MaxNamespaceLength + 1 + MaxPathLength;

	private ContentId(string @namespace, string path)
	{
		Namespace = @namespace;
		Path = path;
	}

	/// <summary>The owning namespace, always lower-case.</summary>
	public string Namespace { get; }

	/// <summary>The bare content id inside the namespace, always lower-case.</summary>
	public string Path { get; }

	/// <summary>True when this id belongs to the built-in <c>cu</c> namespace.</summary>
	public bool IsBuiltIn => string.Equals(Namespace, BuiltInNamespace, StringComparison.Ordinal);

	/// <summary>
	/// Parse a canonical (or upper/mixed-case) <c>namespace:path</c> string.
	/// Returns false for a missing/extra separator, an invalid grammar, or an
	/// over-length part; never throws.
	/// </summary>
	public static bool TryParse(string? text, out ContentId id)
	{
		id = default;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		var trimmed = text!.Trim();
		var separator = trimmed.IndexOf(Separator);
		if (separator <= 0 || separator == trimmed.Length - 1)
		{
			return false;
		}

		if (trimmed.IndexOf(Separator, separator + 1) >= 0)
		{
			return false;
		}

		return TryCreate(trimmed.Substring(0, separator), trimmed.Substring(separator + 1), out id);
	}

	/// <summary>
	/// Parse a canonical id, throwing <see cref="FormatException"/> when it is
	/// invalid. Use this only for compile-time-known literals; external input
	/// goes through <see cref="TryParse"/>.
	/// </summary>
	public static ContentId Parse(string text) =>
		TryParse(text, out var id)
			? id
			: throw new FormatException($"'{text}' is not a valid content id (expected namespace:path).");

	/// <summary>
	/// Build an id from a namespace and a path. Both parts are lower-cased
	/// before validation, so mixed-case input is accepted and normalised. The
	/// parts are NOT trimmed: surrounding whitespace is invalid, so a typo such
	/// as <c>"cu : a"</c> is refused instead of silently normalised.
	/// </summary>
	public static bool TryCreate(string? @namespace, string? path, out ContentId id)
	{
		id = default;
		if (string.IsNullOrEmpty(@namespace) || string.IsNullOrEmpty(path))
		{
			return false;
		}

		var normalizedNamespace = @namespace!.ToLowerInvariant();
		var normalizedPath = path!.ToLowerInvariant();
		if (!IsValidNamespace(normalizedNamespace) || !IsValidPath(normalizedPath))
		{
			return false;
		}

		id = new ContentId(normalizedNamespace, normalizedPath);
		return true;
	}

	/// <summary>True when the text is a valid lower-case namespace (see the grammar above).</summary>
	public static bool IsValidNamespace(string? text)
	{
		if (text is null || text.Length is 0 or > MaxNamespaceLength)
		{
			return false;
		}

		if (!IsLowerLetter(text[0]))
		{
			return false;
		}

		for (var i = 1; i < text.Length; i++)
		{
			var c = text[i];
			if (IsLowerLetter(c) || IsDigit(c) || c == '_')
			{
				continue;
			}

			return false;
		}

		return true;
	}

	/// <summary>
	/// True when the text is a valid lower-case path (see the grammar above).
	/// Upper-case input is intentionally invalid here: registered content ids
	/// must already be canonical, while <see cref="TryCreate"/> normalises
	/// external input.
	/// </summary>
	public static bool IsValidPath(string? text)
	{
		if (text is null || text.Length is 0 or > MaxPathLength)
		{
			return false;
		}

		if (!IsPathStart(text[0]))
		{
			return false;
		}

		for (var i = 1; i < text.Length; i++)
		{
			var c = text[i];
			if (IsPathStart(c) || c == '_' || c == '.' || c == '-')
			{
				continue;
			}

			return false;
		}

		return true;
	}

	/// <inheritdoc />
	public bool Equals(ContentId other) =>
		string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
			&& string.Equals(Path, other.Path, StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is ContentId other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() =>
		((Namespace is null ? 0 : StringComparer.Ordinal.GetHashCode(Namespace)) * 397)
			^ (Path is null ? 0 : StringComparer.Ordinal.GetHashCode(Path));

	/// <inheritdoc />
	public int CompareTo(ContentId other) => string.CompareOrdinal(ToString(), other.ToString());

	/// <summary>
	/// Canonical <c>namespace:path</c> form; <see cref="string.Empty"/> for an
	/// uninitialised <c>default(ContentId)</c> (which is never a valid id).
	/// </summary>
	public override string ToString() =>
		Namespace is null || Path is null ? string.Empty : $"{Namespace}{Separator}{Path}";

	public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);

	public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);

	private static bool IsLowerLetter(char c) => c >= 'a' && c <= 'z';

	private static bool IsDigit(char c) => c >= '0' && c <= '9';

	private static bool IsPathStart(char c) => IsLowerLetter(c) || IsDigit(c);
}
