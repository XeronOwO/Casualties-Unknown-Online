using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The transport-scoped player identity behind <c>characters/&lt;playerKey&gt;.json</c>
/// (decision 162, §2): Steam players key on their account, IP-direct players key
/// on their display name because that mode has no account identity. The two
/// prefixes keep the key spaces distinct — a Steam world is never claimed by an
/// IP-direct name collision.
///
/// The key is also a file name, so it is normalized: ASCII letters and digits
/// survive, runs of anything else collapse into one separator. When that would
/// lose identity (a name written in a non-Latin script, or one long enough to be
/// truncated) a short digest of the ORIGINAL name is appended, because two
/// players who collapse to the same key would overwrite each other's character.
/// </summary>
public static class PlayerKey
{
	/// <summary>The longest sanitized name this format keeps before it truncates and digests.</summary>
	public const int MaxSanitizedLength = 64;

	public const string SteamPrefix = "steam-";
	public const string NamePrefix = "name-";

	/// <summary>The fallback identity when a display name carries nothing usable at all.</summary>
	public const string Unnamed = "unnamed";

	/// <summary>The suffix marking a key whose readable part lost information.</summary>
	internal const string DigestMarker = "-x";

	private const int DigestLength = 8;
	private const char Separator = '-';

	/// <summary>Steam transport: <c>steam-&lt;steamId64&gt;</c>.</summary>
	public static string ForSteam(ulong steamId64) =>
		SteamPrefix + steamId64.ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// IP-direct transport: <c>name-&lt;sanitized display name&gt;</c>. Two display names that
	/// differ only in case, spacing or punctuation are the same player — that is the
	/// game's own name handling — but two that differ in their letters, or that are
	/// too long to keep whole, stay distinct keys.
	/// </summary>
	public static string ForDisplayName(string? displayName) =>
		NamePrefix + Normalize(displayName);

	/// <summary>The sanitized form used by <see cref="ForDisplayName"/> (without the prefix).</summary>
	public static string Normalize(string? displayName)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return Unnamed;
		}

		var trimmed = displayName!.Trim();
		var sanitized = Sanitize(trimmed, out var lostInformation);
		if (sanitized.Length == 0)
		{
			// Nothing readable survived; the digest is the only identity left.
			return Unnamed + DigestMarker + DigestOf(trimmed);
		}

		return lostInformation ? sanitized + DigestMarker + DigestOf(trimmed) : sanitized;
	}

	/// <summary>Keeps ASCII letters and digits, folds case, collapses every other run into one separator.</summary>
	private static string Sanitize(string text, out bool lostInformation)
	{
		var builder = new StringBuilder(Math.Min(text.Length, MaxSanitizedLength));
		var lastWasSeparator = false;
		lostInformation = false;
		foreach (var character in text)
		{
			if (character < 128 && char.IsLetterOrDigit(character))
			{
				if (builder.Length >= MaxSanitizedLength)
				{
					lostInformation = true;
					break;
				}

				builder.Append(char.ToLowerInvariant(character));
				lastWasSeparator = false;
				continue;
			}

			// A rune this format cannot write is dropped identity, and the digest in
			// the final key has to take over. Every other non-alphanumeric character
			// is only spacing/punctuation: it collapses into a separator, which is
			// what makes "Alice  Bob" and "alice-bob" the same player.
			lostInformation |= character >= 128;

			if (lastWasSeparator || builder.Length == 0)
			{
				continue;
			}

			builder.Append(Separator);
			lastWasSeparator = true;
		}

		while (builder.Length > 0 && builder[builder.Length - 1] == Separator)
		{
			builder.Length--;
		}

		return builder.ToString();
	}

	private static string DigestOf(string text)
	{
		using var sha = SHA256.Create();
		var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
		var builder = new StringBuilder(DigestLength);
		for (var index = 0; builder.Length < DigestLength; index++)
		{
			builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
