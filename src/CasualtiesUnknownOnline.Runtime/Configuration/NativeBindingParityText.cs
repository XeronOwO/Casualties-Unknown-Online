namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The text forms of <see cref="NativeBindingParity"/>. The BepInEx config entry and the console's
/// <c>/gamerule</c> value both name these levels, so one place owns the lowercase spelling. The
/// config entry does not read raw file text: its acceptable-value list is ordinal and clamps an
/// unrecognised spelling BEFORE this parser, to the rule's declared fallback; the parser itself is
/// case-insensitive, and the console's arguments are ordinal and accept only this spelling.
/// </summary>
public static class NativeBindingParityText
{
	public static bool TryParse(string? text, out NativeBindingParity parity)
	{
		switch (text?.Trim().ToLowerInvariant())
		{
			case "allow":
				parity = NativeBindingParity.Allow;
				return true;
			case "warn":
				parity = NativeBindingParity.Warn;
				return true;
			case "require":
				parity = NativeBindingParity.Require;
				return true;
			default:
				parity = NativeBindingParity.Warn;
				return false;
		}
	}

	public static string Format(NativeBindingParity parity) => parity switch
	{
		NativeBindingParity.Allow => "allow",
		NativeBindingParity.Require => "require",
		_ => "warn",
	};
}
