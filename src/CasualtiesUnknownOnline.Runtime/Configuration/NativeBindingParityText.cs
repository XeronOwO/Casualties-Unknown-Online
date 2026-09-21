namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The text forms of <see cref="NativeBindingParity"/>. The BepInEx config entry
/// and the console's host-rule JSON both carry text, so one place owns the
/// spelling (read case-insensitively, written lowercase).
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
