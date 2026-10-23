namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// Writable host-rule surface. The Runtime command console depends on this narrow interface so the
/// console's <c>/gamerule</c> verb stays testable; the BepInEx plugin provides the concrete
/// ConfigEntry-backed implementation.
///
/// <para>
/// A rule is addressed by its <see cref="HostRuleId"/> rather than by a config property name: the
/// census already names every rule once, and the id is what the rule's value shape, its console name
/// and its announcement are all keyed by. The value is the CANONICAL text
/// <see cref="HostRuleValueText"/> produced, so a write can be applied without a second grammar in
/// the config layer; the implementation still re-reads it through the same parser before writing,
/// because this seam is public to the composition and must not trust its caller.
/// </para>
/// </summary>
public interface IHostRulesEditor
{
	/// <summary>
	/// Applies one host rule's canonical value. Returns false and sets
	/// <paramref name="error"/> when the rule is not writable in this composition or the value is not
	/// the canonical form of the rule's own kind.
	/// </summary>
	bool TrySet(HostRuleId rule, string value, out string? error);
}
