using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// SHA-256 helpers for the manifest's integrity list. The digest is the
/// lowercase hex the format stores (§3.2), computed over the exact bytes on
/// disk — the same bytes the backup archive holds. (net48 has no
/// <c>SHA256.HashData</c>/<c>Convert.ToHexString</c>; the manual forms below are
/// what this TFM provides.)
/// </summary>
internal static class SaveArchiveChecksum
{
	/// <summary>Lowercase hex SHA-256 of the file's bytes.</summary>
	internal static string OfFile(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		return OfStream(stream);
	}

	/// <summary>Lowercase hex SHA-256 of a byte payload.</summary>
	internal static string OfBytes(byte[] bytes)
	{
		using var sha = SHA256.Create();
		return ToLowerHex(sha.ComputeHash(bytes));
	}

	/// <summary>Lowercase hex SHA-256 of a stream (used when hashing a ZIP entry).</summary>
	internal static string OfStream(Stream stream)
	{
		using var sha = SHA256.Create();
		return ToLowerHex(sha.ComputeHash(stream));
	}

	private static string ToLowerHex(byte[] digest)
	{
		var builder = new StringBuilder(digest.Length * 2);
		foreach (var value in digest)
		{
			builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
