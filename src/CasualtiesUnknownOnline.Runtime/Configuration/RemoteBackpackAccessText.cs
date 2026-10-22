namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The text forms of <see cref="RemoteBackpackAccess"/>. The BepInEx config entry
/// and the console's host-rule JSON both carry text, so one place owns the
/// spelling (read case-insensitively, written lowercase) — the same shape
/// <see cref="NativeBindingParityText"/> uses for the other closed set.
/// </summary>
public static class RemoteBackpackAccessText
{
	public static bool TryParse(string? text, out RemoteBackpackAccess access)
	{
		switch (text?.Trim().ToLowerInvariant())
		{
			case "allow":
				access = RemoteBackpackAccess.Allow;
				return true;
			case "unconscious_or_dead":
				access = RemoteBackpackAccess.UnconsciousOrDeadOnly;
				return true;
			case "deny":
				access = RemoteBackpackAccess.Deny;
				return true;
			default:
				access = RemoteBackpackAccess.Allow;
				return false;
		}
	}

	public static string Format(RemoteBackpackAccess access) => access switch
	{
		RemoteBackpackAccess.UnconsciousOrDeadOnly => "unconscious_or_dead",
		RemoteBackpackAccess.Deny => "deny",
		_ => "allow",
	};
}
