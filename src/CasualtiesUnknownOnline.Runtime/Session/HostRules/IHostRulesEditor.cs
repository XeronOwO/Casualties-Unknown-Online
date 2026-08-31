namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// Writable host-rule surface. The Runtime command console depends on this
/// narrow interface so JSON host-rule commands stay testable; the BepInEx
/// plugin provides the concrete ConfigEntry-backed implementation.
/// </summary>
public interface IHostRulesEditor
{
	/// <summary>
	/// Applies one host-rule property/value pair. Property names are
	/// case-insensitive; values use the same text forms as the JSON provider
	/// (<c>true</c>/<c>false</c> or a number).
	/// </summary>
	bool TrySet(string property, string value, out string? error);
}
