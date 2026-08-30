namespace CasualtiesUnknownOnline.Runtime.Steam;

/// <summary>
/// The single validation policy for IP-direct display names. It is shared by
/// the local IP adapter (refuse to host/join with an invalid configured name)
/// and the host's handshake gate (refuse an inbound peer whose displayed name
/// is empty or malformed). Keeping the policy in one place prevents the two
/// paths from drifting.
/// </summary>
public static class IpDisplayNamePolicy
{
	/// <summary>Maximum display name length. Must stay aligned with the UI text-field limit.</summary>
	public const int MaxLength = 24;

	/// <summary>
	/// Validates a display name and returns a user-facing error when invalid.
	/// A valid name is non-empty after trimming, no longer than
	/// <see cref="MaxLength"/>, and contains no control characters.
	/// </summary>
	public static bool TryValidate(string? name, out string error)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			error = "Display name is required.";
			return false;
		}

		var trimmed = name!.Trim();
		if (trimmed.Length > MaxLength)
		{
			error = $"Display name must be {MaxLength} characters or fewer.";
			return false;
		}

		foreach (var c in trimmed)
		{
			if (char.IsControl(c))
			{
				error = "Display name must not contain control characters.";
				return false;
			}
		}

		error = "";
		return true;
	}

	/// <summary>Returns the canonical trimmed form used when a valid name is stored.</summary>
	public static string Normalize(string? name) => (name ?? "").Trim();
}
