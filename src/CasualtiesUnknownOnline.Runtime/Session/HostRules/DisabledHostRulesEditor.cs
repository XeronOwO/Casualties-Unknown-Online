namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// Default composition host-rule editor: unavailable. The plugin replaces this
/// with the BepInEx ConfigEntry-backed editor so the command exists in the
/// Runtime-only test/diagnostics composition without a config surface.
/// </summary>
internal sealed class DisabledHostRulesEditor : IHostRulesEditor
{
	public bool TrySet(string property, string value, out string? error)
	{
		error = "Host-rule editing is unavailable in this composition.";
		return false;
	}
}
