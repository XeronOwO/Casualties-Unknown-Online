using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety rails for the mod-state surface. Without caps a single
/// broken or hostile mod could grow the host's save file without bound (or make
/// every frame's atomic write a multi-megabyte copy). Keys are bounded by
/// length/count, values by the size of their ENCODING — all errors are refused
/// with a log, never silently truncated.
/// </summary>
internal static class ModStatePolicy
{
	public const int MaxKeyLength = 128;

	/// <summary>The cap on a value's ENCODED size; the model's own structural budgets bound the shape inside it.</summary>
	public const int MaxValueBytes = 64 * 1024;

	public const int MaxKeysPerMod = 1024;

	/// <summary>Keys must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidKey(string? key) =>
		!string.IsNullOrWhiteSpace(key) && key!.Length <= MaxKeyLength;

	/// <summary>
	/// Values must be non-null and encodable inside <see cref="MaxValueBytes"/>.
	/// The encoder is the one validator — accepting a value IS being able to
	/// encode it — so there is no second rule here to drift from it, and
	/// <paramref name="refusal"/> carries its reason (the path inside the model
	/// and the budget it broke) for the caller's log line.
	/// </summary>
	public static bool IsValidValue(ModValue? value, out string? refusal) =>
		ModValueCodec.TryEncode(value, MaxValueBytes, out _, out refusal);

	/// <summary>Adding a brand-new key must not exceed the per-mod key count cap.</summary>
	public static bool CanAddKey(int currentKeyCount) => currentKeyCount < MaxKeysPerMod;
}
