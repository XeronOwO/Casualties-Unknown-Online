using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The path-safety rule of the whole archive layer: every name that arrives
/// from outside — a payload path a caller hands to the writer, a player key, a
/// ZIP entry name in a backup — is validated here before it is joined onto a
/// destination. A name that is absolute, drive-qualified, rooted, UNC, or walks
/// up with <c>..</c> is rejected rather than rewritten, so a hostile or
/// corrupted archive can never write outside its world folder.
/// </summary>
internal static class ArchivePathPolicy
{
	private static readonly char[] Separators = ['/', '\\'];

	/// <summary>
	/// Validates a caller-supplied snapshot-relative path (forward slashes are the format's separator)
	/// and returns it in its canonical form. Throws <see cref="SaveArchivePathException"/> when unsafe.
	/// </summary>
	internal static string ValidateSnapshotPath(string? path)
	{
		var reason = DescribeUnsafePath(path);
		if (reason is not null)
		{
			throw new SaveArchivePathException(path ?? string.Empty, reason);
		}

		return Canonicalize(path!);
	}

	/// <summary>The unsafe-naming reason, or null when the path is a safe relative snapshot path.</summary>
	internal static string? DescribeUnsafePath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "the path is empty";
		}

		var text = path!.Trim();
		if (text.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
		{
			return "the path contains characters that are invalid in a path";
		}

		if (text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("\\", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
		{
			return "the path is root-relative";
		}

		if (text.StartsWith("\\\\", StringComparison.Ordinal))
		{
			return "the path is a UNC path";
		}

		if (text.Length >= 2 && text[1] == ':')
		{
			return "the path is drive-qualified";
		}

		var segments = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
		if (segments.Any(segment => segment is "." or ".."))
		{
			return "the path contains a '.' or '..' segment";
		}

		return null;
	}

	/// <summary>
	/// Canonicalizes a snapshot-relative path: forward slashes, no empty segments.
	/// Only call this after <see cref="DescribeUnsafePath"/> returned null.
	/// </summary>
	internal static string Canonicalize(string path) =>
		string.Join("/", path.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries));

	/// <summary>
	/// Validates a ZIP entry name read from a backup archive. Directory entries are addressed
	/// by their canonical parent path; every other name must be a safe relative path.
	/// </summary>
	internal static string ValidateArchiveEntryName(string? entryName)
	{
		if (string.IsNullOrWhiteSpace(entryName))
		{
			throw new SaveArchivePathException(entryName ?? string.Empty, "the ZIP entry name is empty");
		}

		var text = entryName!.TrimEnd('/', '\\');
		if (text.Length == 0)
		{
			return string.Empty;
		}

		var reason = DescribeUnsafePath(text);
		if (reason is not null)
		{
			throw new SaveArchivePathException(entryName!, reason);
		}

		return Canonicalize(text);
	}

	/// <summary>True = <paramref name="candidate"/> is <paramref name="root"/> or lies below it (both full paths).</summary>
	internal static bool IsWithin(string root, string candidate)
	{
		var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
		var normalizedCandidate = Path.GetFullPath(candidate);
		return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(normalizedCandidate, normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
	}

	private static string EnsureTrailingSeparator(string path) =>
		path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

	/// <summary>Joins a validated relative path onto a destination root.</summary>
	internal static string CombineWithin(string root, string relativePath)
	{
		var combined = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
		return IsWithin(root, combined)
			? combined
			: throw new SaveArchivePathException(relativePath, "the path resolves outside the archive root");
	}

	/// <summary>The canonical relative paths of every file below <paramref name="root"/>, sorted.</summary>
	internal static IReadOnlyList<string> EnumerateFiles(string root)
	{
		var prefixLength = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1;
		return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
			.Select(path => Path.GetFullPath(path).Substring(prefixLength).Replace(Path.DirectorySeparatorChar, '/'))
			.OrderBy(path => path, StringComparer.Ordinal)];
	}
}
