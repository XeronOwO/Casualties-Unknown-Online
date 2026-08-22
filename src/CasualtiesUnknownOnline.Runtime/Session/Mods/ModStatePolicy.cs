namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety rails for the mod-state surface. The framework stores
/// opaque bytes per mod; without caps a single broken or hostile mod could
/// grow the host's save file without bound (or make every frame's atomic
/// write a multi-megabyte copy). Keys are bounded by length/count, values by
/// size — all errors are refused with a log, never silently truncated.
/// </summary>
internal static class ModStatePolicy
{
	public const int MaxKeyLength = 128;
	public const int MaxValueBytes = 64 * 1024;
	public const int MaxKeysPerMod = 1024;

	/// <summary>Keys must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidKey(string? key) =>
		!string.IsNullOrWhiteSpace(key) && key!.Length <= MaxKeyLength;

	/// <summary>Values must be non-null and within the per-value size cap (empty byte[] is valid).</summary>
	public static bool IsValidValue(byte[]? value) =>
		value is not null && value.Length <= MaxValueBytes;

	/// <summary>Adding a brand-new key must not exceed the per-mod key count cap.</summary>
	public static bool CanAddKey(int currentKeyCount) => currentKeyCount < MaxKeysPerMod;
}
