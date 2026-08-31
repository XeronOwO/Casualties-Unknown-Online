using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Applies a parsed flat JSON object to an <see cref="IHostRulesEditor"/>.
/// This is the command-side seam between the console JSON argument provider
/// and the host-rule write surface; keeping it separate from
/// <see cref="CommandConsoleService"/> preserves the service under the
/// line-count gate.
/// </summary>
public static class HostRulesJsonApplier
{
	public static bool TryApply(string json, IHostRulesEditor editor, out int updated, out string? error)
	{
		updated = 0;
		if (!HostRulesJsonParser.TryParse(json, out var values, out error))
		{
			return false;
		}

		foreach (var pair in values)
		{
			if (!editor.TrySet(pair.Key, pair.Value, out error))
			{
				return false;
			}

			updated++;
		}

		error = null;
		return true;
	}
}
